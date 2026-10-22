using ConcoursCM.Api.Contracts;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Domain;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Endpoints;

public static class UtilisateurEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/utilisateurs/{id:int}", ObtenirUtilisateurAsync);
        app.MapPut("/api/utilisateurs/{id:int}", MettreAJourUtilisateurAsync);

        app.MapGet("/api/utilisateurs/{id:int}/favoris/{type:int}", FavorisAsync);
        app.MapPost("/api/utilisateurs/{id:int}/favoris/toggle/{type:int}/{cibleId:int}", ToggleFavoriAsync);

        app.MapGet("/api/utilisateurs/{id:int}/checklist/{concoursId:int}", ChargerChecklistAsync);
        app.MapPut("/api/checklist/{itemId:int}", DefinirCocheAsync);

        app.MapGet("/api/utilisateurs/{id:int}/suivis", SuivisAsync);
        app.MapGet("/api/utilisateurs/{id:int}/suivis/{concoursId:int}", EstSuiviAsync);
        app.MapDelete("/api/utilisateurs/{id:int}/suivis/{concoursId:int}", RetirerSuiviAsync);

        app.MapGet("/api/utilisateurs/{id:int}/alertes", ListerAlertesAsync);
        app.MapGet("/api/utilisateurs/{id:int}/alertes/non-lus", NbNonLusAsync);
        app.MapPut("/api/utilisateurs/{id:int}/alertes/tout-lu", ToutLuAsync);
        app.MapPost("/api/utilisateurs/{id:int}/alertes/synchroniser", SynchroniserAlertesAsync);
        app.MapPost("/api/utilisateurs/{id:int}/alertes/service-sms", NotifierServiceSmsAsync);
        app.MapPut("/api/alertes/{id:int}/lu", MarquerLuAsync);

        app.MapGet("/api/utilisateurs/{id:int}/annonces", MesAnnoncesAsync);
    }

    static async Task<IResult> ObtenirUtilisateurAsync(int id, HttpContext ctx, AppDb db)
    {
        var u = await Securite.ChargerAutoriseAsync(ctx, db, id);
        return u == null ? Results.Unauthorized() : Results.Ok(UtilisateurDto.De(u));
    }

    static async Task<IResult> MettreAJourUtilisateurAsync(int id, UtilisateurDto dto, HttpContext ctx, AppDb db)
    {
        var u = await Securite.ChargerAutoriseAsync(ctx, db, id);
        if (u == null) return Results.Unauthorized();

        u.NomComplet = dto.NomComplet ?? u.NomComplet;
        u.Region = dto.Region ?? u.Region;
        u.Niveau = dto.Niveau ?? u.Niveau;
        u.AnneeObtention = dto.AnneeObtention;
        u.SmsActif = dto.SmsActif;
        u.NumeroSms = dto.NumeroSms ?? u.NumeroSms;
        u.NomEtablissement = dto.NomEtablissement ?? u.NomEtablissement;
        u.Tutelle = dto.Tutelle ?? u.Tutelle;
        u.Representant = dto.Representant ?? u.Representant;
        u.EmailInstitutionnel = dto.EmailInstitutionnel ?? u.EmailInstitutionnel;
        u.TelephoneOfficiel = dto.TelephoneOfficiel ?? u.TelephoneOfficiel;

        await db.SaveChangesAsync();
        return Results.Ok(UtilisateurDto.De(u));
    }

    static async Task<IResult> FavorisAsync(int id, int type, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        if (!Enum.IsDefined(typeof(TypeCible), type)) return Results.BadRequest(new { erreur = "Type de cible invalide." });
        var cible = (TypeCible)type;
        var ids = await db.Favoris.Where(f => f.UtilisateurId == id && f.Cible == cible)
            .Select(f => f.CibleId).ToListAsync();
        return Results.Ok(ids);
    }

    static async Task<IResult> ToggleFavoriAsync(int id, int type, int cibleId, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        if (!Enum.IsDefined(typeof(TypeCible), type)) return Results.BadRequest(new { erreur = "Type de cible invalide." });
        var cible = (TypeCible)type;

        var existant = await db.Favoris.FirstOrDefaultAsync(f => f.UtilisateurId == id && f.Cible == cible && f.CibleId == cibleId);
        if (existant != null)
        {
            db.Favoris.Remove(existant);
            await db.SaveChangesAsync();
            return Results.Ok(new ToggleFavoriResponse(false));
        }

        var existeCible = cible == TypeCible.Concours
            ? await db.Concours.AnyAsync(c => c.Id == cibleId)
            : await db.Bourses.AnyAsync(b => b.Id == cibleId);
        if (!existeCible) return Results.NotFound();

        db.Favoris.Add(new Favori { UtilisateurId = id, Cible = cible, CibleId = cibleId });
        await db.SaveChangesAsync();
        return Results.Ok(new ToggleFavoriResponse(true));
    }

    static async Task<IResult> ChargerChecklistAsync(int id, int concoursId, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        if (!await db.Concours.AnyAsync(c => c.Id == concoursId)) return Results.NotFound();

        var pieces = await db.PieceRequises.Where(p => p.ConcoursId == concoursId).OrderBy(p => p.Ordre).ToListAsync();
        var items = await db.ChecklistItems.Where(i => i.UtilisateurId == id && i.ConcoursId == concoursId).ToListAsync();

        var ajouts = pieces.Where(p => items.All(i => i.PieceRequiseId != p.Id))
            .Select(p => new ChecklistItem { UtilisateurId = id, ConcoursId = concoursId, PieceRequiseId = p.Id })
            .ToList();
        if (ajouts.Count > 0)
        {
            db.ChecklistItems.AddRange(ajouts);
            await db.SaveChangesAsync();
            items.AddRange(ajouts);
        }

        var resultat = pieces.Select(p => new ChecklistEntry(p, items.First(i => i.PieceRequiseId == p.Id))).ToList();
        return Results.Ok(resultat);
    }

    static async Task<IResult> DefinirCocheAsync(int itemId, CocheRequest req, HttpContext ctx, AppDb db)
    {
        var item = await db.ChecklistItems.FindAsync(itemId);
        if (item == null) return Results.NotFound();
        if (await Securite.ChargerAutoriseAsync(ctx, db, item.UtilisateurId) == null) return Results.Unauthorized();

        item.Coche = req.Coche;
        item.DateMaj = DateTime.Now;
        await db.SaveChangesAsync();
        return Results.Ok(item);
    }

    static async Task<IResult> SuivisAsync(int id, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();

        var items = await db.ChecklistItems.Where(i => i.UtilisateurId == id).ToListAsync();
        var ids = items.Select(i => i.ConcoursId).Distinct().ToList();
        var concours = await db.Concours.Where(c => ids.Contains(c.Id)).ToListAsync();
        var map = concours.ToDictionary(c => c.Id);

        var suivis = items.GroupBy(i => i.ConcoursId)
            .Where(g => map.ContainsKey(g.Key))
            .Select(g => new SuiviDto(map[g.Key], g.Count(), g.Count(i => i.Coche)))
            .OrderBy(s => s.Concours.EstClos).ThenBy(s => s.Concours.DateCloture)
            .ToList();
        return Results.Ok(suivis);
    }

    static async Task<IResult> EstSuiviAsync(int id, int concoursId, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        var suivi = await db.ChecklistItems.AnyAsync(i => i.UtilisateurId == id && i.ConcoursId == concoursId);
        return Results.Ok(suivi);
    }

    static async Task<IResult> RetirerSuiviAsync(int id, int concoursId, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        var items = await db.ChecklistItems.Where(i => i.UtilisateurId == id && i.ConcoursId == concoursId).ToListAsync();
        db.ChecklistItems.RemoveRange(items);
        await db.SaveChangesAsync();
        return Results.Ok(OperationResponse.Ok());
    }

    static async Task<IResult> ListerAlertesAsync(int id, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        var alertes = await db.Alertes.Where(a => a.UtilisateurId == id)
            .OrderByDescending(a => a.DateCreation).ToListAsync();
        return Results.Ok(alertes);
    }

    static async Task<IResult> NbNonLusAsync(int id, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        var n = await db.Alertes.CountAsync(a => a.UtilisateurId == id && !a.Lu);
        return Results.Ok(n);
    }

    static async Task<IResult> ToutLuAsync(int id, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();
        var alertes = await db.Alertes.Where(a => a.UtilisateurId == id && !a.Lu).ToListAsync();
        foreach (var a in alertes) a.Lu = true;
        await db.SaveChangesAsync();
        return Results.Ok(OperationResponse.Ok());
    }

    static async Task<IResult> SynchroniserAlertesAsync(int id, SynchroniserAlertesRequest? req, HttpContext ctx, AppDb db)
    {
        var u = await Securite.ChargerAutoriseAsync(ctx, db, id);
        if (u == null) return Results.Unauthorized();

        int[] seuils = req?.Seuils ?? [1, 3, 7];
        await Alertes.SynchroniserAsync(db, u, seuils);

        var alertes = await db.Alertes.Where(a => a.UtilisateurId == id)
            .OrderByDescending(a => a.DateCreation).ToListAsync();
        return Results.Ok(alertes);
    }

    static async Task<IResult> NotifierServiceSmsAsync(int id, ServiceSmsRequest? req, HttpContext ctx, AppDb db)
    {
        var u = await Securite.ChargerAutoriseAsync(ctx, db, id);
        if (u == null) return Results.Unauthorized();

        if (!string.IsNullOrWhiteSpace(req?.Numero))
        {
            u.NumeroSms = req.Numero.Trim();
            await db.SaveChangesAsync();
        }

        var alerte = await Alertes.NotifierServiceSmsAsync(db, u);
        return Results.Ok(alerte);
    }

    static async Task<IResult> MarquerLuAsync(int id, HttpContext ctx, AppDb db)
    {
        var alerte = await db.Alertes.FindAsync(id);
        if (alerte == null) return Results.NotFound();
        if (await Securite.ChargerAutoriseAsync(ctx, db, alerte.UtilisateurId) == null) return Results.Unauthorized();

        if (!alerte.Lu)
        {
            alerte.Lu = true;
            await db.SaveChangesAsync();
        }
        return Results.Ok(alerte);
    }

    static async Task<IResult> MesAnnoncesAsync(int id, HttpContext ctx, AppDb db)
    {
        if (await Securite.ChargerAutoriseAsync(ctx, db, id) == null) return Results.Unauthorized();

        var concours = await db.Concours.Where(c => c.ProprietaireId == id)
            .Select(c => new AnnonceDto(c.Intitule, "Concours", c.Statut, c.DateSoumission)).ToListAsync();
        var bourses = await db.Bourses.Where(b => b.ProprietaireId == id)
            .Select(b => new AnnonceDto(b.Titre, "Bourse", b.Statut, b.DateSoumission)).ToListAsync();
        return Results.Ok(concours.Concat(bourses).OrderByDescending(a => a.Date).ToList());
    }
}
