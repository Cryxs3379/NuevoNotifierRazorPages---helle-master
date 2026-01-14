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
    
    // Cache con expiración: id -> timestamp de cuando se vio
    private readonly ConcurrentDictionary<string, DateTime> _seenMessageIds = new();
    
    // Configuración de cache
    private const int MaxCacheSize = 2000;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    
    // Configuración de throttling
    private const int MaxMessagesPerTick = 20;
    
    // Backoff para errores
    private int _consecutiveFailures = 0;
    private DateTime _lastSuccessTime = DateTime.UtcNow;

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
        _logger.LogInformation("EsendexMessageWatcher started. Interval: {IntervalSeconds}s, AccountRef: {AccountRef}, CacheTTL: {CacheTTL}h, MaxCacheSize: {MaxCacheSize}", 
            _settings.IntervalSeconds, 
            _settings.AccountRef ?? "none",
            CacheTtl.TotalHours,
            MaxCacheSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Purga de cache antes de cada ciclo
                PurgeExpiredCacheEntries();
                
                _logger.LogDebug("EsendexMessageWatcher tick. Cache size: {CacheSize}, Consecutive failures: {Failures}", 
                    _seenMessageIds.Count, _consecutiveFailures);
                
                await CheckForNewMessages(stoppingToken);
                
                // Reset backoff en caso de éxito
                if (_consecutiveFailures > 0)
                {
                    _logger.LogInformation("EsendexMessageWatcher recovered after {Failures} consecutive failures", _consecutiveFailures);
                    _consecutiveFailures = 0;
                }
                _lastSuccessTime = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, "Error in EsendexMessageWatcher cycle (failure #{Failure})", _consecutiveFailures);
            }

            // Esperar con backoff si hay errores
            var waitTime = CalculateDelay();
            _logger.LogDebug("Waiting {WaitSeconds}s before next cycle", waitTime.TotalSeconds);
            await Task.Delay(waitTime, stoppingToken);
        }

        _logger.LogInformation("EsendexMessageWatcher stopped");
    }

    private void PurgeExpiredCacheEntries()
    {
        var now = DateTime.UtcNow;
        var expiredCutoff = now - CacheTtl;
        var keysToRemove = new List<string>();

        // Encontrar entradas expiradas
        foreach (var kvp in _seenMessageIds)
        {
            if (kvp.Value < expiredCutoff)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        // Remover entradas expiradas
        foreach (var key in keysToRemove)
        {
            _seenMessageIds.TryRemove(key, out _);
        }

        // Si aún excede el tamaño máximo, remover las más antiguas
        if (_seenMessageIds.Count > MaxCacheSize)
        {
            var sortedByTime = _seenMessageIds.OrderBy(kvp => kvp.Value).ToList();
            var toRemove = sortedByTime.Take(_seenMessageIds.Count - MaxCacheSize).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in toRemove)
            {
                _seenMessageIds.TryRemove(key, out _);
            }
            
            _logger.LogDebug("Purged {Count} old entries from cache (exceeded max size)", toRemove.Count);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Purged {Count} expired entries from cache. Current size: {Size}", 
                keysToRemove.Count, _seenMessageIds.Count);
        }
    }

    private TimeSpan CalculateDelay()
    {
        if (_consecutiveFailures == 0)
        {
            return TimeSpan.FromSeconds(_settings.IntervalSeconds);
        }

        // Backoff exponencial: IntervalSeconds * 2^n, máximo 5 minutos
        var backoffMultiplier = Math.Pow(2, _consecutiveFailures - 1);
        var backoffSeconds = _settings.IntervalSeconds * backoffMultiplier;
        var maxBackoffSeconds = 300; // 5 minutos
        var delaySeconds = Math.Min(backoffSeconds, maxBackoffSeconds);

        _logger.LogDebug("Calculated backoff delay: {DelaySeconds}s (failures: {Failures})", 
            delaySeconds, _consecutiveFailures);

        return TimeSpan.FromSeconds(delaySeconds);
    }

    private async Task CheckForNewMessages(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var inboxService = scope.ServiceProvider.GetRequiredService<IInboxService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MessagesHub>>();
        var smsRepository = scope.ServiceProvider.GetService<SmsMessageRepository>();

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
            var now = DateTime.UtcNow;

            foreach (var message in response.Items)
            {
                if (string.IsNullOrWhiteSpace(message.Id))
                    continue;

                // Verificar si ya hemos visto este ID (usando timestamp)
                if (_seenMessageIds.TryAdd(message.Id, now))
                {
                    // Es un mensaje nuevo
                    newMessages.Add(message);
                    _logger.LogInformation("New message detected: ID={Id}, From={From}, MessageLength={Length}", 
                        message.Id.Substring(0, Math.Min(8, message.Id.Length)) + "...", 
                        message.From, 
                        message.Message?.Length ?? 0);
                    
                    // Intentar guardar en BD (si el repositorio está disponible)
                    if (smsRepository != null)
                    {
                        var saved = await smsRepository.SaveReceivedAsync(
                            originator: message.From ?? string.Empty,
                            recipient: message.To ?? string.Empty,
                            body: message.Message ?? string.Empty,
                            type: "SMS",
                            messageAt: message.ReceivedUtc,
                            cancellationToken: ct);
                        
                        if (!saved)
                        {
                            // Emitir evento SignalR para notificar el error
                            try
                            {
                                await hubContext.Clients.All.SendAsync("DbError", 
                                    $"No se pudo guardar mensaje recibido en BD: From={message.From}, To={message.To}", 
                                    ct);
                            }
                            catch (Exception signalREx)
                            {
                                _logger.LogWarning(signalREx, "Failed to emit DbError event via SignalR");
                            }
                        }
                    }
                }
                else
                {
                    // Actualizar timestamp si ya existe (para mantenerlo en cache)
                    _seenMessageIds.TryUpdate(message.Id, now, _seenMessageIds[message.Id]);
                }
            }

            if (newMessages.Count == 0)
            {
                _logger.LogDebug("No new messages in this cycle");
                return;
            }

            // Throttling: limitar cantidad de mensajes por tick
            var messagesToEmit = newMessages;
            var truncated = false;
            if (newMessages.Count > MaxMessagesPerTick)
            {
                messagesToEmit = newMessages.Take(MaxMessagesPerTick).ToList();
                truncated = true;
                _logger.LogWarning("Throttling: limiting to {Max} messages per tick (found {Total})", 
                    MaxMessagesPerTick, newMessages.Count);
            }

            // Emitir eventos individuales "NewMessage" (compatibilidad)
            int emittedCount = 0;
            foreach (var message in messagesToEmit)
            {
                try
                {
                    await hubContext.Clients.All.SendAsync("NewMessage", message, ct);
                    emittedCount++;
                    _logger.LogDebug("Emitted NewMessage event for ID={Id}", 
                        message.Id.Substring(0, Math.Min(8, message.Id.Length)) + "...");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to emit NewMessage event for ID={Id}", 
                        message.Id.Substring(0, Math.Min(8, message.Id.Length)) + "...");
                }
            }

            // Opcional: emitir evento batch "NewMessages" (para futuros clientes)
            if (messagesToEmit.Count > 0)
            {
                try
                {
                    await hubContext.Clients.All.SendAsync("NewMessages", messagesToEmit, ct);
                    _logger.LogDebug("Emitted NewMessages batch event with {Count} messages", messagesToEmit.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to emit NewMessages batch event");
                }
            }

            if (emittedCount > 0)
            {
                var message = truncated 
                    ? $"Emitted {emittedCount} new message(s) via SignalR (throttled from {newMessages.Count})"
                    : $"Emitted {emittedCount} new message(s) via SignalR";
                _logger.LogInformation(message);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _consecutiveFailures++;
            _logger.LogWarning(ex, "Authentication failed in EsendexMessageWatcher (failure #{Failure})", _consecutiveFailures);
            // No relanzar, continuar en el siguiente ciclo con backoff
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            _consecutiveFailures++;
            _logger.LogWarning(ex, "Network error in EsendexMessageWatcher (failure #{Failure})", _consecutiveFailures);
            // No relanzar, continuar en el siguiente ciclo con backoff
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            _logger.LogError(ex, "Error checking for new messages in EsendexMessageWatcher (failure #{Failure})", _consecutiveFailures);
            // No relanzar, continuar en el siguiente ciclo con backoff
        }
    }
}
