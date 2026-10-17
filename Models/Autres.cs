using SQLite;

namespace ConcoursCM.Models;

/// <summary>Pièce du dossier exigée par un concours (modèle publié par l'institution).</summary>
public class PieceRequise
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int ConcoursId { get; set; }
    public int Ordre { get; set; }
    public string Libelle { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Icone { get; set; } = "📄";
}

/// <summary>État d'une pièce pour un candidat. Sa création = le candidat « suit » le concours.</summary>
public class ChecklistItem
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int UtilisateurId { get; set; }
    [Indexed] public int ConcoursId { get; set; }
    public int PieceRequiseId { get; set; }
    public bool Coche { get; set; }
    public DateTime DateMaj { get; set; } = DateTime.Now;
}

public class Favori
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int UtilisateurId { get; set; }
    public TypeCible Cible { get; set; }
    public int CibleId { get; set; }
}

public class Alerte
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    [Indexed] public int UtilisateurId { get; set; }
    [Indexed] public string Cle { get; set; } = "";       // anti-doublon : "rappel-concours-3-J3"
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
