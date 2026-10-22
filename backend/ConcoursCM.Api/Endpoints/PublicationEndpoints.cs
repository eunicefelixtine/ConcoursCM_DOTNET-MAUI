using ConcoursCM.Api.Contracts;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Domain;
using ConcoursCM.Api.Helpers;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Endpoints;

public static class PublicationEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/publications", SoumettreAsync);
        app.MapPost("/api/publications/valider", ValiderAsync);
    }

    static TimeSpan Delai(IConfiguration config) =>
        TimeSpan.FromHours(Math.Max(0, config.GetValue<double?>("Moderation:DelaiHeures") ?? 0));

    static async Task<IResult> SoumettreAsync(PublicationRequest req, HttpContext ctx, AppDb db, IConfiguration config)
    {
        var u = await Securite.ChargerAutoriseAsync(ctx, db, req.UtilisateurId);
        if (u == null) return Results.Unauthorized();

        if (!u.PeutPublier)
            return Results.Ok(OperationResponse.Echec("Votre accréditation n'est pas encore validée. La vérification prend jusqu'à 24h ouvrées."));

        var intitule = (req.Intitule ?? "").Trim();
        if (intitule.Length < 10)
            return Results.Ok(OperationResponse.Echec("L'intitulé officiel doit contenir au moins 10 caractères."));
        if (req.DateCloture.Date < DateTime.Today.AddDays(3))
            return Results.Ok(OperationResponse.Echec("La date limite de dépôt doit être au moins 3 jours après aujourd'hui."));
        if (!req.EstBourse)
        {
            if (req.DateEpreuves.Date <= req.DateCloture.Date)
                return Results.Ok(OperationResponse.Echec("La date des épreuves doit suivre la clôture des dépôts."));
            if (req.Places <= 0 || req.Places > 100000)
                return Results.Ok(OperationResponse.Echec("Indiquez un nombre de places valide."));
            if (req.FraisFcfa < 0 || req.FraisFcfa > 200000)
                return Results.Ok(OperationResponse.Echec("Frais de quittance invalides (0 à 200 000 FCFA)."));
        }
        else if (string.IsNullOrWhiteSpace(req.Avantages))
            return Results.Ok(OperationResponse.Echec("Décrivez les avantages de la bourse."));

        var erreurLien = ReglesPublication.ValiderLien(req.Lien, u, req.EstBourse);
        if (erreurLien != null) return Results.Ok(OperationResponse.Echec(erreurLien));
        if (string.IsNullOrEmpty(req.ArretePdfBase64))
            return Results.Ok(OperationResponse.Echec("Joignez l'arrêté officiel signé (PDF)."));

        if (!req.EstBourse)
        {
            var existants = await db.Concours
                .Where(c => c.ProprietaireId == u.Id && c.DateCloture.Year == req.DateCloture.Year && c.Statut != StatutPublication.Rejete)
                .ToListAsync();
            if (existants.Any(c => Fmt.Normaliser(c.Intitule) == Fmt.Normaliser(intitule)))
                return Results.Ok(OperationResponse.Echec("Une annonce identique existe déjà pour cette année."));
        }

        var (chemin, erreurFichier) = Fichiers.EnregistrerPdf(req.ArretePdfBase64, "arretes", 8);
        if (chemin == null) return Results.Ok(OperationResponse.Echec(erreurFichier!));

        if (req.EstBourse)
        {
            db.Bourses.Add(new Bourse
            {
                Titre = intitule, Organisme = u.NomEtablissement, Zone = $"{u.NomEtablissement.ToUpperInvariant()}",
                Categorie = "Excellence", Portee = req.Portee ?? "National", Avantages = (req.Avantages ?? "").Trim(),
                NiveauCible = req.NiveauCible ?? "", DateCloture = req.DateCloture.Date, LienOfficiel = (req.Lien ?? "").Trim(),
                ArretePath = chemin, ProprietaireId = u.Id, Statut = StatutPublication.EnAttente, DateSoumission = DateTime.Now
            });
        }
        else
        {
            var concours = new Concours
            {
                Etablissement = u.NomEtablissement, Sigle = ReglesPublication.Sigle(u.NomEtablissement), Intitule = intitule,
                Categorie = "Autres", Tutelle = u.Tutelle.Split('—')[0].Trim(), DateCloture = req.DateCloture.Date,
                DateEpreuves = req.DateEpreuves.Date, Places = req.Places, FraisFcfa = req.FraisFcfa,
                LienOfficiel = (req.Lien ?? "").Trim(), ArretePath = chemin,
                ReferenceArrete = string.IsNullOrWhiteSpace(req.Reference) ? "Arrêté joint" : req.Reference.Trim(),
                DateArrete = DateTime.Today, ProprietaireId = u.Id, Statut = StatutPublication.EnAttente,
                DateSoumission = DateTime.Now
            };
            db.Concours.Add(concours);
            await db.SaveChangesAsync();

            db.PieceRequises.AddRange(
                new PieceRequise { ConcoursId = concours.Id, Ordre = 1, Libelle = "Acte de naissance certifié conforme", Detail = "Légalisé < 3 mois", Icone = "🪪" },
                new PieceRequise { ConcoursId = concours.Id, Ordre = 2, Libelle = "Diplôme ou relevé de notes certifié", Detail = "Copie certifiée conforme", Icone = "🎓" },
                new PieceRequise { ConcoursId = concours.Id, Ordre = 3, Libelle = "Quittance originale des frais", Detail = $"Reçu imprimé — {Fmt.Fcfa(req.FraisFcfa)}", Icone = "🧾" },
                new PieceRequise { ConcoursId = concours.Id, Ordre = 4, Libelle = "4 photos d'identité 4×4", Detail = "Fond blanc récent", Icone = "📷" });
        }

        await db.SaveChangesAsync();
        await Moderation.ValiderEnAttenteAsync(db, Delai(config));
        return Results.Ok(OperationResponse.Ok());
    }

    static async Task<IResult> ValiderAsync(AppDb db, IConfiguration config)
    {
        var r = await Moderation.ValiderEnAttenteAsync(db, Delai(config));
        return Results.Ok(r);
    }
}
