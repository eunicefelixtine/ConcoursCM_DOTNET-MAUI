namespace ConcoursCM.Services;

public record UneItem(TypeCible Cible, int Id, string Titre, string SousTitre, string Info1, string Info2, DateTime Cloture, int Jours, NiveauUrgence Urgence, string Badge);
public record StatsAccueil(int Ouverts, int Nouveaux, int Bourses);

/// <summary>Lecture du catalogue (concours + bourses) : recherche, filtres, tri, favoris, éligibilité.</summary>
public class ConcoursService(DatabaseService dbs, ApiClient api)
{
    // ---------- Concours
    public async Task<List<string>> CategoriesConcoursAsync() =>
        (await CacheAsync("cats-concours", TimeSpan.FromMinutes(5), async () =>
        {
            var distant = await api.GetAsync<List<string>>("/api/concours/categories");
            if (distant != null) return distant;

            var db = await dbs.GetAsync();
            return (await db.Table<Concours>().ToListAsync())
                .Where(c => c.Statut == StatutPublication.Valide)
                .Select(c => c.Categorie).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
        }))!;

    public async Task<List<Concours>> ListerConcoursAsync(string? texte, string? categorie, bool seulementOuverts,
        bool seulementEligibles, Utilisateur? user, TriConcours tri)
    {
        var chemin = $"/api/concours?texte={Uri.EscapeDataString(texte ?? "")}" +
                     $"&categorie={Uri.EscapeDataString(categorie ?? "")}" +
                     $"&seulementOuverts={(seulementOuverts ? "true" : "false")}" +
                     $"&seulementEligibles={(seulementEligibles ? "true" : "false")}" +
                     $"&tri={(int)tri}" +
                     (user != null ? $"&userId={user.Id}" : "");
        var cle = "concours:" + chemin;
        var distant = await CacheAsync<List<Concours>>(cle, TimeSpan.FromSeconds(30),
            () => api.GetAsync<List<Concours>>(chemin));
        if (distant != null)
        {
            await FusionnerConcoursAsync(distant);
            return distant;
        }

        var db = await dbs.GetAsync();
        IEnumerable<Concours> q = (await db.Table<Concours>().ToListAsync()).Where(c => c.Statut == StatutPublication.Valide);

        if (seulementOuverts) q = q.Where(c => !c.EstClos);
        if (!string.IsNullOrWhiteSpace(categorie) && categorie != "Tous") q = q.Where(c => c.Categorie == categorie);
        if (seulementEligibles && user != null) q = q.Where(c => Evaluer(user, c) != Eligibilite.NonEligible);

        var t = Fmt.Normaliser(texte);
        if (t.Length > 0)
            q = q.Where(c => Fmt.Normaliser($"{c.Etablissement} {c.Sigle} {c.Intitule} {c.Categorie} {c.Tutelle}").Contains(t));

        q = tri switch
        {
            TriConcours.Recents => q.OrderByDescending(c => c.DatePublication),
            TriConcours.Alphabetique => q.OrderBy(c => c.Etablissement),
            _ => q.OrderBy(c => c.EstClos).ThenBy(c => c.DateCloture)
        };
        return q.ToList();
    }

    public async Task<Concours?> ObtenirConcoursAsync(int id)
    {
        var distant = await api.GetAsync<Concours>($"/api/concours/{id}");
        if (distant != null)
        {
            await FusionnerConcoursAsync([distant]);
            InvaliderCache();
            return distant;
        }

        var db = await dbs.GetAsync();
        return await db.FindAsync<Concours>(id);
    }

    public async Task<List<PieceRequise>> PiecesAsync(int concoursId)
    {
        var distant = await api.GetAsync<List<PieceRequise>>($"/api/concours/{concoursId}/pieces");
        if (distant != null)
        {
            var db = await dbs.GetAsync();
            await db.RunInTransactionAsync(conn => { foreach (var p in distant) conn.InsertOrReplace(p); });
            return distant;
        }

        var local = await dbs.GetAsync();
        return (await local.Table<PieceRequise>().ToListAsync()).Where(p => p.ConcoursId == concoursId).OrderBy(p => p.Ordre).ToList();
    }

    // ---------- Bourses
    public async Task<List<string>> CategoriesBoursesAsync() =>
        (await CacheAsync("cats-bourses", TimeSpan.FromMinutes(5), async () =>
        {
            var distant = await api.GetAsync<List<string>>("/api/bourses/categories");
            if (distant != null) return distant;

            var db = await dbs.GetAsync();
            return (await db.Table<Bourse>().ToListAsync())
                .Where(b => b.Statut == StatutPublication.Valide)
                .Select(b => b.Categorie).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
        }))!;

