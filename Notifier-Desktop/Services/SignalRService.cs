using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using NotifierDesktop.Models;

namespace NotifierDesktop.Services;

public class SignalRService : IDisposable, IAsyncDisposable
{
    private HubConnection? _connection;
    private readonly string _hubUrl;
    private bool _disposed = false;

    public SignalRService(string baseUrl)
    {
        _hubUrl = $"{baseUrl.TrimEnd('/')}/hubs/messages";
    }

    public event Action<MessageDto>? OnNewMessage;
    public event Action<MessageDto>? OnNewSentMessage;
    public event Action<string>? OnDbError;
    public event Action<string>? OnEsendexDeleteError;
    public event Action? OnMissedCallsUpdated;
    public event Action? OnCallViewsUpdated;
    public event Action<object>? OnNewMissedCall;
    public event Action? OnConnected;
    public event Action? OnDisconnected;
    public event Action? OnReconnecting;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (_connection != null)
        {
            await StopAsync();
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // Configurar handlers de eventos
        _connection.On<object>("NewMessage", (message) =>
        {
            try
            {
                var dto = ConvertToMessageDto(message);
#if DEBUG
                Debug.WriteLine($"[SignalR] NewMessage parsed: phone={dto.CustomerPhone} from={dto.Originator} to={dto.Recipient} id={dto.Id} body='{dto.Body?.Substring(0, Math.Min(50, dto.Body?.Length ?? 0))}...'");
#endif
                OnNewMessage?.Invoke(dto);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[SignalR] ERROR parsing NewMessage: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[SignalR] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[SignalR] InnerException: {ex.InnerException.Message}");
                }
#endif
                // NO tragar el error silenciosamente - siempre loguear en DEBUG
            }
        });

        _connection.On<object>("NewSentMessage", (message) =>
        {
            try
            {
                var dto = ConvertToMessageDto(message);
#if DEBUG
                Debug.WriteLine($"[SignalR] NewSentMessage parsed: phone={dto.CustomerPhone} from={dto.Originator} to={dto.Recipient} id={dto.Id} body='{dto.Body?.Substring(0, Math.Min(50, dto.Body?.Length ?? 0))}...'");
#endif
                OnNewSentMessage?.Invoke(dto);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[SignalR] ERROR parsing NewSentMessage: {ex.GetType().Name}: {ex.Message}");
                Debug.WriteLine($"[SignalR] StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"[SignalR] InnerException: {ex.InnerException.Message}");
                }
#endif
                // NO tragar el error silenciosamente - siempre loguear en DEBUG
            }
        });

        _connection.On<string>("DbError", (error) =>
        {
            OnDbError?.Invoke(error);
        });

        _connection.On<string>("EsendexDeleteError", (error) =>
        {
            OnEsendexDeleteError?.Invoke(error);
        });

        _connection.On<object>("MissedCallsUpdated", (data) =>
        {
#if DEBUG
            Debug.WriteLine($"[SignalR] MissedCallsUpdated received: {data}");
#endif
            OnMissedCallsUpdated?.Invoke();
        });

        _connection.On("CallViewsUpdated", () =>
        {
#if DEBUG
            Debug.WriteLine("[SignalR] CallViewsUpdated received");
#endif
            OnCallViewsUpdated?.Invoke();
        });

        _connection.On<object>("NewMissedCall", (call) =>
        {
#if DEBUG
            Debug.WriteLine($"[SignalR] NewMissedCall received: {call}");
#endif
            OnNewMissedCall?.Invoke(call);
        });

