using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Models;

namespace ConcoursCM.Api.Domain;

/// <summary>Vérification du jeton de session et anti-brute-force côté serveur.</summary>
public static class Securite
{
    const int MaxEchecs = 5;
    static readonly TimeSpan DureeVerrou = TimeSpan.FromMinutes(5);
    static readonly ConcurrentDictionary<string, (int Echecs, DateTime VerrouJusqua)> Echecs = new();

    public static string NouveauJeton() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static bool Autorise(HttpContext ctx, Utilisateur u)
    {
        var jeton = ctx.Request.Headers["X-Jeton"].ToString();
        if (string.IsNullOrEmpty(jeton) || string.IsNullOrEmpty(u.Jeton)) return false;
        var a = Encoding.UTF8.GetBytes(jeton);
        var b = Encoding.UTF8.GetBytes(u.Jeton);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static async Task<Utilisateur?> ChargerAutoriseAsync(HttpContext ctx, AppDb db, int id)
    {
        var u = await db.Utilisateurs.FindAsync(id);
        return u != null && Autorise(ctx, u) ? u : null;
    }

    public static string? VerrouActif(string identifiant)
    {
        if (!Echecs.TryGetValue(identifiant, out var e) || e.VerrouJusqua <= DateTime.UtcNow) return null;
        var min = (int)Math.Ceiling((e.VerrouJusqua - DateTime.UtcNow).TotalMinutes);
        return $"Trop de tentatives. Réessayez dans {Math.Max(min, 1)} min.";
    }

    public static void EchecConnexion(string identifiant)
    {
        Echecs.AddOrUpdate(identifiant,
            _ => (1, DateTime.MinValue),
            (_, e) =>
            {
                var n = e.Echecs + 1;
                return n >= MaxEchecs
                    ? (0, DateTime.UtcNow.Add(DureeVerrou))
                    : (n, DateTime.MinValue);
            });
    }

    public static void ReussiteConnexion(string identifiant) => Echecs.TryRemove(identifiant, out _);
}
