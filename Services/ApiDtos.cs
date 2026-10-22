namespace ConcoursCM.Services;

// Contrats d'échange avec le backend ConcoursCM (mêmes noms que les modèles locaux).

public record AuthApiResponse(bool Succes, string? Erreur, Utilisateur? Utilisateur, string? Jeton);
public record ConnexionApiRequest(string? Identifiant, string? MotDePasse, RoleUtilisateur RoleAttendu);
public record CandidatApiRequest(string? Nom, string? Identifiant, string? Region, string? MotDePasse, bool Conditions);
public record InstitutionApiRequest(string? Etablissement, string? Tutelle, string? Representant, string? Email,
    string? Telephone, string? MotDePasse, string? MandatPdfBase64, bool Certifie);

public record OperationApi(bool Succes, string? Erreur);
public record ToggleFavoriApi(bool EstFavori);
public record ChecklistApiEntry(PieceRequise Piece, ChecklistItem Item);
public record SuiviApi(Concours Concours, int Total, int Coches);
public record AnnonceApi(string Titre, string Type, StatutPublication Statut, DateTime Date);
public record CocheApiRequest(bool Coche);
public record SeuilsApiRequest(int[] Seuils);
public record ServiceSmsApiRequest(string? Numero);

public record PublicationApiRequest(
    int UtilisateurId,
    bool EstBourse,
    string Intitule,
    DateTime DateCloture,
    DateTime DateEpreuves,
    int Places,
    int FraisFcfa,
    string Lien,
    string? ArretePdfBase64,
    string Reference,
    string Portee,
    string Avantages,
    string NiveauCible);

public record SmsApiRequest(string Numero, string Message);
public record SmsApiResponse(bool Envoye);
public record ModerationApiResponse(int Accreditations, int Concours, int Bourses);
