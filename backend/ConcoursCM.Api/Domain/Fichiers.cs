namespace ConcoursCM.Api.Domain;

/// <summary>Stockage des PDF (mandats, arrêtés) côté serveur.</summary>
public static class Fichiers
{
    public static string RacineStockage { get; set; } = Path.Combine(AppContext.BaseDirectory, "storage");

    public static (string? chemin, string? erreur) EnregistrerPdf(string? base64, string sousDossier, int tailleMaxMo)
    {
        if (string.IsNullOrWhiteSpace(base64)) return (null, "Le document PDF est manquant.");

        byte[] octets;
        try { octets = Convert.FromBase64String(base64); }
        catch { return (null, "Le document transmis n'est pas un fichier valide."); }

        if (octets.Length == 0) return (null, "Le document PDF est vide.");
        if (octets.Length > tailleMaxMo * 1024L * 1024L) return (null, $"Le PDF dépasse {tailleMaxMo} Mo.");
        if (octets.Length < 5 || octets[0] != 0x25 || octets[1] != 0x50 || octets[2] != 0x44 || octets[3] != 0x46)
            return (null, "Le document doit être au format PDF.");

        var dossier = Path.Combine(RacineStockage, sousDossier);
        Directory.CreateDirectory(dossier);
        var chemin = Path.Combine(dossier, $"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(chemin, octets);
        return (chemin, null);
    }
}
