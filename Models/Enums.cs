namespace ConcoursCM.Models;

public enum RoleUtilisateur { Candidat = 0, Institution = 1, Administrateur = 2 }
public enum StatutPublication { EnAttente = 0, Valide = 1, Rejete = 2 }
public enum TypeCible { Concours = 0, Bourse = 1 }
public enum TypeAlerte { Rappel = 0, ArreteOfficiel = 1, Bourse = 2, Service = 3 }
public enum NiveauUrgence { Aucun = 0, Proche = 1, Urgent = 2, Critique = 3, Ferme = 4 }
public enum Eligibilite { Inconnue = 0, Eligible = 1, NonEligible = 2 }
public enum TriConcours { Cloture = 0, Recents = 1, Alphabetique = 2 }

public record OpResult(bool Succes, string? Erreur = null)
{
    public static OpResult Ok() => new(true);
    public static OpResult Echec(string erreur) => new(false, erreur);
}
