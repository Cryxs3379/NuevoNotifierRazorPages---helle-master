using Microsoft.EntityFrameworkCore;
using NotifierAPI.Models;
using NotifierAPI.Services;
using NotifierAPI.Hubs;
using NotifierAPI.Data;

namespace NotifierAPI.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Mapea los endpoints de la API v1
    /// </summary>
    public static WebApplication MapApiV1Endpoints(this WebApplication app)
    {
        // Health Check
        app.MapGet("/api/v1/health", (IInboxService inboxService) =>
        {
            return Results.Ok(new HealthResponse
            {
                Status = "ok",
                EsendexConfigured = inboxService.IsConfigured()
            });
        })
        .WithName("GetHealth")
        .WithTags("Health");

        // Messages endpoints
        app.MapGet("/api/v1/messages", async (
            string? direction,
            int? page,
            int? pageSize,
            string? accountRef,
            IInboxService inbox,
            CancellationToken ct) =>
        {
            try
            {
                var dir = direction ?? "inbound";
                if (dir != "inbound" && dir != "outbound") dir = "inbound";

                var p = page ?? 1;
                if (p < 1) p = 1;

                var ps = pageSize ?? 25;
                if (ps < 10) ps = 10;
                if (ps > 200) ps = 200;

                var response = await inbox.GetMessagesAsync(dir, p, ps, accountRef, ct);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem(statusCode: 401, title: "Error de autenticación con Esendex");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo lista de mensajes");
                return Results.Problem(statusCode: 500, title: "Error al obtener los mensajes");
            }
        })
        .WithName("GetMessages")
        .WithTags("Messages");

        app.MapGet("/api/v1/messages/{id}", async (string id, IInboxService inbox, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest(new { error = "Id requerido" });

            try
            {
                var message = await inbox.GetMessageByIdAsync(id, ct);
                if (message == null)
                    return Results.NotFound(new { error = "Mensaje no encontrado" });

                return Results.Ok(message);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem(statusCode: 401, title: "Error de autenticación con Esendex");
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo mensaje {Id}", id);
                return Results.Problem(statusCode: 500, title: "Error al obtener el mensaje");
            }
        })
        .WithName("GetMessageById")
        .WithTags("Messages");

        app.MapDelete("/api/v1/messages/{id}", async (string id, IInboxService inbox, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(id))
                return Results.BadRequest(new { error = "Id requerido" });

            var ok = await inbox.DeleteMessageAsync(id, ct);
            return ok
                ? Results.NoContent()
                : Results.Problem(statusCode: 502, title: "No se pudo eliminar en Esendex");
        })
        .WithName("DeleteMessage")
        .WithTags("Messages");

        // DB-first message endpoint (para modal "Ver")
        app.MapGet("/api/v1/db/messages/{id:long}", async (long id, NotificationsDbContext dbContext, CancellationToken ct) =>
        {
            try
            {
                var message = await dbContext.SmsMessages.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id, ct);

                if (message == null)
                    return Results.NotFound(new { error = "Mensaje no encontrado" });

                // Mapear al formato que espera el JS del modal
                return Results.Ok(new
                {
                    from = message.Originator,
                    to = message.Recipient,
                    message = message.Body,
                    receivedUtc = message.MessageAt.ToString("O") // ISO8601
                });
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo mensaje {Id} desde SQL", id);
                return Results.Problem(statusCode: 500, title: "Error al obtener el mensaje desde la base de datos");
            }
        })
        .WithName("GetDbMessageById")
        .WithTags("Messages");

        // Calls endpoints
        app.MapGet("/api/v1/calls/missed", async (int? limit, IMissedCallsService callsService, CancellationToken ct) =>
        {
            try
            {
                var lmt = limit ?? 100;
                if (lmt < 10) lmt = 10;
                if (lmt > 500) lmt = 500;

                var response = await callsService.GetMissedCallsAsync(lmt, ct);
                if (response == null)
                {
                    return Results.Problem(statusCode: 502, title: "No se pudieron obtener las llamadas perdidas");
                }

                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo llamadas perdidas");
                return Results.Problem(statusCode: 500, title: "Error al obtener las llamadas perdidas");
            }
        })
        .WithName("GetMissedCalls")
        .WithTags("Calls");

        app.MapGet("/api/v1/calls/stats", async (IMissedCallsService callsService, CancellationToken ct) =>
        {
            try
            {
                var stats = await callsService.GetMissedCallsStatsAsync(ct);
                if (stats == null)
                {
                    return Results.Problem(statusCode: 502, title: "No se pudieron obtener las estadísticas");
                }

                return Results.Ok(stats);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo estadísticas de llamadas perdidas");
                return Results.Problem(statusCode: 500, title: "Error al obtener las estadísticas");
            }
        })
        .WithName("GetMissedCallsStats")
        .WithTags("Calls");

        return app;
    }

    /// <summary>
    /// Mapea los endpoints de Razor Pages
    /// </summary>
    public static WebApplication MapRazorPagesEndpoints(this WebApplication app)
    {
        app.MapRazorPages();
        return app;
    }

    /// <summary>
    /// Mapea los hubs de SignalR
    /// </summary>
    public static WebApplication MapSignalRHubs(this WebApplication app)
    {
        app.MapHub<MessagesHub>("/hubs/messages");
        return app;
    }
}
