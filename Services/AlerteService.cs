using Plugin.LocalNotification;

namespace ConcoursCM.Services;

/// <summary>
/// Rappels intelligents : pour chaque concours suivi ou mis en favori, une alerte est générée
/// au seuil le plus proche atteint (J-7 / J-3 / J-1) et des notifications locales sont planifiées
/// (elles fonctionnent hors connexion). Les doublons sont évités par une clé unique.
/// </summary>
public class AlerteService(DatabaseService dbs, SettingsService settings, ISmsGateway sms, ApiClient api)
{
    // Compteur de la cloche : appelé à chaque changement d'onglet. On interroge la route légère
    // du serveur (un simple entier) au lieu de re-synchroniser toute la table alertes.
    int _nbNonLusCache = -1;
    DateTime _nbNonLusMaj;

    // ---------- Lecture
    public async Task<List<Alerte>> ListerAsync(int userId)
    {
        var distant = await api.GetAsync<List<Alerte>>($"/api/utilisateurs/{userId}/alertes");
        if (distant != null) await RemplacerAlertesAsync(userId, distant);
        InvaliderCompteur();

        var db = await dbs.GetAsync();
        return (await db.Table<Alerte>().ToListAsync()).Where(a => a.UtilisateurId == userId)
            .OrderByDescending(a => a.DateCreation).ToList();
    }

    public async Task<int> NbNonLusAsync(int userId)
    {
        if (_nbNonLusCache >= 0 && DateTime.UtcNow - _nbNonLusMaj < TimeSpan.FromSeconds(30))
            return _nbNonLusCache;

        var distant = await api.GetAsync<int?>($"/api/utilisateurs/{userId}/alertes/non-lus");
        int n;
        if (distant.HasValue) n = distant.Value;
        else
        {
            var db = await dbs.GetAsync();
            n = (await db.Table<Alerte>().ToListAsync()).Count(a => a.UtilisateurId == userId && !a.Lu);
        }
        _nbNonLusCache = n;
        _nbNonLusMaj = DateTime.UtcNow;
        return n;
    }

    void InvaliderCompteur() { _nbNonLusCache = -1; }

    public async Task MarquerLuAsync(Alerte a)
    {
        if (a.Lu) return;
        var db = await dbs.GetAsync();
        a.Lu = true;
        await db.UpdateAsync(a);
        InvaliderCompteur();
        _ = api.PutAsync<Alerte>($"/api/alertes/{a.Id}/lu");
    }

    public async Task MarquerToutLuAsync(int userId)
    {
        var db = await dbs.GetAsync();
        var nonLus = (await db.Table<Alerte>().ToListAsync())
            .Where(a => a.UtilisateurId == userId && !a.Lu).ToList();
        foreach (var a in nonLus) a.Lu = true;
        if (nonLus.Count > 0) await db.UpdateAllAsync(nonLus);
        InvaliderCompteur();
        _ = api.PutAsync<OperationApi>($"/api/utilisateurs/{userId}/alertes/tout-lu");
    }

    // ---------- Génération
    public async Task SynchroniserAsync(Utilisateur u)
    {
        if (u.Role != RoleUtilisateur.Candidat) return;

        var distant = await api.PostAsync<List<Alerte>>($"/api/utilisateurs/{u.Id}/alertes/synchroniser",
            new SeuilsApiRequest(settings.Seuils().ToArray()));
        if (distant != null)
        {
            await RemplacerAlertesAsync(u.Id, distant);
            await PlanifierRappelsAsync(u);
            return;
        }

        await SynchroniserLocalAsync(u);
    }

