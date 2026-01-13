using NotifierAPI.Models;
using NotifierAPI.Services;
using NotifierAPI.Hubs;

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
