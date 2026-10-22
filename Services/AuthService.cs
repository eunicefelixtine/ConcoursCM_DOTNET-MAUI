namespace ConcoursCM.Services;

public class AuthService(DatabaseService dbs, ApiClient api)
{
    const string SessionKey = "session_user_id";
    const int MaxEchecs = 5;

    public Utilisateur? Current { get; private set; }
    public bool EstConnecte => Current != null;

    public async Task<bool> RestaurerSessionAsync()
    {
        var id = Preferences.Get(SessionKey, 0);
        if (id == 0) return false;

        // Le serveur fait foi ; hors ligne, la session locale prend le relais.
        var distant = await api.GetAsync<Utilisateur>($"/api/utilisateurs/{id}");
        if (distant != null)
        {
            Current = await CacherAsync(distant);
            return true;
        }

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

        // 1) Authentification serveur
        var distant = await api.PostAsync<AuthApiResponse>("/api/auth/connexion",
            new ConnexionApiRequest(id, motDePasse, roleAttendu));
        if (distant is { Succes: true, Utilisateur: { } du })
        {
            Preferences.Set("echecs", 0);
            ApiClient.DefinirJeton(distant.Jeton);
            OuvrirSession(await CacherAsync(du));
            return OpResult.Ok();
        }

        // 2) Repli local (comptes créés hors ligne ou serveur indisponible)
        var db = await dbs.GetAsync();
        var user = (await db.Table<Utilisateur>().ToListAsync()).FirstOrDefault(u => u.Identifiant == id);
        var localOk = user != null && PasswordHasher.Verifier(motDePasse, user.MotDePasseHash, user.Sel);

        if (localOk && user!.Role == roleAttendu)
        {
            Preferences.Set("echecs", 0);
            OuvrirSession(user);
            return OpResult.Ok();
        }

        if (localOk && user!.Role != roleAttendu)
            return OpResult.Echec(user.Role == RoleUtilisateur.Institution
                ? "Ce compte est un compte Institution : choisissez l'onglet « École / Inst. »."
                : "Ce compte est un compte Candidat : choisissez l'onglet « Candidat ».");

        if (distant is { Succes: false })
            return OpResult.Echec(distant.Erreur ?? "Identifiant ou mot de passe incorrect.");

        var echecs = Preferences.Get("echecs", 0) + 1;
        if (echecs >= MaxEchecs)
        {
            Preferences.Set("lock_until", DateTime.UtcNow.AddMinutes(5).Ticks);
            echecs = 0;
        }
        Preferences.Set("echecs", echecs);
        return OpResult.Echec("Identifiant ou mot de passe incorrect."); // message volontairement générique
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

        var distant = await api.PostAsync<AuthApiResponse>("/api/auth/inscription/candidat",
            new CandidatApiRequest(nom.Trim(), id, region, motDePasse, conditions));
        if (distant != null)
        {
            if (!distant.Succes || distant.Utilisateur == null)
                return OpResult.Echec(distant.Erreur ?? "Inscription impossible.");
            ApiClient.DefinirJeton(distant.Jeton);
            OuvrirSession(await CacherAsync(distant.Utilisateur));
            return OpResult.Ok();
        }

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

        var (chemin, erreurFichier) = await FichierService.CopierPdfAsync(mandatSource, "mandats", 5);
        if (chemin == null) return OpResult.Echec(erreurFichier!);

        var base64 = await FichierService.LireBase64Async(chemin);
        if (base64 != null)
        {
            var distant = await api.PostAsync<AuthApiResponse>("/api/auth/inscription/institution",
                new InstitutionApiRequest(etablissement.Trim(), tutelle, representant.Trim(), mail, tel, motDePasse, base64, certifie));
            if (distant != null)
            {
                if (!distant.Succes || distant.Utilisateur == null)
                    return OpResult.Echec(distant.Erreur ?? "Inscription impossible.");
                distant.Utilisateur.MandatPath = chemin;
                ApiClient.DefinirJeton(distant.Jeton);
                OuvrirSession(await CacherAsync(distant.Utilisateur));
                return OpResult.Ok();
            }
        }

        var db = await dbs.GetAsync();
        if ((await db.Table<Utilisateur>().ToListAsync()).Any(u => u.Identifiant == mail))
            return OpResult.Echec("Un compte existe déjà avec cet e-mail.");

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

        // Le mot de passe (hash + sel) et les chemins de fichiers locaux ne quittent jamais l'appareil.
        var profil = new
        {
            u.NomComplet, u.Region, u.Niveau, u.AnneeObtention, u.SmsActif, u.NumeroSms,
            u.NomEtablissement, u.Tutelle, u.Representant, u.EmailInstitutionnel, u.TelephoneOfficiel
        };
        var distant = await api.PutAsync<Utilisateur>($"/api/utilisateurs/{u.Id}", profil);
        Current = distant != null ? await CacherAsync(distant) : u;
    }

    public async Task RafraichirAsync()
    {
        if (Current == null) return;

        var distant = await api.GetAsync<Utilisateur>($"/api/utilisateurs/{Current.Id}");
        if (distant != null)
        {
            Current = await CacherAsync(distant);
            return;
        }

        var db = await dbs.GetAsync();
        Current = await db.FindAsync<Utilisateur>(Current.Id) ?? Current;
    }

    public void Deconnexion()
    {
        _ = api.PostAsync<OperationApi>("/api/auth/deconnexion");
        ApiClient.DefinirJeton(null);
        Preferences.Remove(SessionKey);
        Current = null;
    }

    void OuvrirSession(Utilisateur u)
    {
        Current = u;
        Preferences.Set(SessionKey, u.Id);
    }

    /// <summary>Recopie l'utilisateur serveur en base locale en préservant les secrets locaux.</summary>
    async Task<Utilisateur> CacherAsync(Utilisateur distant)
    {
        var db = await dbs.GetAsync();
        var local = await db.FindAsync<Utilisateur>(distant.Id)
            ?? (await db.Table<Utilisateur>().ToListAsync()).FirstOrDefault(u => u.Identifiant == distant.Identifiant);

        if (local != null)
        {
            if (local.Id != distant.Id) await db.DeleteAsync(local);
            if (string.IsNullOrEmpty(distant.MotDePasseHash) && !string.IsNullOrEmpty(local.MotDePasseHash))
            {
                distant.MotDePasseHash = local.MotDePasseHash;
                distant.Sel = local.Sel;
            }
            if ((string.IsNullOrEmpty(distant.MandatPath) || !File.Exists(distant.MandatPath))
                && !string.IsNullOrEmpty(local.MandatPath) && File.Exists(local.MandatPath))
                distant.MandatPath = local.MandatPath;
        }

        await db.InsertOrReplaceAsync(distant);
        return distant;
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

    /// <summary>Lit un PDF local et le encode en Base64 pour l'envoi au serveur.</summary>
    public static async Task<string?> LireBase64Async(string? chemin)
    {
        try
        {
            if (string.IsNullOrEmpty(chemin) || !File.Exists(chemin)) return null;
            var octets = await File.ReadAllBytesAsync(chemin);
            return octets.Length == 0 ? null : Convert.ToBase64String(octets);
        }
        catch { return null; }
    }
}
