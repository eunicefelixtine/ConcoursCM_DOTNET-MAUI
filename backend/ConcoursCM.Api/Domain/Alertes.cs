using ConcoursCM.Api.Data;
using ConcoursCM.Api.Helpers;
using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Domain;

/// <summary>Génération des rappels (J-7 / J-3 / J-1) et des alertes d'annonces.</summary>
public static class Alertes
{
    public static async Task<List<Alerte>> SynchroniserAsync(AppDb db, Utilisateur u, IEnumerable<int> seuils)
    {
        var creees = new List<Alerte>();
        if (u.Role != RoleUtilisateur.Candidat) return creees;

        var listeSeuils = seuils.Where(s => s > 0).Distinct().OrderBy(s => s).ToList();
        if (listeSeuils.Count == 0) return creees;

        var checklist = await db.ChecklistItems.Where(i => i.UtilisateurId == u.Id).ToListAsync();
        var favoris = await db.Favoris.Where(f => f.UtilisateurId == u.Id).ToListAsync();

        var idsConcours = checklist.Select(i => i.ConcoursId)
            .Concat(favoris.Where(f => f.Cible == TypeCible.Concours).Select(f => f.CibleId))
            .ToHashSet();
        var concours = (await db.Concours.Where(c => idsConcours.Contains(c.Id) && c.Statut == StatutPublication.Valide).ToListAsync())
            .Where(c => !c.EstClos);

        foreach (var c in concours)
        {
            var items = checklist.Where(i => i.ConcoursId == c.Id).ToList();
            var restantes = items.Count(i => !i.Coche);
            var pied = items.Count == 0 ? "Commencez votre checklist"
                : restantes == 0 ? "✓ Dossier complet" : $"Il reste {restantes} pièce(s)";

            var atteint = listeSeuils.Where(t => c.JoursRestants <= t).Cast<int?>().FirstOrDefault();
            if (atteint is not int s) continue;

            var jours = Math.Max(c.JoursRestants, 0);
            var msg = $"Date limite le {Fmt.Date(c.DateCloture)} à {c.HeureCloture.Replace(':', 'h')} — {c.LieuDepot}. " +
                      (restantes == 0 && items.Count > 0 ? "Votre dossier est prêt : pensez à le déposer." : "Vérifiez vos pièces physiques certifiées.");

            var a = await CreerSiAbsentAsync(db, new Alerte
            {
                UtilisateurId = u.Id, Cle = $"rappel-c{c.Id}-u{u.Id}-J{s}", Type = TypeAlerte.Rappel,
                Badge = jours == 0 ? "URGENT • DERNIER JOUR" : $"URGENT • J-{jours}", Urgent = jours <= 3,
                Titre = $"Clôture dépôt {c.Sigle}", Message = msg,
                PiedTexte = jours <= 3 ? $"Reste {Math.Max(jours * 24, 0)}h" : pied, ActionLibelle = "Checklist pièces",
                Cible = TypeCible.Concours, CibleId = c.Id
            }, u);
            if (a != null) creees.Add(a);
        }

        var idsBourses = favoris.Where(f => f.Cible == TypeCible.Bourse).Select(f => f.CibleId).ToHashSet();
        var bourses = (await db.Bourses.Where(b => idsBourses.Contains(b.Id) && b.Statut == StatutPublication.Valide).ToListAsync())
            .Where(b => !b.EstClose);
        foreach (var b in bourses)
        {
            var atteint = listeSeuils.Where(t => b.JoursRestants <= t).Cast<int?>().FirstOrDefault();
            if (atteint is not int s) continue;

            var a = await CreerSiAbsentAsync(db, new Alerte
            {
                UtilisateurId = u.Id, Cle = $"rappel-b{b.Id}-u{u.Id}-J{s}", Type = TypeAlerte.Bourse,
                Badge = $"BOURSE • J-{Math.Max(b.JoursRestants, 0)}", Urgent = b.JoursRestants <= 3,
                Titre = b.Titre, Message = $"Clôture le {Fmt.Date(b.DateCloture)}. Préparez vos lettres de recommandation et pièces.",
                PiedTexte = b.Organisme, ActionLibelle = "Détails bourse", Cible = TypeCible.Bourse, CibleId = b.Id
            }, u);
            if (a != null) creees.Add(a);
        }

        return creees;
    }

