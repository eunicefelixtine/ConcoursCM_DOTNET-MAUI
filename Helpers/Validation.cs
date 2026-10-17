using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ConcoursCM.Helpers;

public enum ForceMotDePasse { Faible, Moyen, Fort }

public static class Validation
{
    static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
    static readonly Regex TelCm = new(@"^(?:\+?237)?(6\d{8})$", RegexOptions.Compiled);

    static readonly string[] MessageriesGrandPublic =
        ["gmail.com", "yahoo.com", "yahoo.fr", "hotmail.com", "hotmail.fr", "outlook.com", "outlook.fr", "live.com", "icloud.com"];

    public static readonly string[] Regions =
        ["Adamaoua", "Centre", "Est", "Extrême-Nord", "Littoral", "Nord", "Nord-Ouest", "Ouest", "Sud", "Sud-Ouest"];

    public static readonly string[] Niveaux =
        ["Bac A", "Bac C", "Bac D", "Bac E", "Bac TI", "GCE A-Level", "Licence", "Master"];

    public static readonly string[] Tutelles =
    [
        "MINESUP — Université d'État", "MINESUP — Établissement privé agréé", "MINFOPRA — Fonction publique",
        "MINSANTE — Formation sanitaire", "MINEFOP — Formation professionnelle", "Autre tutelle"
    ];

    /// <summary>Normalise un identifiant : e-mail en minuscules, téléphone en +237XXXXXXXXX.</summary>
    public static bool TryNormaliserIdentifiant(string? saisie, out string normalise, out bool estTelephone)
    {
        normalise = ""; estTelephone = false;
        var s = (saisie ?? "").Trim();
        if (s.Length == 0) return false;

        var chiffres = Regex.Replace(s, @"[\s\.\-\(\)]", "");
        var m = TelCm.Match(chiffres);
        if (m.Success) { normalise = "+237" + m.Groups[1].Value; estTelephone = true; return true; }
        if (Email.IsMatch(s)) { normalise = s.ToLowerInvariant(); return true; }
        return false;
    }

    public static bool EstEmailGrandPublic(string email)
    {
        var at = email.LastIndexOf('@');
        return at >= 0 && MessageriesGrandPublic.Contains(email[(at + 1)..].ToLowerInvariant());
    }

    public static string DomaineDe(string email)
    {
        var at = email.LastIndexOf('@');
        return at >= 0 ? email[(at + 1)..].ToLowerInvariant() : "";
    }

    /// <summary>Opérateur déduit du préfixe (indicatif, les plages exactes évoluent).</summary>
    public static string Operateur(string telNormalise)
    {
        var n = telNormalise.Replace("+237", "");
        if (n.Length != 9) return "";
        if (n.StartsWith("69") || (n.StartsWith("65") && "56789".Contains(n[2]))) return "Orange";
        if (n.StartsWith("67") || n.StartsWith("68") || (n.StartsWith("65") && "01234".Contains(n[2]))) return "MTN";
        if (n.StartsWith("66")) return "Nexttel";
        if (n.StartsWith("62")) return "Camtel";
        return "";
    }

    public static ForceMotDePasse Force(string? mdp)
    {
        var m = mdp ?? "";
        int score = 0;
        if (m.Length >= 8) score++;
        if (m.Length >= 12) score++;
        if (m.Any(char.IsLetter) && m.Any(char.IsDigit)) score++;
        if (m.Any(c => !char.IsLetterOrDigit(c))) score++;
        if (m.Any(char.IsUpper) && m.Any(char.IsLower)) score++;
        return score <= 1 ? ForceMotDePasse.Faible : score <= 3 ? ForceMotDePasse.Moyen : ForceMotDePasse.Fort;
    }

    public static string? ErreurMotDePasse(string? mdp, int min, bool lettreEtChiffre)
    {
        var m = mdp ?? "";
        if (m.Length < min) return $"Le mot de passe doit contenir au moins {min} caractères.";
        if (lettreEtChiffre && !(m.Any(char.IsLetter) && m.Any(char.IsDigit)))
            return "Le mot de passe doit mélanger lettres et chiffres.";
        return null;
    }
}

public static class PasswordHasher
{
    const int Iterations = 100_000;

    public static (string hash, string sel) Hacher(string motDePasse)
    {
        var sel = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(motDePasse, sel, Iterations, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(h), Convert.ToBase64String(sel));
    }

    public static bool Verifier(string motDePasse, string hash, string sel)
    {
        var h = Rfc2898DeriveBytes.Pbkdf2(motDePasse, Convert.FromBase64String(sel), Iterations, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(hash));
    }
}
