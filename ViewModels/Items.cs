namespace ConcoursCM.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string? erreur;
}

public static class Palette
{
    public static readonly Color Primary = Color.FromArgb("#1E3A8A");
    public static readonly Color Danger = Color.FromArgb("#DC2626");
    public static readonly Color Teal = Color.FromArgb("#0F766E");
    public static readonly Color Ink = Color.FromArgb("#1E293B");
    public static readonly Color Soft = Color.FromArgb("#DBEAFE");
    public static readonly Color White = Colors.White;
    public static readonly Color Gris = Color.FromArgb("#E2E8F0");

    public static (Color bg, Color fg) Badge(NiveauUrgence u) => u switch
    {
        NiveauUrgence.Critique or NiveauUrgence.Urgent => (Color.FromArgb("#FEE2E2"), Color.FromArgb("#B91C1C")),
        NiveauUrgence.Proche => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#92400E")),
        NiveauUrgence.Ferme => (Gris, Color.FromArgb("#475569")),
        _ => (Soft, Primary)
    };
}

public partial class ChipVm(string label, bool selected) : ObservableObject
{
    public string Label { get; } = label;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Bg), nameof(Fg))] bool estSelectionne = selected;
    public Color Bg => EstSelectionne ? Color.FromArgb("#0B2A7A") : Palette.Soft;
    public Color Fg => EstSelectionne ? Colors.White : Palette.Primary;
}

public partial class ConcoursCardVm : ObservableObject
{
    public Concours C { get; }
    public int Id => C.Id;
    public string Initiales { get; }
    public string Etablissement => C.Etablissement;
    public string CategorieBadge => C.Categorie.ToUpperInvariant();
    public string Intitule => C.Intitule;
    public string ClotureTexte => Fmt.Date(C.DateCloture);
    public bool ClotureUrgente => C.Urgence is NiveauUrgence.Critique or NiveauUrgence.Urgent;
    public bool ClotureNormale => !ClotureUrgente;
    public string Quittance => Fmt.Fcfa(C.FraisFcfa);
    public string Diplome => C.Diplome;
    public string PlacesTexte => $"{C.Places} places ouvertes";
    public string SourceTexte => $"Dossier {C.Tutelle}";
    public string StatutTexte { get; }
    public Color StatutBg { get; }
    public Color StatutFg { get; }
    public bool ArreteVisible => C.ADesArrete;
    public string EligibiliteTexte { get; }
    public bool EligibiliteVisible { get; }
    public Color EligibiliteColor { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FavoriGlyph))] bool estFavori;
    public string FavoriGlyph => EstFavori ? "★" : "☆";

    public ConcoursCardVm(Concours c, bool favori, Eligibilite el)
    {
        C = c; EstFavori = favori; Initiales = Fmt.Initiales(c.Sigle.Length > 0 ? c.Sigle : c.Etablissement);
        if (c.EstClos) { StatutTexte = "Clos"; StatutBg = Palette.Gris; StatutFg = Color.FromArgb("#475569"); }
        else if (c.EstNouveau) { StatutTexte = "Nouveau"; StatutBg = Color.FromArgb("#38BDF8"); StatutFg = Colors.White; }
        else { StatutTexte = "Ouvert"; StatutBg = Palette.Soft; StatutFg = Palette.Primary; }

        (EligibiliteTexte, EligibiliteVisible, EligibiliteColor) = el switch
        {
            Eligibilite.Eligible => ("✓ Éligible avec votre profil", true, Color.FromArgb("#16A34A")),
            Eligibilite.NonEligible => ($"Niveau requis : {c.Diplome}", true, Color.FromArgb("#B45309")),
            _ => ("", false, Colors.Gray)
        };
    }
}

public partial class BourseCardVm : ObservableObject
{
    public Bourse B { get; }
    public int Id => B.Id;
    public string Zone => B.Zone;
    public string Titre => B.Titre;
    public string Avantages => (B.BilletInclus ? "✈ " : "🛡 ") + B.Avantages;
    public string DateTexte => Fmt.Date(B.DateCloture);
    public bool DateUrgente => B.Urgence is NiveauUrgence.Critique or NiveauUrgence.Urgent;
    public bool DateNormale => !DateUrgente;
    public string Portee => "● " + B.Portee;
    public string Initiales { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(FavoriGlyph))] bool estFavori;
    public string FavoriGlyph => EstFavori ? "🔖" : "🔲";

    public BourseCardVm(Bourse b, bool favori) { B = b; EstFavori = favori; Initiales = Fmt.Initiales(b.Organisme); }
}

