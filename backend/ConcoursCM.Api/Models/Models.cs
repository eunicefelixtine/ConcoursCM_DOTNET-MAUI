using System.ComponentModel.DataAnnotations.Schema;
using ConcoursCM.Api.Helpers;

namespace ConcoursCM.Api.Models;

public enum RoleUtilisateur { Candidat = 0, Institution = 1, Administrateur = 2 }
public enum StatutPublication { EnAttente = 0, Valide = 1, Rejete = 2 }
public enum TypeCible { Concours = 0, Bourse = 1 }
public enum TypeAlerte { Rappel = 0, ArreteOfficiel = 1, Bourse = 2, Service = 3 }
public enum NiveauUrgence { Aucun = 0, Proche = 1, Urgent = 2, Critique = 3, Ferme = 4 }
public enum Eligibilite { Inconnue = 0, Eligible = 1, NonEligible = 2 }
public enum TriConcours { Cloture = 0, Recents = 1, Alphabetique = 2 }

public class Utilisateur
{
    public int Id { get; set; }
    public string Identifiant { get; set; } = "";
    public string NomComplet { get; set; } = "";
    public string MotDePasseHash { get; set; } = "";
    public string Sel { get; set; } = "";
    public RoleUtilisateur Role { get; set; }
    public string Region { get; set; } = "";
    public string Niveau { get; set; } = "";
    public int AnneeObtention { get; set; }
    public bool Verifie { get; set; }
    public bool SmsActif { get; set; } = true;
    public string NumeroSms { get; set; } = "";
    public DateTime DateCreation { get; set; } = DateTime.Now;

    public string NomEtablissement { get; set; } = "";
    public string Tutelle { get; set; } = "";
    public string Representant { get; set; } = "";
    public string EmailInstitutionnel { get; set; } = "";
    public string TelephoneOfficiel { get; set; } = "";
    public string MandatPath { get; set; } = "";
    public StatutPublication Accreditation { get; set; } = StatutPublication.EnAttente;

    public string? Jeton { get; set; }

    [NotMapped] public string Prenom => NomComplet.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    [NotMapped] public string Initiales => Fmt.Initiales(NomComplet);
    [NotMapped] public bool EstInstitution => Role == RoleUtilisateur.Institution;
    [NotMapped] public bool PeutPublier => Role == RoleUtilisateur.Institution && Accreditation == StatutPublication.Valide;
}

public class Concours
{
    public int Id { get; set; }
    public string Etablissement { get; set; } = "";
    public string Sigle { get; set; } = "";
    public string Intitule { get; set; } = "";
    public string Categorie { get; set; } = "";
    public string Tutelle { get; set; } = "MINESUP";
    public string Description { get; set; } = "";
    public string NiveauxAcceptes { get; set; } = "";
    public string Diplome { get; set; } = "";
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

    [NotMapped] public int JoursRestants => (int)(DateCloture.Date - DateTime.Today).TotalDays;
    [NotMapped] public DateTime CloturePrecise => DateCloture.Date + (TimeSpan.TryParse(HeureCloture, out var h) ? h : TimeSpan.Zero);
    [NotMapped] public bool EstClos => DateTime.Now > CloturePrecise;
    [NotMapped] public bool EstNouveau => Statut == StatutPublication.Valide && (DateTime.Today - DatePublication.Date).TotalDays <= 7;
    [NotMapped] public bool ADesArrete => !string.IsNullOrWhiteSpace(ReferenceArrete);
    [NotMapped] public NiveauUrgence Urgence => EstClos ? NiveauUrgence.Ferme
        : JoursRestants <= 3 ? NiveauUrgence.Critique
        : JoursRestants <= 7 ? NiveauUrgence.Urgent
        : JoursRestants <= 30 ? NiveauUrgence.Proche
        : NiveauUrgence.Aucun;
    [NotMapped] public string BadgeJours => EstClos ? "Clos" : JoursRestants <= 0 ? "Dernier jour" : $"J-{JoursRestants}";
}

public class Bourse
{
    public int Id { get; set; }
    public string Titre { get; set; } = "";
    public string Organisme { get; set; } = "";
    public string Zone { get; set; } = "";
    public string Categorie { get; set; } = "Excellence";
    public string Portee { get; set; } = "National";
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

    [NotMapped] public int JoursRestants => (int)(DateCloture.Date - DateTime.Today).TotalDays;
    [NotMapped] public bool EstClose => DateCloture.Date < DateTime.Today;
    [NotMapped] public NiveauUrgence Urgence => EstClose ? NiveauUrgence.Ferme
        : JoursRestants <= 3 ? NiveauUrgence.Critique
        : JoursRestants <= 7 ? NiveauUrgence.Urgent
        : JoursRestants <= 30 ? NiveauUrgence.Proche
        : NiveauUrgence.Aucun;
}

public class PieceRequise
{
    public int Id { get; set; }
    public int ConcoursId { get; set; }
    public int Ordre { get; set; }
    public string Libelle { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Icone { get; set; } = "📄";
}

public class ChecklistItem
{
    public int Id { get; set; }
    public int UtilisateurId { get; set; }
    public int ConcoursId { get; set; }
    public int PieceRequiseId { get; set; }
    public bool Coche { get; set; }
    public DateTime DateMaj { get; set; } = DateTime.Now;
}

public class Favori
{
    public int Id { get; set; }
    public int UtilisateurId { get; set; }
    public TypeCible Cible { get; set; }
    public int CibleId { get; set; }
}

public class Alerte
{
    public int Id { get; set; }
    public int UtilisateurId { get; set; }
    public string Cle { get; set; } = "";
    public TypeAlerte Type { get; set; }
    public string Badge { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Message { get; set; } = "";
    public string PiedTexte { get; set; } = "";
    public string ActionLibelle { get; set; } = "";
    public bool Urgent { get; set; }
    public bool Lu { get; set; }
    public TypeCible Cible { get; set; }
    public int CibleId { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.Now;
}

public class SmsMessage
{
    public int Id { get; set; }
    public string Numero { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime DateCreation { get; set; } = DateTime.Now;
    public bool Envoye { get; set; }
}
