using ConcoursCM.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConcoursCM.Api.Data;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Utilisateur> Utilisateurs => Set<Utilisateur>();
    public DbSet<Concours> Concours => Set<Concours>();
    public DbSet<Bourse> Bourses => Set<Bourse>();
    public DbSet<PieceRequise> PieceRequises => Set<PieceRequise>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<Favori> Favoris => Set<Favori>();
    public DbSet<Alerte> Alertes => Set<Alerte>();
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Utilisateur>().HasIndex(u => u.Identifiant).IsUnique();
        b.Entity<Utilisateur>().HasIndex(u => u.Jeton).IsUnique();

        b.Entity<PieceRequise>().HasIndex(p => p.ConcoursId);
        b.Entity<ChecklistItem>().HasIndex(i => new { i.UtilisateurId, i.ConcoursId });
        b.Entity<Favori>().HasIndex(f => new { f.UtilisateurId, f.Cible, f.CibleId }).IsUnique();
        b.Entity<Alerte>().HasIndex(a => new { a.UtilisateurId, a.Cle }).IsUnique();
        b.Entity<Alerte>().HasIndex(a => a.DateCreation);
    }
}
