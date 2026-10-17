namespace ConcoursCM.Services;

public class AuthService(DatabaseService dbs)
{
    const string SessionKey = "session_user_id";
    const int MaxEchecs = 5;

    public Utilisateur? Current { get; private set; }
    public bool EstConnecte => Current != null;

    public async Task<bool> RestaurerSessionAsync()
    {
        var id = Preferences.Get(SessionKey, 0);
        if (id == 0) return false;
        var db = await dbs.GetAsync();
        Current = await db.FindAsync<Utilisateur>(id);
        return Current != null;
    }

    public async Task<OpResult> ConnexionAsync(string? identifiant, string? motDePasse, RoleUtilisateur roleAttendu)
    {
        // Verrouillage temporaire après 5 échecs (protège contre le brute-force local)
        var verrou = Preferences.Get("lock_until", 0L);
        if (verrou > DateTime.UtcNow.Ticks)
        {
            var min = (int)Math.Ceiling(TimeSpan.FromTicks(verrou - DateTime.UtcNow.Ticks).TotalMinutes);
            return OpResult.Echec($"Trop de tentatives. Réessayez dans {min} min.");
        }

        if (!Validation.TryNormaliserIdentifiant(identifiant, out var id, out _))
            return OpResult.Echec("Identifiant invalide : saisissez un e-mail ou un numéro camerounais (6XX XXX XXX).");
        if (string.IsNullOrEmpty(motDePasse))
            return OpResult.Echec("Saisissez votre mot de passe.");

        var db = await dbs.GetAsync();
        var user = (await db.Table<Utilisateur>().ToListAsync()).FirstOrDefault(u => u.Identifiant == id);

        if (user == null || !PasswordHasher.Verifier(motDePasse, user.MotDePasseHash, user.Sel))
        {
            var echecs = Preferences.Get("echecs", 0) + 1;
            if (echecs >= MaxEchecs)
            {
                Preferences.Set("lock_until", DateTime.UtcNow.AddMinutes(5).Ticks);
                echecs = 0;
            }
            Preferences.Set("echecs", echecs);
            return OpResult.Echec("Identifiant ou mot de passe incorrect."); // message volontairement générique
        }

        if (user.Role != roleAttendu)
            return OpResult.Echec(user.Role == RoleUtilisateur.Institution
                ? "Ce compte est un compte Institution : choisissez l'onglet « École / Inst. »."
                : "Ce compte est un compte Candidat : choisissez l'onglet « Candidat ».");

        Preferences.Set("echecs", 0);
        OuvrirSession(user);
        return OpResult.Ok();
    }

    public async Task<OpResult> InscrireCandidatAsync(string? nom, string? identifiant, string? region, string? motDePasse, bool conditions)
    {
        if (string.IsNullOrWhiteSpace(nom) || nom.Trim().Length < 3) return OpResult.Echec("Saisissez votre nom complet.");
        if (!Validation.TryNormaliserIdentifiant(identifiant, out var id, out var estTel))
            return OpResult.Echec("E-mail ou téléphone invalide (ex. 6XX XX XX XX).");
        if (string.IsNullOrEmpty(region)) return OpResult.Echec("Sélectionnez votre région de résidence.");
        var errMdp = Validation.ErreurMotDePasse(motDePasse, 6, false);
        if (errMdp != null) return OpResult.Echec(errMdp);
        if (!conditions) return OpResult.Echec("Vous devez accepter les conditions d'utilisation.");

        var db = await dbs.GetAsync();
        if ((await db.Table<Utilisateur>().ToListAsync()).Any(u => u.Identifiant == id))
            return OpResult.Echec("Un compte existe déjà avec cet identifiant.");

        var (hash, sel) = PasswordHasher.Hacher(motDePasse!);
        var user = new Utilisateur
        {
            Identifiant = id, NomComplet = nom.Trim(), MotDePasseHash = hash, Sel = sel,
            Role = RoleUtilisateur.Candidat, Region = region, SmsActif = estTel, NumeroSms = estTel ? id : "",
            Verifie = false
        };
        await db.InsertAsync(user);
        OuvrirSession(user);
        return OpResult.Ok();
    }