public class UneItemVm(UneItem i)
{
    public UneItem Item { get; } = i;
    public int Id => Item.Id;
    public string Initiales => Fmt.Initiales(Item.Titre);
    public string Titre => Item.Titre;
    public string SousTitre => Item.SousTitre;
    public string Info1 => (Item.Cible == TypeCible.Concours ? "📅 " : "⏱ ") + Item.Info1;
    public string Info2 => (Item.Cible == TypeCible.Concours ? "💵 " : "✈ ") + Item.Info2;
    public string Badge => Item.Badge;
    public Color BadgeBg => Palette.Badge(Item.Urgence).bg;
    public Color BadgeFg => Palette.Badge(Item.Urgence).fg;
}

public partial class PieceChecklistVm : ObservableObject
{
    public PieceRequise Piece { get; }
    public ChecklistItem Item { get; }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CaseColor), nameof(CaseGlyph), nameof(DetailTexte), nameof(DetailAFaire), nameof(DetailFait))] bool coche;

    public string Libelle => Piece.Libelle;
    public string Icone => Piece.Icone;
    public Color CaseColor => Coche ? Palette.Primary : Palette.Soft;
    public string CaseGlyph => Coche ? "✓" : "";
    public string DetailTexte => Coche ? Piece.Detail : $"À préparer — {Piece.Detail}";
    public bool DetailAFaire => !Coche;
    public bool DetailFait => Coche;

    public PieceChecklistVm(PieceRequise p, ChecklistItem i) { Piece = p; Item = i; Coche = i.Coche; }
}

public class AlerteVm(Alerte a)
{
    public Alerte A { get; } = a;
    public string Badge => A.Badge;
    public string Titre => A.Titre;
    public string Message => A.Message;
    public string Temps => Fmt.Depuis(A.DateCreation);
    public string PiedTexte => A.PiedTexte;
    public string ActionLibelle => A.ActionLibelle;
    public bool NonLu => !A.Lu;
    public string Icone => A.Type switch { TypeAlerte.Rappel => "⏰", TypeAlerte.ArreteOfficiel => "✔", TypeAlerte.Bourse => "🏅", _ => "💬" };
    public Color Accent => A.Type switch
    {
        TypeAlerte.Rappel when A.Urgent => Palette.Danger,
        TypeAlerte.Rappel => Color.FromArgb("#B45309"),
        TypeAlerte.ArreteOfficiel => Palette.Primary,
        TypeAlerte.Bourse => Palette.Teal,
        _ => Palette.Ink
    };
    public Color BadgeBg => A.Type switch
    {
        TypeAlerte.Rappel => Color.FromArgb("#FEE2E2"),
        TypeAlerte.ArreteOfficiel => Palette.Soft,
        TypeAlerte.Bourse => Color.FromArgb("#CFFAFE"),
        _ => Color.FromArgb("#E2E8F0")
    };
    public Color IconBg => BadgeBg;
}

public class SuiviVm(Suivi s)
{
    public Suivi S { get; } = s;
    public int ConcoursId => S.Concours.Id;
    public string Titre => $"{S.Concours.Sigle} {S.Concours.DateCloture.Year}";
    public string SousTitre => S.Concours.Intitule;
    public string Avancement => $"{S.Coches}/{S.Total} pièces ({S.Pourcent}%)";
    public double Progress => S.Total == 0 ? 0 : (double)S.Coches / S.Total;
    public string Badge => S.Concours.BadgeJours;
    public Color BadgeBg => Palette.Badge(S.Concours.Urgence).bg;
    public Color BadgeFg => Palette.Badge(S.Concours.Urgence).fg;
}

public class AnnonceVm((string titre, string type, StatutPublication statut, DateTime date) a)
{
    public string Titre => a.titre;
    public string Type => a.type;
    public string Date => Fmt.Date(a.date);
    public string StatutTexte => a.statut switch { StatutPublication.Valide => "Publié", StatutPublication.Rejete => "Rejeté", _ => "En vérification" };
    public Color StatutBg => a.statut switch { StatutPublication.Valide => Color.FromArgb("#DCFCE7"), StatutPublication.Rejete => Color.FromArgb("#FEE2E2"), _ => Color.FromArgb("#FEF3C7") };
    public Color StatutFg => a.statut switch { StatutPublication.Valide => Color.FromArgb("#166534"), StatutPublication.Rejete => Color.FromArgb("#B91C1C"), _ => Color.FromArgb("#92400E") };
}
