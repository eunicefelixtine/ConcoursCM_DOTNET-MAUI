using ConcoursCM.Api.Contracts;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Helpers;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Endpoints;

public static class CatalogueEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/concours/categories", CategoriesConcoursAsync);
        app.MapGet("/api/concours", ListerConcoursAsync);
        app.MapGet("/api/concours/{id:int}", ObtenirConcoursAsync);
        app.MapGet("/api/concours/{id:int}/pieces", PiecesAsync);

        app.MapGet("/api/bourses/categories", CategoriesBoursesAsync);
        app.MapGet("/api/bourses", ListerBoursesAsync);
        app.MapGet("/api/bourses/{id:int}", ObtenirBourseAsync);

        app.MapGet("/api/stats", StatsAsync);
        app.MapGet("/api/unes", UnesAsync);
    }

    static async Task<IResult> CategoriesConcoursAsync(AppDb db)
    {
        var categories = await db.Concours
            .Where(c => c.Statut == StatutPublication.Valide)
            .Select(c => c.Categorie)
            .ToListAsync();
        return Results.Ok(categories.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList());
    }

    static async Task<IResult> ListerConcoursAsync(AppDb db, string? texte, string? categorie,
        bool seulementOuverts = false, bool seulementEligibles = false, int? userId = null, TriConcours tri = TriConcours.Cloture)
    {
        IEnumerable<Concours> q = await db.Concours.Where(c => c.Statut == StatutPublication.Valide).ToListAsync();

        if (seulementOuverts) q = q.Where(c => !c.EstClos);
        if (!string.IsNullOrWhiteSpace(categorie) && categorie != "Tous") q = q.Where(c => c.Categorie == categorie);

        if (seulementEligibles && userId is int uid)
        {
            var user = await db.Utilisateurs.FindAsync(uid);
            if (user != null) q = q.Where(c => Evaluer(user, c) != Eligibilite.NonEligible);
        }

        var t = Fmt.Normaliser(texte);
        if (t.Length > 0)
            q = q.Where(c => Fmt.Normaliser($"{c.Etablissement} {c.Sigle} {c.Intitule} {c.Categorie} {c.Tutelle}").Contains(t));

        q = tri switch
        {
            TriConcours.Recents => q.OrderByDescending(c => c.DatePublication),
            TriConcours.Alphabetique => q.OrderBy(c => c.Etablissement),
            _ => q.OrderBy(c => c.EstClos).ThenBy(c => c.DateCloture)
        };
        return Results.Ok(q.ToList());
    }

    static async Task<IResult> ObtenirConcoursAsync(int id, AppDb db)
    {
        var c = await db.Concours.FindAsync(id);
        return c == null ? Results.NotFound() : Results.Ok(c);
    }

    static async Task<IResult> PiecesAsync(int id, AppDb db) =>
        Results.Ok(await db.PieceRequises.Where(p => p.ConcoursId == id).OrderBy(p => p.Ordre).ToListAsync());

    static async Task<IResult> CategoriesBoursesAsync(AppDb db)
    {
        var categories = await db.Bourses
            .Where(b => b.Statut == StatutPublication.Valide)
            .Select(b => b.Categorie)
            .ToListAsync();
        return Results.Ok(categories.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().OrderBy(c => c).ToList());
    }

    static async Task<IResult> ListerBoursesAsync(AppDb db, string? texte, string? categorie, bool seulementOuvertes = false)
    {
        IEnumerable<Bourse> q = await db.Bourses.Where(b => b.Statut == StatutPublication.Valide).ToListAsync();
        if (seulementOuvertes) q = q.Where(b => !b.EstClose);
        if (!string.IsNullOrWhiteSpace(categorie) && categorie != "Toutes") q = q.Where(b => b.Categorie == categorie);
        var t = Fmt.Normaliser(texte);
        if (t.Length > 0)
            q = q.Where(b => Fmt.Normaliser($"{b.Titre} {b.Organisme} {b.Zone} {b.NiveauCible}").Contains(t));
        return Results.Ok(q.OrderBy(b => b.EstClose).ThenBy(b => b.DateCloture).ToList());
    }

    static async Task<IResult> ObtenirBourseAsync(int id, AppDb db)
    {
        var b = await db.Bourses.FindAsync(id);
        return b == null ? Results.NotFound() : Results.Ok(b);
    }

    static async Task<IResult> StatsAsync(AppDb db)
    {
        var cs = (await db.Concours.Where(c => c.Statut == StatutPublication.Valide).ToListAsync()).Where(c => !c.EstClos).ToList();
        var bs = (await db.Bourses.Where(b => b.Statut == StatutPublication.Valide).ToListAsync()).Where(b => !b.EstClose).ToList();
        return Results.Ok(new StatsDto(cs.Count, cs.Count(c => c.EstNouveau), bs.Count));
    }

    static async Task<IResult> UnesAsync(AppDb db, int max = 4)
    {
        var items = new List<UneDto>();
        var concours = (await db.Concours.Where(c => c.Statut == StatutPublication.Valide).ToListAsync())
            .Where(c => !c.EstClos && c.JoursRestants <= 30);
        foreach (var c in concours)
            items.Add(new UneDto(TypeCible.Concours, c.Id, c.Etablissement, $"{c.Intitule} • {c.Places} places",
                $"{Fmt.Date(c.DateCloture)}", Fmt.Fcfa(c.FraisFcfa), c.DateCloture, c.JoursRestants, c.Urgence, c.BadgeJours));

        var bourses = (await db.Bourses.Where(b => b.Statut == StatutPublication.Valide).ToListAsync())
            .Where(b => !b.EstClose && b.JoursRestants <= 30);
        foreach (var b in bourses)
            items.Add(new UneDto(TypeCible.Bourse, b.Id, b.Titre, $"{b.NiveauCible} • {b.Organisme}",
                $"Clôture {Fmt.Date(b.DateCloture)}", b.BilletInclus ? "Billet inclus" : b.Avantages,
                b.DateCloture, b.JoursRestants, b.Urgence, $"J-{b.JoursRestants}"));

        return Results.Ok(items.OrderBy(i => i.Jours).Take(max).ToList());
    }

    static Eligibilite Evaluer(Utilisateur u, Concours c)
    {
        if (u.Role != RoleUtilisateur.Candidat || string.IsNullOrWhiteSpace(u.Niveau) || string.IsNullOrWhiteSpace(c.NiveauxAcceptes))
            return Eligibilite.Inconnue;
        var acceptes = c.NiveauxAcceptes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return acceptes.Contains(u.Niveau, StringComparer.OrdinalIgnoreCase) ? Eligibilite.Eligible : Eligibilite.NonEligible;
    }
}
