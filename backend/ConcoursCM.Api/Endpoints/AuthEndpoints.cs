using ConcoursCM.Api.Contracts;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Domain;
using ConcoursCM.Api.Helpers;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        var g = app.MapGroup("/api/auth");
        g.MapPost("/connexion", ConnexionAsync);
        g.MapPost("/inscription/candidat", InscrireCandidatAsync);
        g.MapPost("/inscription/institution", InscrireInstitutionAsync);
        g.MapPost("/deconnexion", DeconnexionAsync);
    }

    static async Task<IResult> ConnexionAsync(ConnexionRequest req, AppDb db)
    {
        if (!Validation.TryNormaliserIdentifiant(req.Identifiant, out var id, out _))
            return Results.Ok(AuthResponse.Echec("Identifiant invalide : saisissez un e-mail ou un numéro camerounais (6XX XXX XXX)."));
        if (string.IsNullOrEmpty(req.MotDePasse))
            return Results.Ok(AuthResponse.Echec("Saisissez votre mot de passe."));

        var verrou = Securite.VerrouActif(id);
        if (verrou != null) return Results.Ok(AuthResponse.Echec(verrou));

        var user = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Identifiant == id);
        if (user == null || !PasswordHasher.Verifier(req.MotDePasse, user.MotDePasseHash, user.Sel))
        {
            Securite.EchecConnexion(id);
            return Results.Ok(AuthResponse.Echec("Identifiant ou mot de passe incorrect."));
        }

        if (user.Role != req.RoleAttendu)
            return Results.Ok(AuthResponse.Echec(user.Role == RoleUtilisateur.Institution
                ? "Ce compte est un compte Institution : choisissez l'onglet « École / Inst. »."
                : "Ce compte est un compte Candidat : choisissez l'onglet « Candidat »."));

        Securite.ReussiteConnexion(id);
        user.Jeton = Securite.NouveauJeton();
        await db.SaveChangesAsync();
        return Results.Ok(AuthResponse.Ok(user, user.Jeton));
    }

    static async Task<IResult> InscrireCandidatAsync(CandidatRequest req, AppDb db)
    {
        if (string.IsNullOrWhiteSpace(req.Nom) || req.Nom.Trim().Length < 3)
            return Results.Ok(AuthResponse.Echec("Saisissez votre nom complet."));
        if (!Validation.TryNormaliserIdentifiant(req.Identifiant, out var id, out var estTel))
            return Results.Ok(AuthResponse.Echec("E-mail ou téléphone invalide (ex. 6XX XX XX XX)."));
        if (string.IsNullOrEmpty(req.Region))
            return Results.Ok(AuthResponse.Echec("Sélectionnez votre région de résidence."));
        var errMdp = Validation.ErreurMotDePasse(req.MotDePasse, 6, false);
        if (errMdp != null) return Results.Ok(AuthResponse.Echec(errMdp));
        if (!req.Conditions)
            return Results.Ok(AuthResponse.Echec("Vous devez accepter les conditions d'utilisation."));

        if (await db.Utilisateurs.AnyAsync(u => u.Identifiant == id))
            return Results.Ok(AuthResponse.Echec("Un compte existe déjà avec cet identifiant."));

        var (hash, sel) = PasswordHasher.Hacher(req.MotDePasse!);
        var user = new Utilisateur
        {
            Identifiant = id, NomComplet = req.Nom.Trim(), MotDePasseHash = hash, Sel = sel,
            Role = RoleUtilisateur.Candidat, Region = req.Region, SmsActif = estTel, NumeroSms = estTel ? id : "",
            Verifie = false, Jeton = Securite.NouveauJeton()
        };
        db.Utilisateurs.Add(user);
        await db.SaveChangesAsync();
        return Results.Ok(AuthResponse.Ok(user, user.Jeton!));
    }

    static async Task<IResult> InscrireInstitutionAsync(InstitutionRequest req, AppDb db)
    {
        if (string.IsNullOrWhiteSpace(req.Etablissement) || req.Etablissement.Trim().Length < 3)
            return Results.Ok(AuthResponse.Echec("Saisissez le nom officiel de l'établissement."));
        if (string.IsNullOrEmpty(req.Tutelle))
            return Results.Ok(AuthResponse.Echec("Sélectionnez la tutelle ministérielle."));
        if (string.IsNullOrWhiteSpace(req.Representant) || req.Representant.Trim().Length < 5)
            return Results.Ok(AuthResponse.Echec("Indiquez le représentant habilité (nom et fonction)."));
        if (!Validation.TryNormaliserIdentifiant(req.Email, out var mail, out var estTel) || estTel)
            return Results.Ok(AuthResponse.Echec("Saisissez un e-mail institutionnel valide."));
        if (Validation.EstEmailGrandPublic(mail))
            return Results.Ok(AuthResponse.Echec("Utilisez une adresse e-mail institutionnelle (pas Gmail, Yahoo, Hotmail…)."));
        if (!Validation.TryNormaliserIdentifiant(req.Telephone, out var tel, out var okTel) || !okTel)
            return Results.Ok(AuthResponse.Echec("Téléphone officiel invalide (+237 6XX XX XX XX)."));
        var errMdp = Validation.ErreurMotDePasse(req.MotDePasse, 8, true);
        if (errMdp != null) return Results.Ok(AuthResponse.Echec(errMdp));
        if (string.IsNullOrEmpty(req.MandatPdfBase64))
            return Results.Ok(AuthResponse.Echec("Joignez l'arrêté de nomination ou le mandat (PDF)."));
        if (!req.Certifie)
            return Results.Ok(AuthResponse.Echec("Vous devez certifier être mandaté par l'établissement."));

        if (await db.Utilisateurs.AnyAsync(u => u.Identifiant == mail))
            return Results.Ok(AuthResponse.Echec("Un compte existe déjà avec cet e-mail."));

        var (chemin, erreurFichier) = Fichiers.EnregistrerPdf(req.MandatPdfBase64, "mandats", 5);
        if (chemin == null) return Results.Ok(AuthResponse.Echec(erreurFichier!));

        var (hash, sel) = PasswordHasher.Hacher(req.MotDePasse!);
        var user = new Utilisateur
        {
            Identifiant = mail, NomComplet = req.Representant.Trim(), MotDePasseHash = hash, Sel = sel,
            Role = RoleUtilisateur.Institution, NomEtablissement = req.Etablissement.Trim(), Tutelle = req.Tutelle,
            Representant = req.Representant.Trim(), EmailInstitutionnel = mail, TelephoneOfficiel = tel,
            MandatPath = chemin, Accreditation = StatutPublication.EnAttente, Region = "", SmsActif = false,
            Jeton = Securite.NouveauJeton()
        };
        db.Utilisateurs.Add(user);
        await db.SaveChangesAsync();
        return Results.Ok(AuthResponse.Ok(user, user.Jeton!));
    }

    static async Task<IResult> DeconnexionAsync(HttpContext ctx, AppDb db)
    {
        var jeton = ctx.Request.Headers["X-Jeton"].ToString();
        if (!string.IsNullOrEmpty(jeton))
        {
            var user = await db.Utilisateurs.FirstOrDefaultAsync(u => u.Jeton == jeton);
            if (user != null)
            {
                user.Jeton = null;
                await db.SaveChangesAsync();
            }
        }
        return Results.Ok(OperationResponse.Ok());
    }
}
