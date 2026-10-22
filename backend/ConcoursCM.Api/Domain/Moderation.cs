using ConcoursCM.Api.Contracts;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Helpers;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Domain;

/// <summary>Back-office simulé : valide les accréditations et les annonces après le délai configuré.</summary>
public static class Moderation
{
    public static async Task<ModerationResponse> ValiderEnAttenteAsync(AppDb db, TimeSpan delai)
    {
        var limite = DateTime.Now - delai;
        var nAccreditations = 0;
        var nConcours = 0;
        var nBourses = 0;

        var institutions = await db.Utilisateurs
            .Where(u => u.Role == RoleUtilisateur.Institution && u.Accreditation == StatutPublication.EnAttente && u.DateCreation <= limite)
            .ToListAsync();
        foreach (var u in institutions)
        {
            u.Accreditation = StatutPublication.Valide;
            u.Verifie = true;
            nAccreditations++;
        }
        if (nAccreditations > 0) await db.SaveChangesAsync();

        var concours = await db.Concours
            .Where(c => c.Statut == StatutPublication.EnAttente && c.DateSoumission <= limite)
            .ToListAsync();
        foreach (var c in concours)
        {
            c.Statut = StatutPublication.Valide;
            c.DatePublication = DateTime.Now;
            await db.SaveChangesAsync();
            await Alertes.NotifierNouvelleAnnonceAsync(db, c);
            nConcours++;
        }

        var bourses = await db.Bourses
            .Where(b => b.Statut == StatutPublication.EnAttente && b.DateSoumission <= limite)
            .ToListAsync();
        foreach (var b in bourses)
        {
            b.Statut = StatutPublication.Valide;
            b.DatePublication = DateTime.Now;
            await db.SaveChangesAsync();
            await Alertes.NotifierNouvelleBourseAsync(db, b);
            nBourses++;
        }

        return new ModerationResponse(nAccreditations, nConcours, nBourses);
    }
}

/// <summary>Règles anti-fraude sur le lien de candidature, identiques à celles de l'application.</summary>
public static class ReglesPublication
{
    static readonly string[] Raccourcisseurs = ["bit.ly", "tinyurl.com", "t.co", "wa.me", "t.me", "facebook.com", "fb.com", "linktr.ee", "shorturl.at"];
    static readonly string[] HotesBoursesFiables = ["campusfrance.org", "chevening.org", "fcdo.gov.uk", "daad.de", "gov.uk", "minesup.gov.cm", "scholarships.gc.ca"];

    public static string? ValiderLien(string? lien, Utilisateur u, bool bourse)
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

    public static string Sigle(string nom)
    {
        var mots = nom.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(m => m.Length > 3 || m.All(char.IsUpper)).ToList();
        if (nom.Length <= 8 && !nom.Contains(' ')) return nom.ToUpperInvariant();
        var s = string.Concat(mots.Select(m => char.ToUpperInvariant(m[0])));
        return s.Length >= 2 ? s : nom[..Math.Min(5, nom.Length)].ToUpperInvariant();
    }
}
