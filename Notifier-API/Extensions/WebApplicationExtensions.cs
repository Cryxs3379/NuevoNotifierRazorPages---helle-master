using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Models;
using NotifierAPI.Services;
using NotifierAPI.Hubs;
using NotifierAPI.Data;
using NotifierAPI.Configuration;
using NotifierAPI.Helpers;
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
                    receivedUtc = message.MessageAt.ToString("O"), // ISO8601
                    sentBy = message.SentBy // Incluir SentBy
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
                        messageAt = m.MessageAt,
                        sentBy = m.SentBy // Incluir SentBy (opcional, nullable)
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

                // Normalizar phone para tolerar con/sin '+'
                var phoneNormalized = phone.Trim().Replace(" ", "");
                var phoneNoPlus = phoneNormalized.StartsWith("+") 
                    ? phoneNormalized.Substring(1) 
                    : phoneNormalized;
                var phonePlus = "+" + phoneNoPlus;

                // LOGS DE DIAGNÓSTICO (PII enmascarado para Information/Warning)
                app.Logger.LogInformation("[GetConversationMessages] Input phone: '{Phone}', Normalized: '{Normalized}', NoPlus: '{NoPlus}', Plus: '{Plus}'", 
                    LoggingHelpers.MaskPhone(phone), LoggingHelpers.MaskPhone(phoneNormalized), LoggingHelpers.MaskPhone(phoneNoPlus), LoggingHelpers.MaskPhone(phonePlus));

                // Verificar qué formatos hay realmente en la BD para este número (muestra de diagnóstico)
                var sampleInbound = await dbContext.SmsMessages.AsNoTracking()
                    .Where(m => m.Direction == 0 && (m.Originator.Contains(phoneNoPlus) || m.Originator.Contains(phonePlus)))
                    .Select(m => m.Originator)
                    .FirstOrDefaultAsync(ct);
                
                var sampleOutbound = await dbContext.SmsMessages.AsNoTracking()
                    .Where(m => m.Direction == 1 && (m.Recipient.Contains(phoneNoPlus) || m.Recipient.Contains(phonePlus)))
                    .Select(m => m.Recipient)
                    .FirstOrDefaultAsync(ct);

                app.Logger.LogInformation("[GetConversationMessages] Sample Inbound Originator in DB: '{Originator}', Sample Outbound Recipient in DB: '{Recipient}'", 
                    LoggingHelpers.MaskPhone(sampleInbound ?? "NOT FOUND"), LoggingHelpers.MaskPhone(sampleOutbound ?? "NOT FOUND"));

                // Mensajes entre empresa y ese phone (tolerante a formato):
                // - Inbound: Direction=0 AND (Originator==phoneNoPlus OR Originator==phonePlus)
                // - Outbound: Direction=1 AND (Recipient==phoneNoPlus OR Recipient==phonePlus)
                var items = await dbContext.SmsMessages.AsNoTracking()
                    .Where(m => 
                        (m.Direction == 0 && (m.Originator == phoneNoPlus || m.Originator == phonePlus)) ||
                        (m.Direction == 1 && (m.Recipient == phoneNoPlus || m.Recipient == phonePlus)))
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
                        messageAt = m.MessageAt,
                        sentBy = m.SentBy // Incluir SentBy en la respuesta
                    })
                    .ToListAsync(ct);

                app.Logger.LogInformation("[GetConversationMessages] Found {Count} messages for phone '{Phone}' (searched as '{NoPlus}' and '{Plus}')", 
                    items.Count, LoggingHelpers.MaskPhone(phone), LoggingHelpers.MaskPhone(phoneNoPlus), LoggingHelpers.MaskPhone(phonePlus));

                if (items.Count == 0)
                {
                    app.Logger.LogWarning("[GetConversationMessages] No messages found. This could indicate a format mismatch. " +
                        "Check if Originator/Recipient in DB match the search patterns. " +
                        "Sample Inbound Originator: '{Originator}', Sample Outbound Recipient: '{Recipient}'", 
                        LoggingHelpers.MaskPhone(sampleInbound ?? "NOT FOUND"), LoggingHelpers.MaskPhone(sampleOutbound ?? "NOT FOUND"));
                }

                return Results.Ok(items);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error obteniendo conversación para {Phone}", LoggingHelpers.MaskPhone(phone));
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
            NotificationsDbContext dbContext,
            CancellationToken ct) =>
        {
            try
            {
                if (body == null || string.IsNullOrWhiteSpace(body.To) || string.IsNullOrWhiteSpace(body.Message))
                {
                    return Results.BadRequest(new { error = "to y message son requeridos" });
                }

                // Normalizar número telefónico antes de validar
                var toNormalized = body.To.Trim().Replace(" ", "");
                
                // Si empieza con "00", convertir a "+"
                if (toNormalized.StartsWith("00") && toNormalized.Length > 2)
                {
                    toNormalized = "+" + toNormalized.Substring(2);
                }
                
                // Si no empieza con '+', añadirlo
                if (!toNormalized.StartsWith("+"))
                {
                    toNormalized = "+" + toNormalized;
                }

                // Validar formato E.164 después de normalizar
                if (!System.Text.RegularExpressions.Regex.IsMatch(toNormalized, @"^\+\d{6,15}$"))
                {
                    return Results.BadRequest(new { error = "to debe ser un número telefónico válido (formato E.164: +XXXXXXXX)" });
                }

                // Enviar SMS con número normalizado
                var sendResult = await sendService.SendAsync(
                    toNormalized, 
                    body.Message, 
                    esendexSettings.AccountReference, 
                    ct);

                // Guardar en SQL (usar número normalizado con '+')
                var originator = esendexSettings.AccountReference ?? "UNKNOWN";
                var savedId = await smsRepository.SaveSentAsync(
                    originator: originator,
                    recipient: toNormalized, // Usar número normalizado
                    body: body.Message,
                    type: "SMS",
                    messageAt: sendResult.SubmittedUtc,
                    sentBy: body.SentBy, // Nombre del recepcionista (opcional)
                    cancellationToken: ct);

                // Emitir SignalR si se guardó correctamente
                if (savedId.HasValue)
                {
                    try
                    {
                        // Normalizar customerPhone al formato canónico (sin '+') para ConversationState
                        // toNormalized tiene '+' (E.164), pero customerPhone debe ser sin '+' para consistencia
                        string customerPhoneForSignalR;
                        try
                        {
                            customerPhoneForSignalR = PhoneNormalizer.NormalizePhone(toNormalized);
                            if (toNormalized != customerPhoneForSignalR)
                            {
                                app.Logger.LogDebug("Normalized customerPhone in SignalR NewSentMessage: '{Original}' -> '{Normalized}'", 
                                    toNormalized, customerPhoneForSignalR);
                            }
                        }
                        catch (Exception normEx)
                        {
                            app.Logger.LogWarning(normEx, "Failed to normalize customerPhone for SignalR: To={To}", LoggingHelpers.MaskPhone(toNormalized));
                            // Usar el original si falla la normalización
                            customerPhoneForSignalR = toNormalized;
                        }
                        
                        // Obtener SentBy del mensaje guardado para incluirlo en SignalR
                        var savedMessage = await dbContext.SmsMessages.FindAsync(new object[] { savedId.Value }, ct);
                        var sentBy = savedMessage?.SentBy;
                        
                        await hubContext.Clients.All.SendAsync("NewSentMessage", new
                        {
                            id = savedId.Value.ToString(),
                            customerPhone = customerPhoneForSignalR, // Para outbound, customerPhone = Recipient (normalizado sin '+')
                            originator = originator,
                            recipient = toNormalized, // Mantener formato E.164 con '+' para recipient
                            body = body.Message,
                            direction = 1,
                            messageAt = sendResult.SubmittedUtc.ToString("O"),
                            sentBy = sentBy // Incluir SentBy si está disponible (opcional, no rompe clientes antiguos)
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

        // Conversations endpoints
        app.MapGet("/api/v1/conversations", async (
            string? q,
            int? page,
            int? pageSize,
            NotificationsDbContext dbContext,
            CancellationToken ct) =>
        {
            try
            {
                var p = page ?? 1;
                if (p < 1) p = 1;

                var ps = pageSize ?? 50;
                if (ps < 10) ps = 10;
                if (ps > 200) ps = 200;

                // Query base de ConversationState
                var query = dbContext.ConversationStates.AsNoTracking();

                // Filtro por teléfono si viene (normalizar antes de buscar)
                if (!string.IsNullOrWhiteSpace(q))
                {
                    try
                    {
                        var normalizedQ = PhoneNormalizer.NormalizePhone(q);
                        query = query.Where(cs => cs.CustomerPhone.Contains(normalizedQ));
                    }
                    catch (ArgumentException)
                    {
                        // Si no se puede normalizar, buscar sin normalizar (para compatibilidad)
                        query = query.Where(cs => cs.CustomerPhone.Contains(q));
                    }
                }

                // Calcular campos calculados y preview
                var conversationsQuery = query.Select(cs => new
                {
                    cs.CustomerPhone,
                    cs.LastInboundAt,
                    cs.LastOutboundAt,
                    cs.LastReadInboundAt,
                    cs.AssignedTo,
                    cs.AssignedUntil,
                    // Calcular pendingReply y unread según reglas
                    PendingReply = cs.LastInboundAt != null && 
                        (cs.LastOutboundAt == null || cs.LastOutboundAt < cs.LastInboundAt),
                    Unread = cs.LastInboundAt != null && 
                        (cs.LastReadInboundAt == null || cs.LastReadInboundAt < cs.LastInboundAt),
                    // lastMessageAt = MAX(LastInboundAt, LastOutboundAt)
                    LastMessageAt = cs.LastInboundAt.HasValue && cs.LastOutboundAt.HasValue
                        ? (cs.LastInboundAt > cs.LastOutboundAt ? cs.LastInboundAt : cs.LastOutboundAt)
                        : (cs.LastInboundAt ?? cs.LastOutboundAt)
                });

                // Contar total
                var total = await conversationsQuery.CountAsync(ct);

                // Obtener conversaciones paginadas
                var conversations = await conversationsQuery
                    .OrderByDescending(c => c.LastMessageAt)
                    .ThenByDescending(c => c.CustomerPhone)
                    .Skip((p - 1) * ps)
                    .Take(ps)
                    .ToListAsync(ct);

                // Obtener preview para cada conversación (último mensaje desde NotifierSmsMessages)
                var phoneList = conversations.Select(c => c.CustomerPhone).ToList();
                // Obtener preview para cada conversación (tolerante a formato con/sin '+')
                var previews = new Dictionary<string, string>();
                var lastRespondedBy = new Dictionary<string, string?>();
                
                foreach (var phone in phoneList)
                {
                    // Normalizar phone para buscar en ambos formatos
                    var phoneNoPlus = phone.StartsWith("+") ? phone.Substring(1) : phone;
                    var phonePlus = "+" + phoneNoPlus;
                    
                    // Obtener preview (último mensaje de cualquier dirección)
                    var previewMessage = await dbContext.SmsMessages.AsNoTracking()
                        .Where(m => 
                            (m.Direction == 0 && (m.Originator == phoneNoPlus || m.Originator == phonePlus)) ||
                            (m.Direction == 1 && (m.Recipient == phoneNoPlus || m.Recipient == phonePlus)))
                        .OrderByDescending(m => m.MessageAt)
                        .ThenByDescending(m => m.Id)
                        .Select(m => m.Body)
                        .FirstOrDefaultAsync(ct);
                    
                    previews[phone] = previewMessage ?? string.Empty;
                    
                    // Obtener último SentBy (quién respondió por última vez) del último mensaje OUTBOUND
                    var lastOutboundSentBy = await dbContext.SmsMessages.AsNoTracking()
                        .Where(m => m.Direction == 1 && // Solo OUTBOUND
                            (m.Recipient == phoneNoPlus || m.Recipient == phonePlus))
                        .OrderByDescending(m => m.MessageAt)
                        .ThenByDescending(m => m.Id)
                        .Select(m => m.SentBy)
                        .FirstOrDefaultAsync(ct);
                    
                    lastRespondedBy[phone] = string.IsNullOrWhiteSpace(lastOutboundSentBy) ? null : lastOutboundSentBy;
                }

                // Construir respuesta
                var items = conversations.Select(c => new
                {
                    customerPhone = c.CustomerPhone,
                    lastInboundAt = c.LastInboundAt,
                    lastOutboundAt = c.LastOutboundAt,
                    lastReadInboundAt = c.LastReadInboundAt,
                    pendingReply = c.PendingReply,
                    unread = c.Unread,
                    assignedTo = c.AssignedTo,
                    assignedUntil = c.AssignedUntil,
                    lastMessageAt = c.LastMessageAt,
                    preview = previews.GetValueOrDefault(c.CustomerPhone, string.Empty),
                    lastRespondedBy = lastRespondedBy.GetValueOrDefault(c.CustomerPhone, null)
                }).ToList();

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
                app.Logger.LogError(ex, "Error obteniendo conversaciones");
                return Results.Problem(statusCode: 500, title: "Error al obtener las conversaciones");
            }
        })
        .WithName("GetConversations")
        .WithTags("Conversations");

        app.MapPost("/api/v1/conversations/{customerPhone}/read", async (
            string customerPhone,
            ConversationStateService conversationStateService,
            NotificationsDbContext dbContext,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customerPhone))
                    return Results.BadRequest(new { error = "customerPhone es requerido" });

                // Normalizar phone al formato canónico (sin '+')
                var normalizedPhone = PhoneNormalizer.NormalizePhone(customerPhone);
                
                // MarkReadAsync ya normaliza internamente, pero normalizamos aquí también para consistencia
                await conversationStateService.MarkReadAsync(normalizedPhone, ct);

                // Obtener estado actualizado (usar teléfono normalizado)
                var state = await dbContext.ConversationStates.AsNoTracking()
                    .FirstOrDefaultAsync(cs => cs.CustomerPhone == normalizedPhone, ct);

                if (state == null)
                    return Results.NotFound(new { error = "Conversación no encontrada después de actualizar" });

                return Results.Ok(new
                {
                    customerPhone = state.CustomerPhone,
                    lastInboundAt = state.LastInboundAt,
                    lastOutboundAt = state.LastOutboundAt,
                    lastReadInboundAt = state.LastReadInboundAt,
                    pendingReply = state.LastInboundAt != null && 
                        (state.LastOutboundAt == null || state.LastOutboundAt < state.LastInboundAt),
                    unread = state.LastInboundAt != null && 
                        (state.LastReadInboundAt == null || state.LastReadInboundAt < state.LastInboundAt),
                    assignedTo = state.AssignedTo,
                    assignedUntil = state.AssignedUntil
                });
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error marcando conversación como leída: {Phone}", LoggingHelpers.MaskPhone(customerPhone));
                return Results.Problem(statusCode: 500, title: "Error al marcar la conversación como leída");
            }
        })
        .WithName("MarkConversationRead")
        .WithTags("Conversations");

        app.MapPost("/api/v1/conversations/{customerPhone}/claim", async (
            string customerPhone,
            HttpRequest request,
            ConversationStateService conversationStateService,
            NotificationsDbContext dbContext,
            CancellationToken ct) =>
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customerPhone))
                    return Results.BadRequest(new { error = "customerPhone es requerido" });

                var body = await request.ReadFromJsonAsync<ClaimRequestDto>(ct);
                if (body == null || string.IsNullOrWhiteSpace(body.OperatorName))
                {
                    return Results.BadRequest(new { error = "operatorName es requerido" });
                }

                // Normalizar phone al formato canónico (sin '+')
                var normalizedPhone = PhoneNormalizer.NormalizePhone(customerPhone);

                var minutes = body.Minutes > 0 ? body.Minutes : 5; // Default 5 minutos

                // ClaimAsync ya normaliza internamente, pero normalizamos aquí también para consistencia
                var result = await conversationStateService.ClaimAsync(
                    normalizedPhone, 
                    body.OperatorName, 
                    minutes, 
                    ct);

                return Results.Ok(new
                {
                    success = result.Success,
                    wasAlreadyAssigned = result.WasAlreadyAssigned,
                    assignedTo = result.AssignedTo,
                    assignedUntil = result.AssignedUntil
                });
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error asignando conversación: {Phone}", LoggingHelpers.MaskPhone(customerPhone));
                return Results.Problem(statusCode: 500, title: "Error al asignar la conversación");
            }
        })
        .WithName("ClaimConversation")
        .WithTags("Conversations");

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

        // Internal endpoint: Notify new calls (from Notifier-APiCalls)
        app.MapPost("/api/v1/internal/calls/notify", async (
            HttpRequest request,
            IHubContext<MessagesHub> hubContext,
            IConfiguration configuration,
            CancellationToken ct) =>
        {
            try
            {
                // Validar ApiKey
                var expectedApiKey = configuration["NotifierApi:InternalApiKey"] ?? 
                                    configuration["InternalApiKey"] ?? 
                                    "notifier-internal-key-2024";
                
                if (!request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) ||
                    apiKeyHeader.ToString() != expectedApiKey)
                {
                    app.Logger.LogWarning("Intento de acceso al endpoint interno sin ApiKey válido desde IP: {RemoteIp}",
                        request.HttpContext.Connection.RemoteIpAddress);
                    return Results.Unauthorized();
                }

                // Leer payload
                var payload = await request.ReadFromJsonAsync<CallsNotifyPayload>(ct);
                if (payload == null)
                {
                    return Results.BadRequest(new { error = "Payload inválido" });
                }

                app.Logger.LogInformation("Notificación de nuevas llamadas recibida. NewCount: {NewCount}, MaxId: {MaxId}",
                    payload.NewCount, payload.MaxId);

                // Emitir SignalR
                await hubContext.Clients.All.SendAsync("CallsUpdated", new
                {
                    newCount = payload.NewCount,
                    maxId = payload.MaxId,
                    latestAtUtc = payload.LatestAtUtc
                }, ct);

                return Results.Ok(new { success = true, message = "Notificación enviada" });
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "Error procesando notificación de llamadas");
                return Results.Problem(statusCode: 500, title: "Error al procesar la notificación");
            }
        })
        .WithName("NotifyNewCalls")
        .WithTags("Internal");

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

/// <summary>
/// Payload para notificación de nuevas llamadas desde Notifier-APiCalls
/// </summary>
internal class CallsNotifyPayload
{
    public int NewCount { get; set; }
    public long MaxId { get; set; }
    public string LatestAtUtc { get; set; } = string.Empty;
}
