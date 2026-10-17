using SQLite;

namespace ConcoursCM.Models;

[Table("Utilisateurs")]
public class Utilisateur
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed(Unique = true)] public string Identifiant { get; set; } = "";   // e-mail (minuscule) ou +237XXXXXXXXX
    public string NomComplet { get; set; } = "";
    public string MotDePasseHash { get; set; } = "";
    public string Sel { get; set; } = "";
    public RoleUtilisateur Role { get; set; }
    public string Region { get; set; } = "";
    public string Niveau { get; set; } = "";          // ex. "Bac C" (sert à l'éligibilité)
    public int AnneeObtention { get; set; }
    public bool Verifie { get; set; }
    public bool SmsActif { get; set; } = true;
    public string NumeroSms { get; set; } = "";
    public DateTime DateCreation { get; set; } = DateTime.Now;

    // --- Institution
    public string NomEtablissement { get; set; } = "";
    public string Tutelle { get; set; } = "";
    public string Representant { get; set; } = "";
    public string EmailInstitutionnel { get; set; } = "";
    public string TelephoneOfficiel { get; set; } = "";
    public string MandatPath { get; set; } = "";
    public StatutPublication Accreditation { get; set; } = StatutPublication.EnAttente;

    [Ignore] public string Prenom => NomComplet.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
    [Ignore] public string Initiales => Fmt.Initiales(NomComplet);
    [Ignore] public bool EstInstitution => Role == RoleUtilisateur.Institution;
    [Ignore] public bool PeutPublier => Role == RoleUtilisateur.Institution && Accreditation == StatutPublication.Valide;
}
