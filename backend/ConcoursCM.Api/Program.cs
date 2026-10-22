using System.Text.Json.Serialization;
using System.IO.Compression;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Domain;
using ConcoursCM.Api.Endpoints;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var cheminDb = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(cheminDb))
    cheminDb = "Data Source=" + Path.Combine(builder.Environment.ContentRootPath, "concourscm.db");

builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(cheminDb));

// Les réponses JSON pèsent quelques dizaines de Ko ; sur réseau 2G/3G la compression
// réduit le transfert de 70 à 90 % et accélère nettement l'affichage des listes.
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = new[] { "application/json" };
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = null;
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.UseResponseCompression();

Fichiers.RacineStockage = Path.Combine(app.Environment.ContentRootPath, "storage");
Directory.CreateDirectory(Fichiers.RacineStockage);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.EnsureCreatedAsync();
    await Seed.ExecuterAsync(db);
}

app.MapGet("/", () => Results.Ok(new { service = "ConcoursCM API", version = "1.0", statut = "ok" }));
app.MapGet("/api/sante", () => Results.Ok(new { statut = "ok", heure = DateTime.Now }));

AuthEndpoints.Map(app);
CatalogueEndpoints.Map(app);
UtilisateurEndpoints.Map(app);
PublicationEndpoints.Map(app);
SmsEndpoints.Map(app);

app.Run();