    public async Task<OpResult> InscrireInstitutionAsync(string? etablissement, string? tutelle, string? representant,
        string? email, string? telephone, string? motDePasse, string? mandatSource, bool certifie)
    {
        if (string.IsNullOrWhiteSpace(etablissement) || etablissement.Trim().Length < 3) return OpResult.Echec("Saisissez le nom officiel de l'établissement.");
        if (string.IsNullOrEmpty(tutelle)) return OpResult.Echec("Sélectionnez la tutelle ministérielle.");
        if (string.IsNullOrWhiteSpace(representant) || representant.Trim().Length < 5) return OpResult.Echec("Indiquez le représentant habilité (nom et fonction).");
        if (!Validation.TryNormaliserIdentifiant(email, out var mail, out var estTel) || estTel)
            return OpResult.Echec("Saisissez un e-mail institutionnel valide.");
        if (Validation.EstEmailGrandPublic(mail))
            return OpResult.Echec("Utilisez une adresse e-mail institutionnelle (pas Gmail, Yahoo, Hotmail…).");
        if (!Validation.TryNormaliserIdentifiant(telephone, out var tel, out var okTel) || !okTel)
            return OpResult.Echec("Téléphone officiel invalide (+237 6XX XX XX XX).");
        var errMdp = Validation.ErreurMotDePasse(motDePasse, 8, true);
        if (errMdp != null) return OpResult.Echec(errMdp);
        if (string.IsNullOrEmpty(mandatSource)) return OpResult.Echec("Joignez l'arrêté de nomination ou le mandat (PDF).");
        if (!certifie) return OpResult.Echec("Vous devez certifier être mandaté par l'établissement.");

        var db = await dbs.GetAsync();
        if ((await db.Table<Utilisateur>().ToListAsync()).Any(u => u.Identifiant == mail))
            return OpResult.Echec("Un compte existe déjà avec cet e-mail.");

        var (chemin, erreurFichier) = await FichierService.CopierPdfAsync(mandatSource, "mandats", 5);
        if (chemin == null) return OpResult.Echec(erreurFichier!);

        var (hash, sel) = PasswordHasher.Hacher(motDePasse!);
        var user = new Utilisateur
        {
            Identifiant = mail, NomComplet = representant.Trim(), MotDePasseHash = hash, Sel = sel,
            Role = RoleUtilisateur.Institution, NomEtablissement = etablissement.Trim(), Tutelle = tutelle,
            Representant = representant.Trim(), EmailInstitutionnel = mail, TelephoneOfficiel = tel,
            MandatPath = chemin,
            Accreditation = StatutPublication.EnAttente, Region = "", SmsActif = false
        };
        await db.InsertAsync(user);
        OuvrirSession(user);
        return OpResult.Ok();
    }

    public async Task MettreAJourAsync(Utilisateur u)
    {
        var db = await dbs.GetAsync();
        await db.UpdateAsync(u);
        Current = u;
    }

    public async Task RafraichirAsync()
    {
        if (Current == null) return;
        var db = await dbs.GetAsync();
        Current = await db.FindAsync<Utilisateur>(Current.Id) ?? Current;
    }

    public void Deconnexion()
    {
        Preferences.Remove(SessionKey);
        Current = null;
    }

    void OuvrirSession(Utilisateur u)
    {
        Current = u;
        Preferences.Set(SessionKey, u.Id);
    }
}

/// <summary>Copie sécurisée de PDF choisis par l'utilisateur dans le stockage privé de l'app.</summary>
public static class FichierService
{
    /// <summary>Retourne (chemin de la copie, null) en cas de succès, sinon (null, message d'erreur).</summary>
    public static async Task<(string? chemin, string? erreur)> CopierPdfAsync(string source, string sousDossier, int tailleMaxMo)
    {
        try
        {
            if (!source.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return (null, "Le document doit être au format PDF.");
            var info = new FileInfo(source);
            if (!info.Exists) return (null, "Le fichier sélectionné est introuvable.");
            if (info.Length > tailleMaxMo * 1024L * 1024L)
                return (null, $"Le PDF dépasse {tailleMaxMo} Mo.");

            var dossier = Path.Combine(FileSystem.AppDataDirectory, sousDossier);
            Directory.CreateDirectory(dossier);
            var dest = Path.Combine(dossier, $"{Guid.NewGuid():N}.pdf");
            await using var src = File.OpenRead(source);
            await using var dst = File.Create(dest);
            await src.CopyToAsync(dst);
            return (dest, null);
        }
        catch (Exception ex)
        {
            return (null, "Impossible de lire le fichier : " + ex.Message);
        }
    }
}
