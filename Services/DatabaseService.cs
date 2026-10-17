using SQLite;

namespace ConcoursCM.Services;

/// <summary>Base locale SQLite + données de démonstration (dates relatives à aujourd'hui).</summary>
public class DatabaseService
{
    SQLiteAsyncConnection? _db;
    readonly SemaphoreSlim _verrou = new(1, 1);

    public async Task<SQLiteAsyncConnection> GetAsync()
    {
        if (_db != null) return _db;
        await _verrou.WaitAsync();
        try
        {
            if (_db != null) return _db;
            var db = new SQLiteAsyncConnection(
                Path.Combine(FileSystem.AppDataDirectory, "concourscm.db3"),
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            await db.CreateTableAsync<Utilisateur>();
            await db.CreateTableAsync<Concours>();
            await db.CreateTableAsync<Bourse>();
            await db.CreateTableAsync<PieceRequise>();
            await db.CreateTableAsync<ChecklistItem>();
            await db.CreateTableAsync<Favori>();
            await db.CreateTableAsync<Alerte>();

            await SeedAsync(db);
            _db = db;
            return db;
        }
        finally { _verrou.Release(); }
    }

    static async Task SeedAsync(SQLiteAsyncConnection db)
    {
        if (await db.Table<Concours>().CountAsync() > 0) return;
        var j = DateTime.Today;

        var enspy = new Concours
        {
            Etablissement = "ENSPY Yaoundé", Sigle = "ENSPY", Intitule = "Ingénieur de Conception (5 ans)",
            Categorie = "Ingénierie", Tutelle = "MINESUP",
            Description = "Concours d'entrée en cycle ingénieur de conception à l'École Nationale Supérieure Polytechnique de Yaoundé.",
            NiveauxAcceptes = "Bac C,Bac D,Bac E,Bac TI,GCE A-Level", Diplome = "Bac C / D / GCE",
            DateCloture = j.AddDays(18), HeureCloture = "15:30", DateEpreuves = j.AddDays(23),
            Places = 250, FraisFcfa = 20000, LienOfficiel = "https://www.polytechnique.cm",
            ReferenceArrete = "Arrêté_MINESUP_ENSPY", DateArrete = j.AddDays(-40),
            LieuDepot = "Antenne MINESUP — Yaoundé (Sous-sol)",
            Statut = StatutPublication.Valide, DatePublication = j.AddDays(-30), DateSoumission = j.AddDays(-31)
        };
        var fmsb = new Concours
        {
            Etablissement = "FMSB Yaoundé", Sigle = "FMSB", Intitule = "Doctorat en Médecine Générale",
            Categorie = "Santé", Tutelle = "MINESUP",
            Description = "Concours d'entrée en première année de médecine générale à la Faculté de Médecine et des Sciences Biomédicales.",
            NiveauxAcceptes = "Bac C,Bac D,GCE A-Level", Diplome = "Bac C / D / GCE",
            DateCloture = j.AddDays(26), HeureCloture = "15:30", DateEpreuves = j.AddDays(40),
            Places = 150, FraisFcfa = 50000, LienOfficiel = "https://www.uy1.uninet.cm",
            ReferenceArrete = "Arrêté N°24/0115", DateArrete = j.AddDays(-1),
            LieuDepot = "Scolarité FMSB — Ngoa-Ekellé, Yaoundé",
            Statut = StatutPublication.Valide, DatePublication = j.AddDays(-1), DateSoumission = j.AddDays(-2)
        };
        var enam = new Concours
        {
            Etablissement = "ENAM Cameroun", Sigle = "ENAM", Intitule = "Cycle A - Régie Financière & Douanes",
            Categorie = "Fonction publique", Tutelle = "MINFOPRA",
            Description = "Concours d'entrée à l'École Nationale d'Administration et de Magistrature, cycle A.",
            NiveauxAcceptes = "Licence,Master", Diplome = "Licence / Master",
            DateCloture = j.AddDays(60), HeureCloture = "15:30", DateEpreuves = j.AddDays(85),
            Places = 100, FraisFcfa = 25000, LienOfficiel = "https://www.enam.cm",
            ReferenceArrete = "Arrêté MINFOPRA", DateArrete = j.AddDays(-10),
            LieuDepot = "Direction ENAM — Yaoundé",
            Statut = StatutPublication.Valide, DatePublication = j.AddDays(-9), DateSoumission = j.AddDays(-10)
        };
        foreach (var c in new[] { enspy, fmsb, enam }) await db.InsertAsync(c);

        foreach (var c in new[] { enspy, fmsb, enam })
        {
            var diplome = c.Categorie == "Fonction publique"
                ? ("Diplôme de Licence ou Master", "Photocopie certifiée conforme et légalisée")
                : ("Relevé de notes officiel du Bac", "Signé de l'OBC ou relevé certifié");
            var pieces = new List<PieceRequise>
            {
                new() { ConcoursId = c.Id, Ordre = 1, Libelle = "Acte de naissance certifié conforme", Detail = "Légalisé < 3 mois par commissariat/mairie", Icone = "🪪" },
                new() { ConcoursId = c.Id, Ordre = 2, Libelle = diplome.Item1, Detail = diplome.Item2, Icone = "🎓" },
                new() { ConcoursId = c.Id, Ordre = 3, Libelle = "Certificat médical d'aptitude", Detail = "Médecin du secteur public (< 3 mois)", Icone = "⚕️" },
                new() { ConcoursId = c.Id, Ordre = 4, Libelle = "Quittance originale des frais", Detail = $"MTN MoMo / Orange Money — {Fmt.Fcfa(c.FraisFcfa)}, reçu imprimé", Icone = "🧾" },
                new() { ConcoursId = c.Id, Ordre = 5, Libelle = "4 photos d'identité 4×4", Detail = "Fond blanc récent", Icone = "📷" },
            };
            await db.InsertAllAsync(pieces);
        }

        await db.InsertAllAsync(new[]
        {
            new Bourse { Titre = "Bourse d'Excellence MINESUP", Organisme = "MINESUP", Zone = "CAMEROUN • UNIVERSITÉS D'ÉTAT",
                Categorie = "Excellence", Portee = "National", Avantages = "Allocation annuelle", NiveauCible = "Licence – Master",
                DateCloture = j.AddDays(20), LienOfficiel = "https://www.minesup.gov.cm",
                Statut = StatutPublication.Valide, DatePublication = j.AddDays(-14) },
            new Bourse { Titre = "France Excellence Eiffel", Organisme = "Campus France", Zone = "FRANCE • MASTER & DOC",
                Categorie = "Master/Doctorat", Portee = "International", Avantages = "Prise en charge intégrale", NiveauCible = "Master & Doctorat",
                DateCloture = j.AddDays(95), LienOfficiel = "https://www.campusfrance.org",
                Statut = StatutPublication.Valide, DatePublication = j.AddDays(-5) },
            new Bourse { Titre = "Bourse du Commonwealth", Organisme = "Commonwealth Scholarship Commission", Zone = "ROYAUME-UNI & CANADA",
                Categorie = "Coopération", Portee = "Partenariat", Avantages = "Scolarité + billet avion", NiveauCible = "Master & Doctorat", BilletInclus = true,
                DateCloture = j.AddDays(35), LienOfficiel = "https://cscuk.fcdo.gov.uk",
                Statut = StatutPublication.Valide, DatePublication = j.AddDays(-3) },
        });
    }
}
