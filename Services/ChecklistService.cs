namespace ConcoursCM.Services;

public record Suivi(Concours Concours, int Total, int Coches)
{
    public int Pourcent => Total == 0 ? 0 : (int)Math.Round(100.0 * Coches / Total);
}

/// <summary>Checklist de dossier physique : créer une checklist = « suivre » le concours.</summary>
public class ChecklistService(DatabaseService dbs)
{
    public async Task<List<(PieceRequise piece, ChecklistItem item)>> ChargerAsync(int userId, int concoursId)
    {
        var db = await dbs.GetAsync();
        var pieces = (await db.Table<PieceRequise>().ToListAsync()).Where(p => p.ConcoursId == concoursId).OrderBy(p => p.Ordre).ToList();
        var items = (await db.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == userId && i.ConcoursId == concoursId).ToList();

        // Crée les lignes manquantes (aussi si l'institution ajoute une pièce après coup)
        foreach (var p in pieces.Where(p => items.All(i => i.PieceRequiseId != p.Id)))
        {
            var it = new ChecklistItem { UtilisateurId = userId, ConcoursId = concoursId, PieceRequiseId = p.Id };
            await db.InsertAsync(it);
            items.Add(it);
        }
        return pieces.Select(p => (p, items.First(i => i.PieceRequiseId == p.Id))).ToList();
    }

    public async Task DefinirCocheAsync(ChecklistItem item, bool coche)
    {
        var db = await dbs.GetAsync();
        item.Coche = coche;
        item.DateMaj = DateTime.Now;
        await db.UpdateAsync(item);
    }

    public async Task<bool> EstSuiviAsync(int userId, int concoursId)
    {
        var db = await dbs.GetAsync();
        return (await db.Table<ChecklistItem>().ToListAsync()).Any(i => i.UtilisateurId == userId && i.ConcoursId == concoursId);
    }

    public async Task<List<Suivi>> SuivisAsync(int userId)
    {
        var db = await dbs.GetAsync();
        var items = (await db.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == userId).ToList();
        var concours = (await db.Table<Concours>().ToListAsync()).ToDictionary(c => c.Id);
        return items.GroupBy(i => i.ConcoursId)
            .Where(g => concours.ContainsKey(g.Key))
            .Select(g => new Suivi(concours[g.Key], g.Count(), g.Count(i => i.Coche)))
            .OrderBy(s => s.Concours.EstClos).ThenBy(s => s.Concours.DateCloture).ToList();
    }

    public async Task<int> NbSuivisAsync(int userId) => (await SuivisAsync(userId)).Count;

    public async Task RetirerSuiviAsync(int userId, int concoursId)
    {
        var db = await dbs.GetAsync();
        foreach (var i in (await db.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == userId && i.ConcoursId == concoursId))
            await db.DeleteAsync(i);
    }
}