    public async Task<List<Bourse>> ListerBoursesAsync(string? texte, string? categorie, bool seulementOuvertes)
    {
        var chemin = $"/api/bourses?texte={Uri.EscapeDataString(texte ?? "")}" +
                     $"&categorie={Uri.EscapeDataString(categorie ?? "")}" +
                     $"&seulementOuvertes={(seulementOuvertes ? "true" : "false")}";
        var cle = "bourses:" + chemin;
        var distant = await CacheAsync<List<Bourse>>(cle, TimeSpan.FromSeconds(30),
            () => api.GetAsync<List<Bourse>>(chemin));
        if (distant != null)
        {
            await FusionnerBoursesAsync(distant);
            return distant;
        }

        var db = await dbs.GetAsync();
        IEnumerable<Bourse> q = (await db.Table<Bourse>().ToListAsync()).Where(b => b.Statut == StatutPublication.Valide);
        if (seulementOuvertes) q = q.Where(b => !b.EstClose);
        if (!string.IsNullOrWhiteSpace(categorie) && categorie != "Toutes") q = q.Where(b => b.Categorie == categorie);
        var t = Fmt.Normaliser(texte);
        if (t.Length > 0)
            q = q.Where(b => Fmt.Normaliser($"{b.Titre} {b.Organisme} {b.Zone} {b.NiveauCible}").Contains(t));
        return q.OrderBy(b => b.EstClose).ThenBy(b => b.DateCloture).ToList();
    }

    // ---------- Accueil
    public async Task<StatsAccueil> StatsAsync() =>
        (await CacheAsync("stats", TimeSpan.FromSeconds(30), async () =>
        {
            var distant = await api.GetAsync<StatsAccueil>("/api/stats");
            if (distant != null) return distant;

            var db = await dbs.GetAsync();
            var cs = (await db.Table<Concours>().ToListAsync()).Where(c => c.Statut == StatutPublication.Valide && !c.EstClos).ToList();
            var bs = (await db.Table<Bourse>().ToListAsync()).Where(b => b.Statut == StatutPublication.Valide && !b.EstClose).ToList();
            return new StatsAccueil(cs.Count, cs.Count(c => c.EstNouveau), bs.Count);
        }))!;

    /// <summary>« À la Une » : concours et bourses ouverts, clôture dans les 30 jours, les plus proches d'abord.</summary>
    public async Task<List<UneItem>> UnesAsync(int max = 4) =>
        (await CacheAsync($"unes:{max}", TimeSpan.FromSeconds(30), async () =>
        {
            var distant = await api.GetAsync<List<UneItem>>($"/api/unes?max={max}");
            if (distant != null) return distant;

            var db = await dbs.GetAsync();
            var items = new List<UneItem>();
            foreach (var c in (await db.Table<Concours>().ToListAsync()).Where(c => c.Statut == StatutPublication.Valide && !c.EstClos && c.JoursRestants <= 30))
                items.Add(new UneItem(TypeCible.Concours, c.Id, c.Etablissement, $"{c.Intitule} • {c.Places} places",
                    $"{Fmt.Date(c.DateCloture)}", Fmt.Fcfa(c.FraisFcfa), c.DateCloture, c.JoursRestants, c.Urgence, c.BadgeJours));
            foreach (var b in (await db.Table<Bourse>().ToListAsync()).Where(b => b.Statut == StatutPublication.Valide && !b.EstClose && b.JoursRestants <= 30))
                items.Add(new UneItem(TypeCible.Bourse, b.Id, b.Titre, $"{b.NiveauCible} • {b.Organisme}",
                    $"Clôture {Fmt.Date(b.DateCloture)}", b.BilletInclus ? "Billet inclus" : b.Avantages, b.DateCloture, b.JoursRestants, b.Urgence, $"J-{b.JoursRestants}"));
            return items.OrderBy(i => i.Jours).Take(max).ToList();
        }))!;

    public async Task<Bourse?> ObtenirBourseAsync(int id)
    {
        var distant = await api.GetAsync<Bourse>($"/api/bourses/{id}");
        if (distant != null)
        {
            await FusionnerBoursesAsync([distant]);
            return distant;
        }

        var db = await dbs.GetAsync();
        return await db.FindAsync<Bourse>(id);
    }

    // ---------- Favoris
    public async Task<HashSet<int>> FavorisAsync(int userId, TypeCible type)
    {
        var distant = await api.GetAsync<List<int>>($"/api/utilisateurs/{userId}/favoris/{(int)type}");
        if (distant != null)
        {
            var db = await dbs.GetAsync();
            var locaux = (await db.Table<Favori>().ToListAsync()).Where(f => f.UtilisateurId == userId && f.Cible == type).ToList();
            foreach (var f in locaux.Where(f => !distant.Contains(f.CibleId))) await db.DeleteAsync(f);
            foreach (var id in distant.Where(id => locaux.All(f => f.CibleId != id)))
                await db.InsertAsync(new Favori { UtilisateurId = userId, Cible = type, CibleId = id });
            return distant.ToHashSet();
        }

        var local = await dbs.GetAsync();
        return (await local.Table<Favori>().ToListAsync())
            .Where(f => f.UtilisateurId == userId && f.Cible == type).Select(f => f.CibleId).ToHashSet();
    }

