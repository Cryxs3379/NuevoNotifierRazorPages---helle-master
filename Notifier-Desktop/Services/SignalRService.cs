using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using NotifierDesktop.Models;

namespace NotifierDesktop.Services;

public class SignalRService : IDisposable
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
                // El payload puede venir como MessageDto o como objeto dinámico
                var dto = ConvertToMessageDto(message);
                OnNewMessage?.Invoke(dto);
            }
            catch
            {
                // Ignorar errores de deserialización
            }
        });

        _connection.On<object>("NewSentMessage", (message) =>
        {
            try
            {
                var dto = ConvertToMessageDto(message);
                OnNewSentMessage?.Invoke(dto);
            }
            catch
            {
                // Ignorar errores de deserialización
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
        // Convertir objeto dinámico a MessageDto
        if (obj is MessageDto dto)
            return dto;

        // Si viene como objeto anónimo/dinámico, usar reflexión
        var type = obj.GetType();
        var dtoResult = new MessageDto();

        // Logs de diagnóstico: obtener todas las propiedades detectadas
        var detectedProps = type.GetProperties().Select(p => p.Name).ToList();
        Debug.WriteLine($"[SignalR] ConvertToMessageDto - Detected properties: {string.Join(", ", detectedProps)}");

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
                        Debug.WriteLine($"[SignalR] Id is GUID string '{idStr}', setting Id=0");
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
            Debug.WriteLine("[SignalR] MessageAt not found, using DateTime.UtcNow");
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
                Debug.WriteLine("[SignalR] Direction not found but receivedUtc present, assuming Direction=0 (inbound)");
            }
            else
            {
                // Por defecto, asumir inbound
                dtoResult.Direction = 0;
                Debug.WriteLine("[SignalR] Direction not found, defaulting to Direction=0 (inbound)");
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

        // Logs de diagnóstico: valores mapeados
        Debug.WriteLine($"[SignalR] Mapped values - Id={dtoResult.Id}, Originator='{dtoResult.Originator}', " +
            $"Recipient='{dtoResult.Recipient}', Body='{dtoResult.Body?.Substring(0, Math.Min(50, dtoResult.Body?.Length ?? 0))}...', " +
            $"Direction={dtoResult.Direction}, MessageAt={dtoResult.MessageAt:O}, CustomerPhone='{dtoResult.CustomerPhone}'");

        return dtoResult;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopAsync().GetAwaiter().GetResult();
        _connection?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _disposed = true;
    }
}
