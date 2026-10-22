using ConcoursCM.Api.Contracts;
using ConcoursCM.Api.Data;
using ConcoursCM.Api.Models;

namespace ConcoursCM.Api.Endpoints;

/// <summary>
/// Passerelle SMS simulée : les messages sont stockés dans une boîte d'envoi.
/// Brancher ici un agrégateur (MTN / Orange / Nexmo…) pour un envoi réel.
/// </summary>
public static class SmsEndpoints
{
    public static void Map(WebApplication app) => app.MapPost("/api/sms", EnvoyerAsync);

    static async Task<IResult> EnvoyerAsync(SmsRequest req, AppDb db)
    {
        if (string.IsNullOrWhiteSpace(req.Numero) || string.IsNullOrWhiteSpace(req.Message))
            return Results.BadRequest(new SmsResponse(false));

        db.SmsMessages.Add(new SmsMessage
        {
            Numero = req.Numero.Trim(),
            Message = req.Message.Trim(),
            Envoye = true
        });
        await db.SaveChangesAsync();
        return Results.Ok(new SmsResponse(true));
    }
}
