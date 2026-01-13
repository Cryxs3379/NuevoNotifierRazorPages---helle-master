using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using NotifierAPI.Configuration;
using NotifierAPI.Hubs;
using NotifierAPI.Models;

namespace NotifierAPI.Services;

public class EsendexMessageWatcher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EsendexMessageWatcher> _logger;
    private readonly WatcherSettings _settings;
    private readonly ConcurrentDictionary<string, bool> _seenMessageIds = new();

    public EsendexMessageWatcher(
        IServiceProvider serviceProvider,
        ILogger<EsendexMessageWatcher> logger,
        WatcherSettings settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EsendexMessageWatcher started. Interval: {IntervalSeconds}s, AccountRef: {AccountRef}", 
            _settings.IntervalSeconds, _settings.AccountRef ?? "none");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckForNewMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EsendexMessageWatcher cycle");
            }

            // Esperar el intervalo configurado antes de la siguiente verificación
            await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("EsendexMessageWatcher stopped");
    }

    private async Task CheckForNewMessages(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MessagesHub>>();

        try
        {
            // Consultar los primeros 10 mensajes inbound
            var response = await inboxService.GetMessagesAsync(
                direction: "inbound",
                page: 1,
                pageSize: 10,
                accountRef: string.IsNullOrWhiteSpace(_settings.AccountRef) ? null : _settings.AccountRef,
                cancellationToken: ct);

            if (response?.Items == null || response.Items.Count == 0)
            {
                _logger.LogDebug("No messages found in this cycle");
                return;
            }

            var newMessages = new List<MessageDto>();

            foreach (var message in response.Items)
            {
                if (string.IsNullOrWhiteSpace(message.Id))
                    continue;

                // Verificar si ya hemos visto este ID
                if (_seenMessageIds.TryAdd(message.Id, true))
                {
                    // Es un mensaje nuevo
                    newMessages.Add(message);
                    _logger.LogInformation("New message detected: ID={Id}, From={From}, MessageLength={Length}", 
                        message.Id.Substring(0, Math.Min(8, message.Id.Length)) + "...", 
                        message.From, 
                        message.Message?.Length ?? 0);
                }
            }

            // Emitir eventos para cada mensaje nuevo
            foreach (var message in newMessages)
            {
                try
                {
                    await hubContext.Clients.All.SendAsync("NewMessage", message, ct);
                    _logger.LogDebug("Emitted NewMessage event for ID={Id}", 
                        message.Id.Substring(0, Math.Min(8, message.Id.Length)) + "...");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to emit NewMessage event for ID={Id}", 
                        message.Id.Substring(0, Math.Min(8, message.Id.Length)) + "...");
                }
            }

            if (newMessages.Count > 0)
            {
                _logger.LogInformation("Emitted {Count} new message(s) via SignalR", newMessages.Count);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Authentication failed in EsendexMessageWatcher");
            // No relanzar, continuar en el siguiente ciclo
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for new messages in EsendexMessageWatcher");
            // No relanzar, continuar en el siguiente ciclo
        }
    }
}
