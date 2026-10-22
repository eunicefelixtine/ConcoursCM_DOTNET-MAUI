namespace ConcoursCM.Services;

public record Suivi(Concours Concours, int Total, int Coches)
{
    public int Pourcent => Total == 0 ? 0 : (int)Math.Round(100.0 * Coches / Total);
}

/// <summary>Checklist de dossier physique : créer une checklist = « suivre » le concours.</summary>
public class ChecklistService(DatabaseService dbs, ApiClient api)
{
    public async Task<List<(PieceRequise piece, ChecklistItem item)>> ChargerAsync(int userId, int concoursId)
    {
        var distant = await api.GetAsync<List<ChecklistApiEntry>>($"/api/utilisateurs/{userId}/checklist/{concoursId}");
        if (distant != null)
        {
            var db = await dbs.GetAsync();
            foreach (var e in distant) await db.InsertOrReplaceAsync(e.Piece);

            var locaux = (await db.Table<ChecklistItem>().ToListAsync())
                .Where(i => i.UtilisateurId == userId && i.ConcoursId == concoursId).ToList();
            foreach (var e in distant)
            {
                foreach (var vieux in locaux.Where(i => i.PieceRequiseId == e.Item.PieceRequiseId && i.Id != e.Item.Id))
                    await db.DeleteAsync(vieux);
                await db.InsertOrReplaceAsync(e.Item);
            }
            return distant.Select(e => (e.Piece, e.Item)).ToList();
        }

        var local = await dbs.GetAsync();
        var pieces = (await local.Table<PieceRequise>().ToListAsync()).Where(p => p.ConcoursId == concoursId).OrderBy(p => p.Ordre).ToList();
        var items = (await local.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == userId && i.ConcoursId == concoursId).ToList();

        // Crée les lignes manquantes (aussi si l'institution ajoute une pièce après coup)
        foreach (var p in pieces.Where(p => items.All(i => i.PieceRequiseId != p.Id)))
        {
            var it = new ChecklistItem { UtilisateurId = userId, ConcoursId = concoursId, PieceRequiseId = p.Id };
            await local.InsertAsync(it);
            items.Add(it);
        }
        return pieces.Select(p => (p, items.First(i => i.PieceRequiseId == p.Id))).ToList();
    }

    public async Task DefinirCocheAsync(ChecklistItem item, bool coche)
    {
        var distant = await api.PutAsync<ChecklistItem>($"/api/checklist/{item.Id}", new CocheApiRequest(coche));
        if (distant != null)
        {
            var db = await dbs.GetAsync();
            var locaux = (await db.Table<ChecklistItem>().ToListAsync())
                .Where(i => i.UtilisateurId == item.UtilisateurId && i.ConcoursId == item.ConcoursId
                            && i.PieceRequiseId == item.PieceRequiseId).ToList();
            foreach (var l in locaux.Where(i => i.Id != distant.Id)) await db.DeleteAsync(l);
            await db.InsertOrReplaceAsync(distant);

            item.Id = distant.Id;
            item.Coche = distant.Coche;
            item.DateMaj = distant.DateMaj;
            return;
        }

        var local = await dbs.GetAsync();
        item.Coche = coche;
        item.DateMaj = DateTime.Now;
        await local.UpdateAsync(item);
    }

    public async Task<bool> EstSuiviAsync(int userId, int concoursId)
    {
        var distant = await api.GetAsync<bool?>($"/api/utilisateurs/{userId}/suivis/{concoursId}");
        if (distant is bool suivi) return suivi;

        var db = await dbs.GetAsync();
        return (await db.Table<ChecklistItem>().ToListAsync()).Any(i => i.UtilisateurId == userId && i.ConcoursId == concoursId);
    }

    public async Task<List<Suivi>> SuivisAsync(int userId)
    {
        var distant = await api.GetAsync<List<SuiviApi>>($"/api/utilisateurs/{userId}/suivis");
        if (distant != null)
        {
            var db = await dbs.GetAsync();
            foreach (var s in distant) await db.InsertOrReplaceAsync(s.Concours);
            return distant.Select(s => new Suivi(s.Concours, s.Total, s.Coches)).ToList();
        }

        var local = await dbs.GetAsync();
        var items = (await local.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == userId).ToList();
        var concoursMap = (await local.Table<Concours>().ToListAsync()).ToDictionary(c => c.Id);
        return items.GroupBy(i => i.ConcoursId)
            .Where(g => concoursMap.ContainsKey(g.Key))
            .Select(g => new Suivi(concoursMap[g.Key], g.Count(), g.Count(i => i.Coche)))
            .OrderBy(s => s.Concours.EstClos).ThenBy(s => s.Concours.DateCloture).ToList();
    }

    public async Task<int> NbSuivisAsync(int userId) => (await SuivisAsync(userId)).Count;

    public async Task RetirerSuiviAsync(int userId, int concoursId)
    {
        await api.DeleteAsync($"/api/utilisateurs/{userId}/suivis/{concoursId}");

        var db = await dbs.GetAsync();
        foreach (var i in (await db.Table<ChecklistItem>().ToListAsync()).Where(i => i.UtilisateurId == userId && i.ConcoursId == concoursId))
            await db.DeleteAsync(i);
    }
}
