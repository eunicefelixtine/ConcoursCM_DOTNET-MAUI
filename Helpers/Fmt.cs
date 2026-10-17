using System.Text;

namespace ConcoursCM.Helpers;

public static class Fmt
{
    static readonly string[] Mois = ["Janv", "Févr", "Mars", "Avr", "Mai", "Juin", "Juil", "Août", "Sept", "Oct", "Nov", "Déc"];

    public static string Date(DateTime d) => $"{d.Day:00} {Mois[d.Month - 1]} {d.Year}";

    public static string Fcfa(int v) =>
        v <= 0 ? "Gratuit" : string.Format(CultureInfo.InvariantCulture, "{0:N0}", v).Replace(',', ' ') + " FCFA";

    public static string Initiales(string? s)
    {
        var mots = (s ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return mots.Length switch
        {
            0 => "?",
            1 => mots[0][..1].ToUpperInvariant(),
            _ => (mots[0][..1] + mots[1][..1]).ToUpperInvariant()
        };
    }

    /// <summary>Minuscules sans accents, pour des recherches tolérantes.</summary>
    public static string Normaliser(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    public static string Depuis(DateTime d)
    {
        var t = DateTime.Now - d;
        if (t.TotalMinutes < 1) return "À l'instant";
        if (t.TotalMinutes < 60) return $"Il y a {(int)t.TotalMinutes} min";
        if (t.TotalHours < 24) return $"Il y a {(int)t.TotalHours}h";
        if (t.TotalHours < 48) return $"Hier à {d:HH'h'mm}";
        return $"Il y a {(int)t.TotalDays}j";
    }

    /// <summary>Année académique courante : à partir de septembre → N/N+1.</summary>
    public static string AnneeAcademique(DateTime n) =>
        n.Month >= 9 ? $"{n.Year}/{n.Year + 1}" : $"{n.Year - 1}/{n.Year}";
}