    /// <summary>Bascule le favori ; retourne le nouvel état.</summary>
    public async Task<bool> ToggleFavoriAsync(int userId, TypeCible type, int cibleId)
    {
        var distant = await api.PostAsync<ToggleFavoriApi>(
            $"/api/utilisateurs/{userId}/favoris/toggle/{(int)type}/{cibleId}");
        if (distant != null)
        {
            await AppliquerFavoriLocalAsync(userId, type, cibleId, distant.EstFavori);
            return distant.EstFavori;
        }

        var db = await dbs.GetAsync();
        var existant = (await db.Table<Favori>().ToListAsync())
            .FirstOrDefault(f => f.UtilisateurId == userId && f.Cible == type && f.CibleId == cibleId);
        if (existant != null) { await db.DeleteAsync(existant); return false; }
        await db.InsertAsync(new Favori { UtilisateurId = userId, Cible = type, CibleId = cibleId });
        return true;
    }

    // ---------- Éligibilité (niveau du profil vs niveaux acceptés)
    public static Eligibilite Evaluer(Utilisateur? u, Concours c)
    {
        if (u == null || u.Role != RoleUtilisateur.Candidat || string.IsNullOrWhiteSpace(u.Niveau)
            || string.IsNullOrWhiteSpace(c.NiveauxAcceptes)) return Eligibilite.Inconnue;
        var acceptes = c.NiveauxAcceptes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return acceptes.Contains(u.Niveau, StringComparer.OrdinalIgnoreCase) ? Eligibilite.Eligible : Eligibilite.NonEligible;
    }

    // ---------- Synchronisation de la copie locale (mode hors ligne)
    // Cache court des réponses serveur : les onglets se relancent à chaque passage dans Shell,
    // inutile de re-télécharger le même catalogue toutes les 2 secondes.
    readonly Dictionary<string, (DateTime date, object valeur)> _cache = new();

    async Task<T?> CacheAsync<T>(string cle, TimeSpan duree, Func<Task<T?>> lecture) where T : class
    {
        if (_cache.TryGetValue(cle, out var e) && DateTime.UtcNow - e.date < duree)
            return (T?)e.valeur;
        var v = await lecture();
        if (v != null) _cache[cle] = (DateTime.UtcNow, v);
        return v;
    }

    void InvaliderCache() => _cache.Clear();

    async Task FusionnerConcoursAsync(IEnumerable<Concours> liste)
    {
        var db = await dbs.GetAsync();
        var items = liste.ToList();
        var ids = items.Select(c => c.Id).ToList();
        var locales = (await db.Table<Concours>().ToListAsync()).Where(c => ids.Contains(c.Id)).ToDictionary(c => c.Id);

        foreach (var c in items)
        {
            // Conserve l'arrêté téléchargé sur l'appareil si le serveur n'en a pas.
            if (locales.TryGetValue(c.Id, out var local) && !string.IsNullOrEmpty(local.ArretePath)
                && File.Exists(local.ArretePath) && (string.IsNullOrEmpty(c.ArretePath) || !File.Exists(c.ArretePath)))
                c.ArretePath = local.ArretePath;
        }
        await db.RunInTransactionAsync(conn => { foreach (var c in items) conn.InsertOrReplace(c); });
    }

    async Task FusionnerBoursesAsync(IEnumerable<Bourse> liste)
    {
        var db = await dbs.GetAsync();
        var items = liste.ToList();
        var ids = items.Select(b => b.Id).ToList();
        var locales = (await db.Table<Bourse>().ToListAsync()).Where(b => ids.Contains(b.Id)).ToDictionary(b => b.Id);

        foreach (var b in items)
        {
            if (locales.TryGetValue(b.Id, out var local) && !string.IsNullOrEmpty(local.ArretePath)
                && File.Exists(local.ArretePath) && (string.IsNullOrEmpty(b.ArretePath) || !File.Exists(b.ArretePath)))
                b.ArretePath = local.ArretePath;
        }
        await db.RunInTransactionAsync(conn => { foreach (var b in items) conn.InsertOrReplace(b); });
    }

    async Task AppliquerFavoriLocalAsync(int userId, TypeCible type, int cibleId, bool actif)
    {
        var db = await dbs.GetAsync();
        var existant = (await db.Table<Favori>().ToListAsync())
            .FirstOrDefault(f => f.UtilisateurId == userId && f.Cible == type && f.CibleId == cibleId);
        if (actif && existant == null)
            await db.InsertAsync(new Favori { UtilisateurId = userId, Cible = type, CibleId = cibleId });
        else if (!actif && existant != null)
            await db.DeleteAsync(existant);
        InvaliderCache();
    }
}