    /// <summary>Génération locale de secours (identique à l'ancien comportement hors ligne).</summary>
    async Task SynchroniserLocalAsync(Utilisateur u)
    {
        var db = await dbs.GetAsync();
        var seuils = settings.Seuils(); // ordre croissant : [1,3,7]

        var checklist = (await db.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == u.Id).ToList();
        var favoris = (await db.Table<Favori>().ToListAsync()).Where(f => f.UtilisateurId == u.Id).ToList();

        // --- Concours suivis ou favoris
        var idsConcours = checklist.Select(i => i.ConcoursId)
            .Concat(favoris.Where(f => f.Cible == TypeCible.Concours).Select(f => f.CibleId)).ToHashSet();
        foreach (var c in (await db.Table<Concours>().ToListAsync()).Where(c => idsConcours.Contains(c.Id) && c.Statut == StatutPublication.Valide && !c.EstClos))
        {
            var items = checklist.Where(i => i.ConcoursId == c.Id).ToList();
            var restantes = items.Count(i => !i.Coche);
            var pied = items.Count == 0 ? "Commencez votre checklist"
                : restantes == 0 ? "✓ Dossier complet" : $"Il reste {restantes} pièce(s)";

            foreach (var t in seuils)
                await PlanifierAsync($"rappel-c{c.Id}-u{u.Id}-J{t}", $"Clôture {c.Sigle} dans {t} jour(s)",
                    $"{c.Etablissement} — dépôt avant le {Fmt.Date(c.DateCloture)} à {c.HeureCloture}.", c.DateCloture.AddDays(-t).AddHours(8));

            var atteint = seuils.Where(t => c.JoursRestants <= t).Cast<int?>().FirstOrDefault();
            if (atteint is int s)
            {
                var jours = Math.Max(c.JoursRestants, 0);
                var msg = $"Date limite le {Fmt.Date(c.DateCloture)} à {c.HeureCloture.Replace(':', 'h')} — {c.LieuDepot}. " +
                          (restantes == 0 && items.Count > 0 ? "Votre dossier est prêt : pensez à le déposer." : "Vérifiez vos pièces physiques certifiées.");
                await CreerSiAbsentAsync(new Alerte
                {
                    UtilisateurId = u.Id, Cle = $"rappel-c{c.Id}-u{u.Id}-J{s}", Type = TypeAlerte.Rappel,
                    Badge = jours == 0 ? "URGENT • DERNIER JOUR" : $"URGENT • J-{jours}", Urgent = jours <= 3,
                    Titre = $"Clôture dépôt {c.Sigle}", Message = msg,
                    PiedTexte = jours <= 3 ? $"Reste {Math.Max(jours * 24, 0)}h" : pied, ActionLibelle = "Checklist pièces",
                    Cible = TypeCible.Concours, CibleId = c.Id
                }, u);
            }
        }

        // --- Bourses en favori
        var idsBourses = favoris.Where(f => f.Cible == TypeCible.Bourse).Select(f => f.CibleId).ToHashSet();
        foreach (var b in (await db.Table<Bourse>().ToListAsync()).Where(b => idsBourses.Contains(b.Id) && b.Statut == StatutPublication.Valide && !b.EstClose))
        {
            foreach (var t in seuils)
                await PlanifierAsync($"rappel-b{b.Id}-u{u.Id}-J{t}", $"Bourse : clôture dans {t} jour(s)", b.Titre, b.DateCloture.AddDays(-t).AddHours(8));

            var atteint = seuils.Where(t => b.JoursRestants <= t).Cast<int?>().FirstOrDefault();
            if (atteint is int s)
                await CreerSiAbsentAsync(new Alerte
                {
                    UtilisateurId = u.Id, Cle = $"rappel-b{b.Id}-u{u.Id}-J{s}", Type = TypeAlerte.Bourse,
                    Badge = $"BOURSE • J-{Math.Max(b.JoursRestants, 0)}", Urgent = b.JoursRestants <= 3,
                    Titre = b.Titre, Message = $"Clôture le {Fmt.Date(b.DateCloture)}. Préparez vos lettres de recommandation et pièces.",
                    PiedTexte = b.Organisme, ActionLibelle = "Détails bourse", Cible = TypeCible.Bourse, CibleId = b.Id
                }, u);
        }
    }

    /// <summary>Alerte « arrêté officiel » envoyée à tous les candidats quand une annonce est validée.</summary>
    public async Task NotifierNouvelleAnnonceAsync(Concours c)
    {
        var db = await dbs.GetAsync();
        foreach (var u in (await db.Table<Utilisateur>().ToListAsync()).Where(u => u.Role == RoleUtilisateur.Candidat))
            await CreerSiAbsentAsync(new Alerte
            {
                UtilisateurId = u.Id, Cle = $"annonce-c{c.Id}-u{u.Id}", Type = TypeAlerte.ArreteOfficiel, Badge = "ARRÊTÉ OFFICIEL",
                Titre = $"Concours {c.Sigle} {c.DateCloture.Year}",
                Message = $"{c.Etablissement} publie : {c.Intitule}. {c.Places} places ouvertes. Clôture le {Fmt.Date(c.DateCloture)}.",
                PiedTexte = string.IsNullOrEmpty(c.ReferenceArrete) ? c.Tutelle : c.ReferenceArrete, ActionLibelle = "Consulter l'avis",
                Cible = TypeCible.Concours, CibleId = c.Id
            }, u, notifierSms: false);
    }

    public async Task NotifierNouvelleBourseAsync(Bourse b)
    {
        var db = await dbs.GetAsync();
        foreach (var u in (await db.Table<Utilisateur>().ToListAsync()).Where(u => u.Role == RoleUtilisateur.Candidat))
            await CreerSiAbsentAsync(new Alerte
            {
                UtilisateurId = u.Id, Cle = $"annonce-b{b.Id}-u{u.Id}", Type = TypeAlerte.Bourse, Badge = $"BOURSE {b.Categorie.ToUpperInvariant()}",
                Titre = b.Titre, Message = $"{b.Avantages}. Clôture le {Fmt.Date(b.DateCloture)}.",
                PiedTexte = b.Organisme, ActionLibelle = "Détails bourse", Cible = TypeCible.Bourse, CibleId = b.Id
            }, u, notifierSms: false);
    }

