using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ConcoursCM.Services;

/// <summary>Génère la « fiche récapitulative » PDF du dossier physique et la partage.</summary>
public class FicheRecapService
{
    public async Task GenererEtPartagerAsync(Utilisateur u, Concours c, IReadOnlyList<(PieceRequise piece, ChecklistItem item)> pieces)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var chemin = Path.Combine(FileSystem.CacheDirectory, $"Fiche_{c.Sigle}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
        var faites = pieces.Count(p => p.item.Coche);

        await Task.Run(() =>
        {
            Document.Create(doc => doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("ConcoursCM — Fiche récapitulative de dossier").FontSize(16).Bold().FontColor("#1E3A8A");
                    col.Item().Text($"Édité le {DateTime.Now:dd/MM/yyyy à HH:mm}").FontSize(9).FontColor("#64748B");
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#1E3A8A");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text($"Candidat : {u.NomComplet}").Bold();
                    col.Item().Text($"Concours : {c.Etablissement} — {c.Intitule}").Bold();
                    col.Item().Text($"Tutelle : {c.Tutelle}   |   Quittance : {Fmt.Fcfa(c.FraisFcfa)}");
                    col.Item().Text($"Date limite de dépôt : {Fmt.Date(c.DateCloture)} à {c.HeureCloture.Replace(':', 'h')}").FontColor("#B91C1C").Bold();
                    col.Item().Text($"Lieu de dépôt : {c.LieuDepot}");
                    col.Item().PaddingTop(6).Text($"Avancement : {faites}/{pieces.Count} pièces prêtes").Bold();

                    foreach (var (p, i) in pieces)
                        col.Item().Row(r =>
                        {
                            r.ConstantItem(26).Text(i.Coche ? "[x]" : "[  ]").Bold();
                            r.RelativeItem().Column(cc =>
                            {
                                cc.Item().Text(p.Libelle).SemiBold();
                                cc.Item().Text(p.Detail).FontSize(9).FontColor("#64748B");
                            });
                        });

                    col.Item().PaddingTop(10).Background("#FEF3C7").Padding(8).Text(
                        "Rappel : le dépôt et le paiement des frais se font uniquement via le portail ou le guichet officiel de l'établissement. " +
                        "Aucun intermédiaire n'est agréé.").FontSize(9);
                });

                page.Footer().AlignCenter().Text(t => { t.Span("ConcoursCM • Page ").FontSize(9); t.CurrentPageNumber().FontSize(9); });
            })).GeneratePdf(chemin);
        });

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = $"Fiche dossier {c.Sigle}",
            File = new ShareFile(chemin)
        });
    }
}
