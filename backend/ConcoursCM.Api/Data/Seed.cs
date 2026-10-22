using ConcoursCM.Api.Helpers;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Data;

/// <summary>
/// Données de démonstration identiques à celles de l'application mobile.
/// Les dates des lignes de démonstration (ProprietaireId = 0) sont recalculées au démarrage
/// afin que le catalogue reste toujours « vivant » quand le serveur tourne longtemps.
/// </summary>
public static class Seed
{
    public static async Task ExecuterAsync(AppDb db)
    {
        if (!await db.Concours.AnyAsync() && !await db.Bourses.AnyAsync())
            await CreerDemoAsync(db);

        await RafraichirDatesDemoAsync(db);
    }

    static async Task CreerDemoAsync(AppDb db)
    {
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
        db.Concours.AddRange(enspy, fmsb, enam);
        await db.SaveChangesAsync();

        foreach (var c in new[] { enspy, fmsb, enam })
        {
            var diplome = c.Categorie == "Fonction publique"
                ? ("Diplôme de Licence ou Master", "Photocopie certifiée conforme et légalisée")
                : ("Relevé de notes officiel du Bac", "Signé de l'OBC ou relevé certifié");
            db.PieceRequises.AddRange(
                new PieceRequise { ConcoursId = c.Id, Ordre = 1, Libelle = "Acte de naissance certifié conforme", Detail = "Légalisé < 3 mois par commissariat/mairie", Icone = "🪪" },
                new PieceRequise { ConcoursId = c.Id, Ordre = 2, Libelle = diplome.Item1, Detail = diplome.Item2, Icone = "🎓" },
                new PieceRequise { ConcoursId = c.Id, Ordre = 3, Libelle = "Certificat médical d'aptitude", Detail = "Médecin du secteur public (< 3 mois)", Icone = "⚕️" },
                new PieceRequise { ConcoursId = c.Id, Ordre = 4, Libelle = "Quittance originale des frais", Detail = $"MTN MoMo / Orange Money — {Fmt.Fcfa(c.FraisFcfa)}, reçu imprimé", Icone = "🧾" },
                new PieceRequise { ConcoursId = c.Id, Ordre = 5, Libelle = "4 photos d'identité 4×4", Detail = "Fond blanc récent", Icone = "📷" });
        }

        db.Bourses.AddRange(
            new Bourse
            {
                Titre = "Bourse d'Excellence MINESUP", Organisme = "MINESUP", Zone = "CAMEROUN • UNIVERSITÉS D'ÉTAT",
                Categorie = "Excellence", Portee = "National", Avantages = "Allocation annuelle", NiveauCible = "Licence – Master",
                DateCloture = j.AddDays(20), LienOfficiel = "https://www.minesup.gov.cm",
                Statut = StatutPublication.Valide, DatePublication = j.AddDays(-14), DateSoumission = j.AddDays(-15)
            },
            new Bourse
            {
                Titre = "France Excellence Eiffel", Organisme = "Campus France", Zone = "FRANCE • MASTER & DOC",
                Categorie = "Master/Doctorat", Portee = "International", Avantages = "Prise en charge intégrale", NiveauCible = "Master & Doctorat",
                DateCloture = j.AddDays(95), LienOfficiel = "https://www.campusfrance.org",
                Statut = StatutPublication.Valide, DatePublication = j.AddDays(-5), DateSoumission = j.AddDays(-6)
            },
            new Bourse
            {
                Titre = "Bourse du Commonwealth", Organisme = "Commonwealth Scholarship Commission", Zone = "ROYAUME-UNI & CANADA",
                Categorie = "Coopération", Portee = "Partenariat", Avantages = "Scolarité + billet avion", NiveauCible = "Master & Doctorat", BilletInclus = true,
                DateCloture = j.AddDays(35), LienOfficiel = "https://cscuk.fcdo.gov.uk",
                Statut = StatutPublication.Valide, DatePublication = j.AddDays(-3), DateSoumission = j.AddDays(-4)
            });

        await db.SaveChangesAsync();
    }

    static async Task RafraichirDatesDemoAsync(AppDb db)
    {
        var j = DateTime.Today;

        var concours = new (string Sigle, int Cloture, int Epreuves, int Arrete, int Publication, int Soumission)[]
        {
            ("ENSPY", 18, 23, -40, -30, -31),
            ("FMSB", 26, 40, -1, -1, -2),
            ("ENAM", 60, 85, -10, -9, -10),
        };
        foreach (var d in concours)
        {
            var c = await db.Concours.FirstOrDefaultAsync(x => x.ProprietaireId == 0 && x.Sigle == d.Sigle);
            if (c == null) continue;
            c.DateCloture = j.AddDays(d.Cloture);
            c.DateEpreuves = j.AddDays(d.Epreuves);
            c.DateArrete = j.AddDays(d.Arrete);
            c.DatePublication = j.AddDays(d.Publication);
            c.DateSoumission = j.AddDays(d.Soumission);
        }

        var bourses = new (string Titre, int Cloture, int Publication, int Soumission)[]
        {
            ("Bourse d'Excellence MINESUP", 20, -14, -15),
            ("France Excellence Eiffel", 95, -5, -6),
            ("Bourse du Commonwealth", 35, -3, -4),
        };
        foreach (var d in bourses)
        {
            var b = await db.Bourses.FirstOrDefaultAsync(x => x.ProprietaireId == 0 && x.Titre == d.Titre);
            if (b == null) continue;
            b.DateCloture = j.AddDays(d.Cloture);
            b.DatePublication = j.AddDays(d.Publication);
            b.DateSoumission = j.AddDays(d.Soumission);
        }

        await db.SaveChangesAsync();
    }
}
