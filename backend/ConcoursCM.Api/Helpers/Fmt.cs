using System.Globalization;
using System.Text;

namespace ConcoursCM.Api.Helpers;

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

    public static string Normaliser(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }
}
