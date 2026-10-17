using SQLite;

namespace ConcoursCM.Models;

public class Concours
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Etablissement { get; set; } = "";
    public string Sigle { get; set; } = "";
    public string Intitule { get; set; } = "";
    public string Categorie { get; set; } = "";          // Ingénierie, Santé, Fonction publique...
    public string Tutelle { get; set; } = "MINESUP";
    public string Description { get; set; } = "";
    public string NiveauxAcceptes { get; set; } = "";    // CSV : "Bac C,Bac D,GCE A-Level"
    public string Diplome { get; set; } = "";            // libellé affiché
    public DateTime DateCloture { get; set; }
    public string HeureCloture { get; set; } = "15:30";
    public DateTime? DateEpreuves { get; set; }
    public int Places { get; set; }
    public int FraisFcfa { get; set; }
    public string LienOfficiel { get; set; } = "";
    public string ReferenceArrete { get; set; } = "";
    public string ArretePath { get; set; } = "";
    public DateTime? DateArrete { get; set; }
    public string LieuDepot { get; set; } = "";
    public StatutPublication Statut { get; set; } = StatutPublication.EnAttente;
    public int ProprietaireId { get; set; }
    public DateTime DateSoumission { get; set; } = DateTime.Now;
    public DateTime DatePublication { get; set; } = DateTime.Now;

    [Ignore] public int JoursRestants => (int)(DateCloture.Date - DateTime.Today).TotalDays;
    [Ignore] public DateTime CloturePrecise => DateCloture.Date + (TimeSpan.TryParse(HeureCloture, out var h) ? h : TimeSpan.Zero);
    [Ignore] public bool EstClos => DateTime.Now > CloturePrecise;
    [Ignore] public bool EstNouveau => Statut == StatutPublication.Valide && (DateTime.Today - DatePublication.Date).TotalDays <= 7;
    [Ignore] public bool ADesArrete => !string.IsNullOrWhiteSpace(ReferenceArrete);
    [Ignore] public NiveauUrgence Urgence => EstClos ? NiveauUrgence.Ferme
        : JoursRestants <= 3 ? NiveauUrgence.Critique
        : JoursRestants <= 7 ? NiveauUrgence.Urgent
        : JoursRestants <= 30 ? NiveauUrgence.Proche
        : NiveauUrgence.Aucun;
    [Ignore] public string BadgeJours => EstClos ? "Clos" : JoursRestants <= 0 ? "Dernier jour" : $"J-{JoursRestants}";
}
