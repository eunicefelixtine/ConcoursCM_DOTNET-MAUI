namespace ConcoursCM.Services;

public class PublicationForm
{
    public bool EstBourse { get; set; }
    public string Intitule { get; set; } = "";
    public DateTime DateCloture { get; set; } = DateTime.Today.AddDays(30);
    public DateTime DateEpreuves { get; set; } = DateTime.Today.AddDays(60);
    public int Places { get; set; }
    public int FraisFcfa { get; set; }
    public string Lien { get; set; } = "";
    public string ArreteSource { get; set; } = "";
    public string Reference { get; set; } = "";
    // Bourse
    public string Portee { get; set; } = "National";
    public string Avantages { get; set; } = "";
    public string NiveauCible { get; set; } = "";
}

/// <summary>
/// Publication contrôlée : seules les institutions accréditées publient, avec des règles anti-fraude
/// (lien HTTPS officiel, arrêté PDF, dates cohérentes, anti-doublon). La modération est assurée par
/// le backend ; hors ligne, elle est simulée localement.
/// </summary>
public class PublicationService(DatabaseService dbs, AlerteService alertes, ApiClient api)
{
#if DEBUG
    static readonly TimeSpan DelaiModeration = TimeSpan.Zero;          // démo : validation immédiate
#else
    static readonly TimeSpan DelaiModeration = TimeSpan.FromHours(24); // production : vérification sous 24h
#endif

    static readonly string[] Raccourcisseurs = ["bit.ly", "tinyurl.com", "t.co", "wa.me", "t.me", "facebook.com", "fb.com", "linktr.ee", "shorturl.at"];
    static readonly string[] HotesBoursesFiables = ["campusfrance.org", "chevening.org", "fcdo.gov.uk", "daad.de", "gov.uk", "minesup.gov.cm", "scholarships.gc.ca"];

    public async Task<OpResult> SoumettreAsync(Utilisateur u, PublicationForm f)
    {
        if (!u.PeutPublier) return OpResult.Echec("Votre accréditation n'est pas encore validée. La vérification prend jusqu'à 24h ouvrées.");

        if (string.IsNullOrWhiteSpace(f.Intitule) || f.Intitule.Trim().Length < 10)
            return OpResult.Echec("L'intitulé officiel doit contenir au moins 10 caractères.");
        if (f.DateCloture.Date < DateTime.Today.AddDays(3))
            return OpResult.Echec("La date limite de dépôt doit être au moins 3 jours après aujourd'hui.");
        if (!f.EstBourse)
        {
            if (f.DateEpreuves.Date <= f.DateCloture.Date) return OpResult.Echec("La date des épreuves doit suivre la clôture des dépôts.");
            if (f.Places <= 0 || f.Places > 100000) return OpResult.Echec("Indiquez un nombre de places valide.");
            if (f.FraisFcfa < 0 || f.FraisFcfa > 200000) return OpResult.Echec("Frais de quittance invalides (0 à 200 000 FCFA).");
        }
        else if (string.IsNullOrWhiteSpace(f.Avantages)) return OpResult.Echec("Décrivez les avantages de la bourse.");

        var erreurLien = ValiderLien(f.Lien, u, f.EstBourse);
        if (erreurLien != null) return OpResult.Echec(erreurLien);
        if (string.IsNullOrEmpty(f.ArreteSource)) return OpResult.Echec("Joignez l'arrêté officiel signé (PDF).");

        var db = await dbs.GetAsync();
        var intitule = f.Intitule.Trim();

        // Anti-doublon
        if (!f.EstBourse && (await db.Table<Concours>().ToListAsync()).Any(c =>
                c.ProprietaireId == u.Id && Fmt.Normaliser(c.Intitule) == Fmt.Normaliser(intitule) && c.DateCloture.Year == f.DateCloture.Year && c.Statut != StatutPublication.Rejete))
            return OpResult.Echec("Une annonce identique existe déjà pour cette année.");

        var (chemin, erreurFichier) = await FichierService.CopierPdfAsync(f.ArreteSource, "arretes", 8);
        if (chemin == null) return OpResult.Echec(erreurFichier!);

        // 1) Envoi au backend (le serveur valide et modère)
        var base64 = await FichierService.LireBase64Async(chemin);
        if (base64 != null)
        {
            var distant = await api.PostAsync<OperationApi>("/api/publications", new PublicationApiRequest(
                u.Id, f.EstBourse, intitule, f.DateCloture, f.DateEpreuves, f.Places, f.FraisFcfa,
                f.Lien.Trim(), base64, f.Reference, f.Portee, f.Avantages, f.NiveauCible));
            if (distant != null) return new OpResult(distant.Succes, distant.Erreur);
        }

        // 2) Repli local hors ligne
        await InsererLocalAsync(u, f, intitule, chemin);
        await ValiderEnAttenteLocalAsync();
        return OpResult.Ok();
    }