        _connection.Reconnecting += (error) =>
        {
            OnReconnecting?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Reconnected += (connectionId) =>
        {
            OnConnected?.Invoke();
            return Task.CompletedTask;
        };

        _connection.Closed += (error) =>
        {
            OnDisconnected?.Invoke();
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync(ct);
            OnConnected?.Invoke();
        }
        catch
        {
            OnDisconnected?.Invoke();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync(ct);
            }
            catch
            {
                // Ignorar errores al detener
            }
        }
    }

    private MessageDto ConvertToMessageDto(object obj)
    {
        if (obj == null)
        {
#if DEBUG
            Debug.WriteLine("[SignalR] ConvertToMessageDto: obj is null");
#endif
            return new MessageDto();
        }

        // Convertir objeto dinámico a MessageDto
        if (obj is MessageDto dto)
        {
#if DEBUG
            Debug.WriteLine($"[SignalR] ConvertToMessageDto: obj is already MessageDto");
#endif
            return dto;
        }

        // Si viene como JsonElement, leer propiedades directamente
        if (obj is JsonElement je)
        {
#if DEBUG
            Debug.WriteLine("[SignalR] ConvertToMessageDto: obj is JsonElement");
#endif
            return ConvertFromJsonElement(je);
        }

        // Si viene como objeto anónimo/dinámico, usar reflexión
        var type = obj.GetType();
        var dtoResult = new MessageDto();

        // Logs de diagnóstico: obtener todas las propiedades detectadas
#if DEBUG
        var detectedProps = type.GetProperties().Select(p => p.Name).ToList();
        Debug.WriteLine($"[SignalR] ConvertToMessageDto - Detected properties: {string.Join(", ", detectedProps)}");
#endif

        // 1. Mapear Id (soporte string GUID y long)
        var idProp = type.GetProperty("id") ?? type.GetProperty("Id");
        if (idProp != null)
        {
            var idValue = idProp.GetValue(obj);
            if (idValue != null)
            {
                if (idValue is string idStr)
                {
                    if (long.TryParse(idStr, out var idLong))
                        dtoResult.Id = idLong;
                    else
                    {
                        // Es GUID string, poner 0 sin romper
                        dtoResult.Id = 0;
#if DEBUG
                        Debug.WriteLine($"[SignalR] Id is GUID string '{idStr}', setting Id=0");
#endif
                    }
                }
                else if (idValue is long idLong2)
                    dtoResult.Id = idLong2;
            }
        }

        // 2. Mapear Originator (soporte formato DB y Esendex)
        var originatorProp = type.GetProperty("originator") ?? type.GetProperty("Originator") 
            ?? type.GetProperty("from") ?? type.GetProperty("From");
        if (originatorProp != null)
        {
            var originatorValue = originatorProp.GetValue(obj)?.ToString();
            dtoResult.Originator = originatorValue?.Trim() ?? string.Empty;
        }

        // 3. Mapear Recipient (soporte formato DB y Esendex)
        var recipientProp = type.GetProperty("recipient") ?? type.GetProperty("Recipient")
            ?? type.GetProperty("to") ?? type.GetProperty("To");
        if (recipientProp != null)
        {
            var recipientValue = recipientProp.GetValue(obj)?.ToString();
            dtoResult.Recipient = recipientValue?.Trim() ?? string.Empty;
        }

        // 4. Mapear Body (soporte formato DB y Esendex)
        var bodyProp = type.GetProperty("body") ?? type.GetProperty("Body")
            ?? type.GetProperty("message") ?? type.GetProperty("Message");
        if (bodyProp != null)
            dtoResult.Body = bodyProp.GetValue(obj)?.ToString() ?? string.Empty;

        // 5. Mapear MessageAt (soporte formato DB y Esendex)
        var messageAtProp = type.GetProperty("messageAt") ?? type.GetProperty("MessageAt")
            ?? type.GetProperty("receivedUtc") ?? type.GetProperty("ReceivedUtc");
        if (messageAtProp != null)
        {
            var dateValue = messageAtProp.GetValue(obj);
            if (dateValue != null)
            {
                if (dateValue is DateTime dt)
                    dtoResult.MessageAt = dt;
                else if (dateValue is string dateStr && DateTime.TryParse(dateStr, out var parsedDt))
                    dtoResult.MessageAt = parsedDt;
            }
        }
        
        // Si no se obtuvo MessageAt, usar UtcNow como fallback
        if (dtoResult.MessageAt == default(DateTime))
        {
            dtoResult.MessageAt = DateTime.UtcNow;
#if DEBUG
            Debug.WriteLine("[SignalR] MessageAt not found, using DateTime.UtcNow");
#endif
        }

        // 6. Mapear Direction (con lógica de inferencia)
        var directionProp = type.GetProperty("direction") ?? type.GetProperty("Direction");
        if (directionProp != null)
        {
            var dirValue = directionProp.GetValue(obj);
            if (dirValue != null)
            {
                if (dirValue is byte dirByte)
                    dtoResult.Direction = dirByte;
                else if (int.TryParse(dirValue.ToString(), out var dirInt))
                    dtoResult.Direction = (byte)dirInt;
            }
        }
        else
        {
            // Si no viene direction, inferir según presencia de receivedUtc
            // Si hay receivedUtc, asumir inbound (Direction=0)
            var hasReceivedUtc = type.GetProperty("receivedUtc") != null || type.GetProperty("ReceivedUtc") != null;
            if (hasReceivedUtc)
            {
                dtoResult.Direction = 0; // Inbound
#if DEBUG
                Debug.WriteLine("[SignalR] Direction not found but receivedUtc present, assuming Direction=0 (inbound)");
#endif
            }
            else
            {
                // Por defecto, asumir inbound
                dtoResult.Direction = 0;
#if DEBUG
                Debug.WriteLine("[SignalR] Direction not found, defaulting to Direction=0 (inbound)");
#endif
            }
        }

        // 7. Calcular CustomerPhone AL FINAL (después de tener Direction/Originator/Recipient)
        var customerPhoneProp = type.GetProperty("customerPhone") ?? type.GetProperty("CustomerPhone");
        if (customerPhoneProp != null)
        {
            var customerPhoneValue = customerPhoneProp.GetValue(obj)?.ToString();
            dtoResult.CustomerPhone = customerPhoneValue?.Trim() ?? string.Empty;
        }
        
        // Si no viene customerPhone, calcular según Direction
        if (string.IsNullOrWhiteSpace(dtoResult.CustomerPhone))
        {
            if (dtoResult.Direction == 0)
            {
                // Inbound: CustomerPhone = Originator
                dtoResult.CustomerPhone = dtoResult.Originator;
            }
            else if (dtoResult.Direction == 1)
            {
                // Outbound: CustomerPhone = Recipient
                dtoResult.CustomerPhone = dtoResult.Recipient;
            }
            else
            {
                // Fallback si Direction no está establecido
                if (!string.IsNullOrWhiteSpace(dtoResult.Originator))
                    dtoResult.CustomerPhone = dtoResult.Originator;
                else if (!string.IsNullOrWhiteSpace(dtoResult.Recipient))
                    dtoResult.CustomerPhone = dtoResult.Recipient;
            }
        }

        // 8. Mapear SentBy (opcional, solo para OUTBOUND)
        var sentByProp = type.GetProperty("sentBy") ?? type.GetProperty("SentBy");
        if (sentByProp != null)
        {
            var sentByValue = sentByProp.GetValue(obj)?.ToString();
            dtoResult.SentBy = string.IsNullOrWhiteSpace(sentByValue) ? null : sentByValue.Trim();
        }

        // Logs de diagnóstico: valores mapeados
#if DEBUG
        Debug.WriteLine($"[SignalR] Mapped values - Id={dtoResult.Id}, Originator='{dtoResult.Originator}', " +
            $"Recipient='{dtoResult.Recipient}', Body='{dtoResult.Body?.Substring(0, Math.Min(50, dtoResult.Body?.Length ?? 0))}...', " +
            $"Direction={dtoResult.Direction}, MessageAt={dtoResult.MessageAt:O}, CustomerPhone='{dtoResult.CustomerPhone}'");
#endif

        return dtoResult;
    }

    private MessageDto ConvertFromJsonElement(JsonElement je)
    {
        var dto = new MessageDto();

        try
        {
            // 1. Mapear Id
            if (je.TryGetProperty("id", out var idElement) || je.TryGetProperty("Id", out idElement))
            {
                if (idElement.ValueKind == JsonValueKind.String)
                {
                    var idStr = idElement.GetString();
                    if (long.TryParse(idStr, out var idLong))
                        dto.Id = idLong;
                    else
                    {
                        dto.Id = 0;
#if DEBUG
                        Debug.WriteLine($"[SignalR] Id is GUID string '{idStr}', setting Id=0");
#endif
                    }
                }
                else if (idElement.ValueKind == JsonValueKind.Number)
                {
                    dto.Id = idElement.GetInt64();
                }
            }

            // 2. Mapear CustomerPhone
            if (je.TryGetProperty("customerPhone", out var customerPhoneElement) || 
                je.TryGetProperty("CustomerPhone", out customerPhoneElement))
            {
                dto.CustomerPhone = customerPhoneElement.GetString()?.Trim() ?? string.Empty;
            }

            // 3. Mapear Originator (from/originator)
            string? originator = null;
            if (je.TryGetProperty("originator", out var originatorElement) || 
                je.TryGetProperty("Originator", out originatorElement) ||
                je.TryGetProperty("from", out originatorElement) ||
                je.TryGetProperty("From", out originatorElement))
            {
                originator = originatorElement.GetString()?.Trim();
            }
            dto.Originator = originator ?? string.Empty;

            // 4. Mapear Recipient (to/recipient)
            string? recipient = null;
            if (je.TryGetProperty("recipient", out var recipientElement) || 
                je.TryGetProperty("Recipient", out recipientElement) ||
                je.TryGetProperty("to", out recipientElement) ||
                je.TryGetProperty("To", out recipientElement))
            {
                recipient = recipientElement.GetString()?.Trim();
            }
            dto.Recipient = recipient ?? string.Empty;

            // 5. Mapear Body (message/body)
            if (je.TryGetProperty("body", out var bodyElement) || 
                je.TryGetProperty("Body", out bodyElement) ||
                je.TryGetProperty("message", out bodyElement) ||
                je.TryGetProperty("Message", out bodyElement))
            {
                dto.Body = bodyElement.GetString() ?? string.Empty;
            }

            // 6. Mapear MessageAt (messageAt/receivedUtc)
            if (je.TryGetProperty("messageAt", out var messageAtElement) || 
                je.TryGetProperty("MessageAt", out messageAtElement) ||
                je.TryGetProperty("receivedUtc", out messageAtElement) ||
                je.TryGetProperty("ReceivedUtc", out messageAtElement))
            {
                if (messageAtElement.ValueKind == JsonValueKind.String)
                {
                    var dateStr = messageAtElement.GetString();
                    if (DateTime.TryParse(dateStr, out var parsedDt))
                        dto.MessageAt = parsedDt;
                }
                else if (messageAtElement.ValueKind == JsonValueKind.Number)
                {
                    // Timestamp Unix (no esperado pero por si acaso)
                    var timestamp = messageAtElement.GetInt64();
                    dto.MessageAt = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                }
            }

            // Si no se obtuvo MessageAt, usar UtcNow como fallback
            if (dto.MessageAt == default(DateTime))
            {
                dto.MessageAt = DateTime.UtcNow;
#if DEBUG
                Debug.WriteLine("[SignalR] MessageAt not found in JsonElement, using DateTime.UtcNow");
#endif
            }

            // 7. Mapear Direction
            if (je.TryGetProperty("direction", out var directionElement) || 
                je.TryGetProperty("Direction", out directionElement))
            {
                if (directionElement.ValueKind == JsonValueKind.Number)
                {
                    dto.Direction = (byte)directionElement.GetByte();
                }
                else if (directionElement.ValueKind == JsonValueKind.String)
                {
                    if (byte.TryParse(directionElement.GetString(), out var dirByte))
                        dto.Direction = dirByte;
                }
            }
            else
            {
                // Inferir direction: si hay receivedUtc, asumir inbound
                if (je.TryGetProperty("receivedUtc", out _) || je.TryGetProperty("ReceivedUtc", out _))
                {
                    dto.Direction = 0; // Inbound
#if DEBUG
                    Debug.WriteLine("[SignalR] Direction not found but receivedUtc present, assuming Direction=0 (inbound)");
#endif
                }
                else
                {
                    dto.Direction = 0; // Default inbound
#if DEBUG
                    Debug.WriteLine("[SignalR] Direction not found, defaulting to Direction=0 (inbound)");
#endif
                }
            }

            // 8. Calcular CustomerPhone si no viene
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
            {
                if (dto.Direction == 0)
                {
                    // Inbound: CustomerPhone = Originator
                    dto.CustomerPhone = dto.Originator;
                }
                else if (dto.Direction == 1)
                {
                    // Outbound: CustomerPhone = Recipient
                    dto.CustomerPhone = dto.Recipient;
                }
                else
                {
                    // Fallback
                    if (!string.IsNullOrWhiteSpace(dto.Originator))
                        dto.CustomerPhone = dto.Originator;
                    else if (!string.IsNullOrWhiteSpace(dto.Recipient))
                        dto.CustomerPhone = dto.Recipient;
                }
            }

            // 9. Mapear SentBy (opcional, solo para OUTBOUND)
            if (je.TryGetProperty("sentBy", out var sentByElement) || 
                je.TryGetProperty("SentBy", out sentByElement))
            {
                var sentByValue = sentByElement.GetString();
                dto.SentBy = string.IsNullOrWhiteSpace(sentByValue) ? null : sentByValue.Trim();
            }

            // Log si no se pudo obtener phone
#if DEBUG
            if (string.IsNullOrWhiteSpace(dto.CustomerPhone))
            {
                Debug.WriteLine($"[SignalR] WARNING: Could not determine CustomerPhone from JsonElement. " +
                    $"Originator='{dto.Originator}', Recipient='{dto.Recipient}', Direction={dto.Direction}");
            }

            Debug.WriteLine($"[SignalR] JsonElement mapped - Id={dto.Id}, CustomerPhone='{dto.CustomerPhone}', " +
                $"Originator='{dto.Originator}', Recipient='{dto.Recipient}', Body='{dto.Body?.Substring(0, Math.Min(50, dto.Body?.Length ?? 0))}...', " +
                $"Direction={dto.Direction}, MessageAt={dto.MessageAt:O}");
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[SignalR] ERROR in ConvertFromJsonElement: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"[SignalR] StackTrace: {ex.StackTrace}");
#endif
            // Retornar DTO parcial en lugar de fallar completamente
        }

        return dto;
    }

    /// <summary>
    /// Dispose sincrónico. Para evitar deadlocks, se recomienda usar DisposeAsync en lugar de este método.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        // Usar Task.Run para evitar deadlocks potenciales al esperar async en dispose sincrónico
        try
        {
            Task.Run(async () =>
            {
                await StopAsync();
                if (_connection != null)
                    await _connection.DisposeAsync();
            }).GetAwaiter().GetResult();
        }
        catch
        {
            // Ignorar excepciones durante dispose
        }
        
        _disposed = true;
    }

    /// <summary>
    /// Dispose asíncrono recomendado para evitar deadlocks.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await StopAsync();
            if (_connection != null)
                await _connection.DisposeAsync();
        }
        catch
        {
            // Ignorar excepciones durante dispose
        }
        
        _disposed = true;
    }
}