    /// <summary>Alerte « service » affichée quand le candidat active les rappels sur un numéro.</summary>
    public async Task NotifierServiceSmsAsync(Utilisateur u)
    {
        if (string.IsNullOrEmpty(u.NumeroSms)) return;

        var distant = await api.PostAsync<Alerte>($"/api/utilisateurs/{u.Id}/alertes/service-sms",
            new ServiceSmsApiRequest(u.NumeroSms));
        if (distant != null)
        {
            var db = await dbs.GetAsync();
            await db.InsertOrReplaceAsync(distant);
            return;
        }

        var local = await dbs.GetAsync();
        var n = u.NumeroSms.Replace("+237", "");
        var masque = n.Length == 9 ? $"+237 {n[0]}•• •• •• {n[^2..]}" : u.NumeroSms;
        var op = Validation.Operateur(u.NumeroSms);
        var alerte = new Alerte
        {
            UtilisateurId = u.Id, Cle = $"service-sms-u{u.Id}-{u.NumeroSms}", Type = TypeAlerte.Service, Badge = "SERVICE RAPPELS ACTIF",
            Titre = "Rappels automatiques activés",
            Message = $"Rappels avant chaque clôture pour le numéro {masque}, ainsi que des notifications sur ce téléphone.",
            PiedTexte = string.IsNullOrEmpty(op) ? "Opérateur MTN / Orange" : $"Opérateur {op}", ActionLibelle = "Modifier n°",
            Cible = TypeCible.Concours, CibleId = 0
        };
        if ((await local.Table<Alerte>().ToListAsync()).Any(x => x.Cle == alerte.Cle)) return;
        await local.InsertAsync(alerte);
    }

    // ---------- Notifications locales
    public async Task<bool> ActiverNotificationsAsync()
    {
        try
        {
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled()) return true;
            return await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
        catch { return false; }
    }

    /// <summary>Planifie les notifications locales à partir des données de l'appareil (mode connecté).</summary>
    async Task PlanifierRappelsAsync(Utilisateur u)
    {
        var db = await dbs.GetAsync();
        var seuils = settings.Seuils();
        if (seuils.Count == 0) return;

        var checklist = (await db.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == u.Id).ToList();
        var favoris = (await db.Table<Favori>().ToListAsync()).Where(f => f.UtilisateurId == u.Id).ToList();

        var idsConcours = checklist.Select(i => i.ConcoursId)
            .Concat(favoris.Where(f => f.Cible == TypeCible.Concours).Select(f => f.CibleId)).ToHashSet();
        foreach (var c in (await db.Table<Concours>().ToListAsync()).Where(c => idsConcours.Contains(c.Id) && c.Statut == StatutPublication.Valide && !c.EstClos))
            foreach (var t in seuils)
                await PlanifierAsync($"rappel-c{c.Id}-u{u.Id}-J{t}", $"Clôture {c.Sigle} dans {t} jour(s)",
                    $"{c.Etablissement} — dépôt avant le {Fmt.Date(c.DateCloture)} à {c.HeureCloture}.", c.DateCloture.AddDays(-t).AddHours(8));

        var idsBourses = favoris.Where(f => f.Cible == TypeCible.Bourse).Select(f => f.CibleId).ToHashSet();
        foreach (var b in (await db.Table<Bourse>().ToListAsync()).Where(b => idsBourses.Contains(b.Id) && b.Statut == StatutPublication.Valide && !b.EstClose))
            foreach (var t in seuils)
                await PlanifierAsync($"rappel-b{b.Id}-u{u.Id}-J{t}", $"Bourse : clôture dans {t} jour(s)", b.Titre, b.DateCloture.AddDays(-t).AddHours(8));
    }

    async Task PlanifierAsync(string cle, string titre, string description, DateTime quand)
    {
        if (quand <= DateTime.Now) return;
        try
        {
            await LocalNotificationCenter.Current.Show(new NotificationRequest
            {
                NotificationId = IdStable(cle), Title = titre, Description = description,
                Schedule = new NotificationRequestSchedule { NotifyTime = quand }
            });
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    async Task CreerSiAbsentAsync(Alerte a, Utilisateur u, bool notifierSms = true)
    {
        var db = await dbs.GetAsync();
        if ((await db.Table<Alerte>().ToListAsync()).Any(x => x.Cle == a.Cle)) return;
        await db.InsertAsync(a);
        if (notifierSms && u.SmsActif && !string.IsNullOrEmpty(u.NumeroSms))
            _ = sms.EnvoyerAsync(u.NumeroSms, $"ConcoursCM : {a.Titre} — {a.Message}");
    }

    async Task RemplacerAlertesAsync(int userId, List<Alerte> distant)
    {
        var db = await dbs.GetAsync();
        var locales = (await db.Table<Alerte>().ToListAsync()).Where(a => a.UtilisateurId == userId).ToList();
        // Une seule transaction : 2 appels disque au lieu de 2 × N.
        await db.RunInTransactionAsync(conn =>
        {
            foreach (var a in locales) conn.Delete(a);
            foreach (var a in distant) conn.InsertOrReplace(a);
        });
    }

    /// <summary>Hash déterministe (string.GetHashCode varie à chaque lancement).</summary>
    static int IdStable(string s)
    {
        unchecked
        {
            uint h = 2166136261;
            foreach (var c in s) h = (h ^ c) * 16777619;
            return (int)(h & 0x7FFFFFFF);
        }
    }
}