    async Task InsererLocalAsync(Utilisateur u, PublicationForm f, string intitule, string chemin)
    {
        var db = await dbs.GetAsync();
        if (f.EstBourse)
        {
            await db.InsertAsync(new Bourse
            {
                Titre = intitule, Organisme = u.NomEtablissement, Zone = $"{u.NomEtablissement.ToUpperInvariant()}", Categorie = "Excellence",
                Portee = f.Portee, Avantages = f.Avantages.Trim(), NiveauCible = f.NiveauCible, DateCloture = f.DateCloture.Date,
                LienOfficiel = f.Lien.Trim(), ArretePath = chemin, ProprietaireId = u.Id, Statut = StatutPublication.EnAttente,
                DateSoumission = DateTime.Now
            });
        }
        else
        {
            var concours = new Concours
            {
                Etablissement = u.NomEtablissement, Sigle = Sigle(u.NomEtablissement), Intitule = intitule, Categorie = "Autres",
                Tutelle = u.Tutelle.Split('—')[0].Trim(), DateCloture = f.DateCloture.Date, DateEpreuves = f.DateEpreuves.Date,
                Places = f.Places, FraisFcfa = f.FraisFcfa, LienOfficiel = f.Lien.Trim(), ArretePath = chemin,
                ReferenceArrete = string.IsNullOrWhiteSpace(f.Reference) ? "Arrêté joint" : f.Reference.Trim(), DateArrete = DateTime.Today,
                ProprietaireId = u.Id, Statut = StatutPublication.EnAttente, DateSoumission = DateTime.Now
            };
            await db.InsertAsync(concours);
            // Checklist modèle par défaut : l'institution pourra l'ajuster via le back-office
            await db.InsertAllAsync(new[]
            {
                new PieceRequise { ConcoursId = concours.Id, Ordre = 1, Libelle = "Acte de naissance certifié conforme", Detail = "Légalisé < 3 mois", Icone = "🪪" },
                new PieceRequise { ConcoursId = concours.Id, Ordre = 2, Libelle = "Diplôme ou relevé de notes certifié", Detail = "Copie certifiée conforme", Icone = "🎓" },
                new PieceRequise { ConcoursId = concours.Id, Ordre = 3, Libelle = "Quittance originale des frais", Detail = $"Reçu imprimé — {Fmt.Fcfa(f.FraisFcfa)}", Icone = "🧾" },
                new PieceRequise { ConcoursId = concours.Id, Ordre = 4, Libelle = "4 photos d'identité 4×4", Detail = "Fond blanc récent", Icone = "📷" },
            });
        }
    }

    /// <summary>Déclenche la modération back-office (serveur) ; repli local hors ligne.</summary>
    public async Task ValiderEnAttenteAsync()
    {
        var distant = await api.PostAsync<ModerationApiResponse>("/api/publications/valider");
        if (distant != null) return;

        await ValiderEnAttenteLocalAsync();
    }

