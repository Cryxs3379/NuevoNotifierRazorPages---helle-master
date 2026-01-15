using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Models;
using NotifierAPI.Services;
using NotifierAPI.Hubs;
using NotifierAPI.Data;
using NotifierAPI.Configuration;
using System.Text.RegularExpressions;

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

        // DB-first list endpoint (para WinForms)
        app.MapGet("/api/v1/db/messages", async (
            int direction,
            int? page,
            int? pageSize,
            string? phone,
            NotificationsDbContext dbContext,
            CancellationToken ct) =>
        {
            try
            {
                // Validar direction
                if (direction != 0 && direction != 1)
                    return Results.BadRequest(new { error = "direction debe ser 0 (inbound) o 1 (outbound)" });

                // Validar y normalizar paginación
                var p = page ?? 1;
                if (p < 1) p = 1;

                var ps = pageSize ?? 50;
                if (ps < 10) ps = 10;
                if (ps > 200) ps = 200;

                // Construir query base
                var query = dbContext.SmsMessages.AsNoTracking()
                    .Where(m => m.Direction == direction);

                // Aplicar filtro por teléfono si viene
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    if (direction == 0) // inbound: filtrar por Originator
                    {
                        query = query.Where(m => m.Originator.Contains(phone));
                    }
                    else // outbound: filtrar por Recipient
                    {
                        query = query.Where(m => m.Recipient.Contains(phone));
                    }
                }

                // Contar total
                var total = await query.CountAsync(ct);

                // Aplicar orden y paginación
                var items = await query
                    .OrderByDescending(m => m.MessageAt)
                    .ThenByDescending(m => m.Id)
                    .Skip((p - 1) * ps)
                    .Take(ps)
                    .Select(m => new
                    {
                        id = m.Id,
                        originator = m.Originator,
                        recipient = m.Recipient,
                        body = m.Body,
                        type = m.Type,
                        direction = m.Direction,
                        messageAt = m.MessageAt
                    })
                    .ToListAsync(ct);

                return Results.Ok(new
                {
                    items,
                    page = p,
                    pageSize = ps,
                    total
                });
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo mensajes desde SQL");
                return Results.Problem(statusCode: 500, title: "Error al obtener los mensajes desde la base de datos");
            }
        })
        .WithName("GetDbMessages")
        .WithTags("Messages");

        // DB-first conversation endpoint (para WinForms)
        app.MapGet("/api/v1/db/conversations/{phone}/messages", async (
            string phone,
            int? take,
            NotificationsDbContext dbContext,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(phone))
                    return Results.BadRequest(new { error = "phone es requerido" });

                var t = take ?? 200;
                if (t < 1) t = 200;
                if (t > 500) t = 500;

                // Mensajes entre empresa y ese phone:
                // - Inbound: Direction=0 AND Originator==phone
                // - Outbound: Direction=1 AND Recipient==phone
                var items = await dbContext.SmsMessages.AsNoTracking()
                    .Where(m => 
                        (m.Direction == 0 && m.Originator == phone) ||
                        (m.Direction == 1 && m.Recipient == phone))
                    .OrderBy(m => m.MessageAt) // ASC para orden natural del chat
                    .ThenBy(m => m.Id)
                    .Take(t)
                    .Select(m => new
                    {
                        id = m.Id,
                        originator = m.Originator,
                        recipient = m.Recipient,
                        body = m.Body,
                        direction = m.Direction,
                        messageAt = m.MessageAt
                    })
                    .ToListAsync(ct);

                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo conversación para {Phone}", phone);
                return Results.Problem(statusCode: 500, title: "Error al obtener la conversación");
            }
        })
        .WithName("GetConversationMessages")
        .WithTags("Messages");

        // DB-first send endpoint (para WinForms)
        app.MapPost("/api/v1/db/messages/send", async (
            SendMessageRequest body,
            ISendService sendService,
            SmsMessageRepository smsRepository,
            IHubContext<MessagesHub> hubContext,
            EsendexSettings esendexSettings,
            CancellationToken ct) =>
        {
            try
            {
                if (body == null || string.IsNullOrWhiteSpace(body.To) || string.IsNullOrWhiteSpace(body.Message))
                {
                    return Results.BadRequest(new { error = "to y message son requeridos" });
                }

                // Validar formato E.164
                if (!System.Text.RegularExpressions.Regex.IsMatch(body.To, @"^\+\d{6,15}$"))
                {
                    return Results.BadRequest(new { error = "to debe estar en formato E.164 (+XXXXXXXX)" });
                }

                // Enviar SMS
                var sendResult = await sendService.SendAsync(
                    body.To, 
                    body.Message, 
                    esendexSettings.AccountReference, 
                    ct);

                // Guardar en SQL
                var originator = esendexSettings.AccountReference ?? "UNKNOWN";
                var savedId = await smsRepository.SaveSentAsync(
                    originator: originator,
                    recipient: body.To,
                    body: body.Message,
                    type: "SMS",
                    messageAt: sendResult.SubmittedUtc,
                    cancellationToken: ct);

                // Emitir SignalR si se guardó correctamente
                if (savedId.HasValue)
                {
                    try
                    {
                        await hubContext.Clients.All.SendAsync("NewSentMessage", new
                        {
                            id = savedId.Value.ToString(),
                            originator = originator,
                            recipient = body.To,
                            body = body.Message,
                            direction = 1,
                            messageAt = sendResult.SubmittedUtc.ToString("O")
                        }, ct);
                    }
                    catch (Exception signalREx)
                    {
                        app.Logger.LogWarning(signalREx, "Failed to emit NewSentMessage event via SignalR");
                    }
                }

                return Results.Ok(new
                {
                    success = true,
                    id = savedId
                });
            }
            catch (ArgumentException ex)
            {
                app.Logger.LogWarning(ex, "Invalid request to send message");
                return Results.BadRequest(new { error = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                app.Logger.LogError(ex, "Failed to send SMS");
                return Results.Problem(statusCode: 502, title: "Error al enviar SMS", detail: ex.Message);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Unexpected error sending message");
                return Results.Problem(statusCode: 500, title: "Error inesperado al enviar mensaje");
            }
        })
        .WithName("SendDbMessage")
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
