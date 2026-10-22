using ConcoursCM.Api.Models;

namespace ConcoursCM.Api.Contracts;

public record UtilisateurDto(
    int Id,
    string Identifiant,
    string NomComplet,
    RoleUtilisateur Role,
    string Region,
    string Niveau,
    int AnneeObtention,
    bool Verifie,
    bool SmsActif,
    string NumeroSms,
    DateTime DateCreation,
    string NomEtablissement,
    string Tutelle,
    string Representant,
    string EmailInstitutionnel,
    string TelephoneOfficiel,
    string MandatPath,
    StatutPublication Accreditation)
{
    public static UtilisateurDto De(Utilisateur u) => new(
        u.Id, u.Identifiant, u.NomComplet, u.Role, u.Region, u.Niveau, u.AnneeObtention, u.Verifie,
        u.SmsActif, u.NumeroSms, u.DateCreation, u.NomEtablissement, u.Tutelle, u.Representant,
        u.EmailInstitutionnel, u.TelephoneOfficiel, u.MandatPath, u.Accreditation);
}

public record ConnexionRequest(string? Identifiant, string? MotDePasse, RoleUtilisateur RoleAttendu);
public record CandidatRequest(string? Nom, string? Identifiant, string? Region, string? MotDePasse, bool Conditions);
public record InstitutionRequest(string? Etablissement, string? Tutelle, string? Representant, string? Email,
    string? Telephone, string? MotDePasse, string? MandatPdfBase64, bool Certifie);

public record AuthResponse(bool Succes, string? Erreur, UtilisateurDto? Utilisateur, string? Jeton)
{
    public static AuthResponse Echec(string erreur) => new(false, erreur, null, null);
    public static AuthResponse Ok(Utilisateur u, string jeton) => new(true, null, UtilisateurDto.De(u), jeton);
}

public record OperationResponse(bool Succes, string? Erreur)
{
    public static OperationResponse Ok() => new(true, null);
    public static OperationResponse Echec(string erreur) => new(false, erreur);
}

public record ToggleFavoriResponse(bool EstFavori);
public record ChecklistEntry(PieceRequise Piece, ChecklistItem Item);
public record SuiviDto(Concours Concours, int Total, int Coches);
public record AnnonceDto(string Titre, string Type, StatutPublication Statut, DateTime Date);
public record StatsDto(int Ouverts, int Nouveaux, int Bourses);
public record UneDto(TypeCible Cible, int Id, string Titre, string SousTitre, string Info1, string Info2,
    DateTime Cloture, int Jours, NiveauUrgence Urgence, string Badge);

public record CocheRequest(bool Coche);
public record SynchroniserAlertesRequest(int[]? Seuils);
public record ServiceSmsRequest(string? Numero);
public record SmsRequest(string Numero, string Message);
public record SmsResponse(bool Envoye);

public record PublicationRequest(
    int UtilisateurId,
    bool EstBourse,
    string? Intitule,
    DateTime DateCloture,
    DateTime DateEpreuves,
    int Places,
    int FraisFcfa,
    string? Lien,
    string? ArretePdfBase64,
    string? Reference,
    string? Portee,
    string? Avantages,
    string? NiveauCible);

public record ModerationResponse(int Accreditations, int Concours, int Bourses);
