namespace ConcoursCM.Services;

public record UneItem(TypeCible Cible, int Id, string Titre, string SousTitre, string Info1, string Info2, DateTime Cloture, int Jours, NiveauUrgence Urgence, string Badge);
public record StatsAccueil(int Ouverts, int Nouveaux, int Bourses);

/// <summary>Lecture du catalogue (concours + bourses) : recherche, filtres, tri, favoris, éligibilité.</summary>
public class ConcoursService(DatabaseService dbs)
{
    // ---------- Concours
    public async Task<List<string>> CategoriesConcoursAsync()
    {
        var db = await dbs.GetAsync();
        return (await db.Table<Concours>().ToListAsync())
            .Where(c => c.Statut == StatutPublication.Valide)
            .Select(c => c.Categorie).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<List<Concours>> ListerConcoursAsync(string? texte, string? categorie, bool seulementOuverts,
        bool seulementEligibles, Utilisateur? user, TriConcours tri)
    {
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
        var db = await dbs.GetAsync();
        return await db.FindAsync<Concours>(id);
    }

    public async Task<List<PieceRequise>> PiecesAsync(int concoursId)
    {
        var db = await dbs.GetAsync();
        return (await db.Table<PieceRequise>().ToListAsync()).Where(p => p.ConcoursId == concoursId).OrderBy(p => p.Ordre).ToList();
    }

    // ---------- Bourses
    public async Task<List<string>> CategoriesBoursesAsync()
    {
        var db = await dbs.GetAsync();
        return (await db.Table<Bourse>().ToListAsync())
            .Where(b => b.Statut == StatutPublication.Valide)
            .Select(b => b.Categorie).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<List<Bourse>> ListerBoursesAsync(string? texte, string? categorie, bool seulementOuvertes)
    {
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
    public async Task<StatsAccueil> StatsAsync()
    {
        var db = await dbs.GetAsync();
        var cs = (await db.Table<Concours>().ToListAsync()).Where(c => c.Statut == StatutPublication.Valide && !c.EstClos).ToList();
        var bs = (await db.Table<Bourse>().ToListAsync()).Where(b => b.Statut == StatutPublication.Valide && !b.EstClose).ToList();
        return new StatsAccueil(cs.Count, cs.Count(c => c.EstNouveau), bs.Count);
    }

    /// <summary>« À la Une » : concours et bourses ouverts, clôture dans les 30 jours, les plus proches d'abord.</summary>
    public async Task<List<UneItem>> UnesAsync(int max = 4)
    {
        var db = await dbs.GetAsync();
        var items = new List<UneItem>();
        foreach (var c in (await db.Table<Concours>().ToListAsync()).Where(c => c.Statut == StatutPublication.Valide && !c.EstClos && c.JoursRestants <= 30))
            items.Add(new UneItem(TypeCible.Concours, c.Id, c.Etablissement, $"{c.Intitule} • {c.Places} places",
                $"{Fmt.Date(c.DateCloture)}", Fmt.Fcfa(c.FraisFcfa), c.DateCloture, c.JoursRestants, c.Urgence, c.BadgeJours));
        foreach (var b in (await db.Table<Bourse>().ToListAsync()).Where(b => b.Statut == StatutPublication.Valide && !b.EstClose && b.JoursRestants <= 30))
            items.Add(new UneItem(TypeCible.Bourse, b.Id, b.Titre, $"{b.NiveauCible} • {b.Organisme}",
                $"Clôture {Fmt.Date(b.DateCloture)}", b.BilletInclus ? "Billet inclus" : b.Avantages, b.DateCloture, b.JoursRestants, b.Urgence, $"J-{b.JoursRestants}"));
        return items.OrderBy(i => i.Jours).Take(max).ToList();
    }

    public async Task<Bourse?> ObtenirBourseAsync(int id)
    {
        var db = await dbs.GetAsync();
        return await db.FindAsync<Bourse>(id);
    }

    // ---------- Favoris
    public async Task<HashSet<int>> FavorisAsync(int userId, TypeCible type)
    {
        var db = await dbs.GetAsync();
        return (await db.Table<Favori>().ToListAsync())
            .Where(f => f.UtilisateurId == userId && f.Cible == type).Select(f => f.CibleId).ToHashSet();
    }

    /// <summary>Bascule le favori ; retourne le nouvel état.</summary>
    public async Task<bool> ToggleFavoriAsync(int userId, TypeCible type, int cibleId)
    {
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
}