    /// <summary>Simule le back-office hors ligne : valide accréditations et annonces après le délai de modération.</summary>
    async Task ValiderEnAttenteLocalAsync()
    {
        var db = await dbs.GetAsync();
        var limite = DateTime.Now - DelaiModeration;

        foreach (var u in (await db.Table<Utilisateur>().ToListAsync()).Where(u => u.Role == RoleUtilisateur.Institution && u.Accreditation == StatutPublication.EnAttente && u.DateCreation <= limite))
        {
            u.Accreditation = StatutPublication.Valide; u.Verifie = true;
            await db.UpdateAsync(u);
        }
        foreach (var c in (await db.Table<Concours>().ToListAsync()).Where(c => c.Statut == StatutPublication.EnAttente && c.DateSoumission <= limite))
        {
            c.Statut = StatutPublication.Valide; c.DatePublication = DateTime.Now;
            await db.UpdateAsync(c);
            await alertes.NotifierNouvelleAnnonceAsync(c);
        }
        foreach (var b in (await db.Table<Bourse>().ToListAsync()).Where(b => b.Statut == StatutPublication.EnAttente && b.DateSoumission <= limite))
        {
            b.Statut = StatutPublication.Valide; b.DatePublication = DateTime.Now;
            await db.UpdateAsync(b);
            await alertes.NotifierNouvelleBourseAsync(b);
        }
    }

    public async Task<List<(string titre, string type, StatutPublication statut, DateTime date)>> MesAnnoncesAsync(int userId)
    {
        var distant = await api.GetAsync<List<AnnonceApi>>($"/api/utilisateurs/{userId}/annonces");
        if (distant != null)
            return distant.Select(a => (a.Titre, a.Type, a.Statut, a.Date)).ToList();

        var db = await dbs.GetAsync();
        var l = new List<(string, string, StatutPublication, DateTime)>();
        l.AddRange((await db.Table<Concours>().ToListAsync()).Where(c => c.ProprietaireId == userId).Select(c => (c.Intitule, "Concours", c.Statut, c.DateSoumission)));
        l.AddRange((await db.Table<Bourse>().ToListAsync()).Where(b => b.ProprietaireId == userId).Select(b => (b.Titre, "Bourse", b.Statut, b.DateSoumission)));
        return l.OrderByDescending(x => x.Item4).ToList();
    }

    // ---------- Règles anti-fraude sur le lien de candidature
    static string? ValiderLien(string? lien, Utilisateur u, bool bourse)
    {
        if (!Uri.TryCreate((lien ?? "").Trim(), UriKind.Absolute, out var uri)) return "Le lien de candidature n'est pas une adresse valide.";
        if (uri.Scheme != Uri.UriSchemeHttps) return "Le lien doit utiliser HTTPS (connexion sécurisée).";
        var hote = uri.Host.ToLowerInvariant();
        if (Uri.CheckHostName(hote) is UriHostNameType.IPv4 or UriHostNameType.IPv6) return "Les adresses IP ne sont pas acceptées : utilisez un nom de domaine officiel.";
        if (Raccourcisseurs.Any(r => hote == r || hote.EndsWith("." + r))) return "Les liens raccourcis ou réseaux sociaux ne sont pas acceptés.";

        var domaineInstitution = Validation.DomaineDe(u.EmailInstitutionnel);
        var officiel = hote.EndsWith(".cm")
            || (!string.IsNullOrEmpty(domaineInstitution) && (hote == domaineInstitution || hote.EndsWith("." + domaineInstitution)))
            || (bourse && HotesBoursesFiables.Any(h => hote == h || hote.EndsWith("." + h)));
        return officiel ? null : "Le lien doit pointer vers un portail officiel (domaine .cm ou celui de votre e-mail institutionnel).";
    }

    static string Sigle(string nom)
    {
        var mots = nom.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(m => m.Length > 3 || m.All(char.IsUpper)).ToList();
        if (nom.Length <= 8 && !nom.Contains(' ')) return nom.ToUpperInvariant();
        var s = string.Concat(mots.Select(m => char.ToUpperInvariant(m[0])));
        return s.Length >= 2 ? s : nom[..Math.Min(5, nom.Length)].ToUpperInvariant();
    }
}
