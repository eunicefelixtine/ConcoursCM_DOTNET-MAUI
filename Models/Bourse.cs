using SQLite;

namespace ConcoursCM.Models;

public class Bourse
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Titre { get; set; } = "";
    public string Organisme { get; set; } = "";
    public string Zone { get; set; } = "";               // "CAMEROUN • UNIVERSITÉS D'ÉTAT"
    public string Categorie { get; set; } = "Excellence"; // Excellence, Coopération, Master/Doctorat
    public string Portee { get; set; } = "National";      // National, International, Partenariat
    public string Avantages { get; set; } = "";
    public string NiveauCible { get; set; } = "";
    public bool BilletInclus { get; set; }
    public DateTime DateCloture { get; set; }
    public string LienOfficiel { get; set; } = "";
    public string ArretePath { get; set; } = "";
    public bool Verifie { get; set; } = true;
    public StatutPublication Statut { get; set; } = StatutPublication.EnAttente;
    public int ProprietaireId { get; set; }
    public DateTime DateSoumission { get; set; } = DateTime.Now;
    public DateTime DatePublication { get; set; } = DateTime.Now;

    [Ignore] public int JoursRestants => (int)(DateCloture.Date - DateTime.Today).TotalDays;
    [Ignore] public bool EstClose => DateCloture.Date < DateTime.Today;
    [Ignore] public NiveauUrgence Urgence => EstClose ? NiveauUrgence.Ferme
        : JoursRestants <= 3 ? NiveauUrgence.Critique
        : JoursRestants <= 7 ? NiveauUrgence.Urgent
        : JoursRestants <= 30 ? NiveauUrgence.Proche
        : NiveauUrgence.Aucun;
}
