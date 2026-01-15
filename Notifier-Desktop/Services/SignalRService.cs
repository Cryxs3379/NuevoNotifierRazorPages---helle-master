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

        var idProp = type.GetProperty("id");
        if (idProp != null)
        {
            var idValue = idProp.GetValue(obj);
            if (idValue != null)
            {
                if (idValue is string idStr && long.TryParse(idStr, out var idLong))
                    dtoResult.Id = idLong;
                else if (idValue is long idLong2)
                    dtoResult.Id = idLong2;
            }
        }

        var originatorProp = type.GetProperty("originator") ?? type.GetProperty("Originator");
        if (originatorProp != null)
            dtoResult.Originator = originatorProp.GetValue(obj)?.ToString() ?? string.Empty;

        var recipientProp = type.GetProperty("recipient") ?? type.GetProperty("Recipient");
        if (recipientProp != null)
            dtoResult.Recipient = recipientProp.GetValue(obj)?.ToString() ?? string.Empty;

        var customerPhoneProp = type.GetProperty("customerPhone") ?? type.GetProperty("CustomerPhone");
        if (customerPhoneProp != null)
            dtoResult.CustomerPhone = customerPhoneProp.GetValue(obj)?.ToString() ?? string.Empty;
        else
        {
            // Si no viene customerPhone, inferirlo desde direction
            // Para inbound: customerPhone = originator, para outbound: customerPhone = recipient
            if (dtoResult.Direction == 0)
                dtoResult.CustomerPhone = dtoResult.Originator;
            else
                dtoResult.CustomerPhone = dtoResult.Recipient;
        }

        var bodyProp = type.GetProperty("body") ?? type.GetProperty("Body") ?? type.GetProperty("message") ?? type.GetProperty("Message");
        if (bodyProp != null)
            dtoResult.Body = bodyProp.GetValue(obj)?.ToString() ?? string.Empty;

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

        var messageAtProp = type.GetProperty("messageAt") ?? type.GetProperty("MessageAt") ?? type.GetProperty("receivedUtc") ?? type.GetProperty("ReceivedUtc");
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