    public static async Task NotifierNouvelleAnnonceAsync(AppDb db, Concours c)
    {
        var candidats = await db.Utilisateurs.Where(u => u.Role == RoleUtilisateur.Candidat).ToListAsync();
        foreach (var u in candidats)
            await CreerSiAbsentAsync(db, new Alerte
            {
                UtilisateurId = u.Id, Cle = $"annonce-c{c.Id}-u{u.Id}", Type = TypeAlerte.ArreteOfficiel, Badge = "ARRÊTÉ OFFICIEL",
                Titre = $"Concours {c.Sigle} {c.DateCloture.Year}",
                Message = $"{c.Etablissement} publie : {c.Intitule}. {c.Places} places ouvertes. Clôture le {Fmt.Date(c.DateCloture)}.",
                PiedTexte = string.IsNullOrEmpty(c.ReferenceArrete) ? c.Tutelle : c.ReferenceArrete, ActionLibelle = "Consulter l'avis",
                Cible = TypeCible.Concours, CibleId = c.Id
            }, u, notifierSms: false);
    }

    public static async Task NotifierNouvelleBourseAsync(AppDb db, Bourse b)
    {
        var candidats = await db.Utilisateurs.Where(u => u.Role == RoleUtilisateur.Candidat).ToListAsync();
        foreach (var u in candidats)
            await CreerSiAbsentAsync(db, new Alerte
            {
                UtilisateurId = u.Id, Cle = $"annonce-b{b.Id}-u{u.Id}", Type = TypeAlerte.Bourse, Badge = $"BOURSE {b.Categorie.ToUpperInvariant()}",
                Titre = b.Titre, Message = $"{b.Avantages}. Clôture le {Fmt.Date(b.DateCloture)}.",
                PiedTexte = b.Organisme, ActionLibelle = "Détails bourse", Cible = TypeCible.Bourse, CibleId = b.Id
            }, u, notifierSms: false);
    }

    public static async Task<Alerte?> NotifierServiceSmsAsync(AppDb db, Utilisateur u)
    {
        if (string.IsNullOrEmpty(u.NumeroSms)) return null;
        var n = u.NumeroSms.Replace("+237", "");
        var masque = n.Length == 9 ? $"+237 {n[0]}•• •• •• {n[^2..]}" : u.NumeroSms;
        var op = Validation.Operateur(u.NumeroSms);
        return await CreerSiAbsentAsync(db, new Alerte
        {
            UtilisateurId = u.Id, Cle = $"service-sms-u{u.Id}-{u.NumeroSms}", Type = TypeAlerte.Service, Badge = "SERVICE RAPPELS ACTIF",
            Titre = "Rappels automatiques activés",
            Message = $"Rappels avant chaque clôture pour le numéro {masque}, ainsi que des notifications sur ce téléphone.",
            PiedTexte = string.IsNullOrEmpty(op) ? "Opérateur MTN / Orange" : $"Opérateur {op}", ActionLibelle = "Modifier n°",
            Cible = TypeCible.Concours, CibleId = 0
        }, u, notifierSms: false);
    }

    static async Task<Alerte?> CreerSiAbsentAsync(AppDb db, Alerte a, Utilisateur? u, bool notifierSms = true)
    {
        var existante = await db.Alertes.FirstOrDefaultAsync(x => x.Cle == a.Cle);
        if (existante != null) return existante;

        db.Alertes.Add(a);
        if (notifierSms && u is { SmsActif: true } && !string.IsNullOrEmpty(u.NumeroSms))
            db.SmsMessages.Add(new SmsMessage
            {
                Numero = u.NumeroSms,
                Message = $"ConcoursCM : {a.Titre} — {a.Message}",
                Envoye = false
            });
        await db.SaveChangesAsync();
        return a;
    }
}
