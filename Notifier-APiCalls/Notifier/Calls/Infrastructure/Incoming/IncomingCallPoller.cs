using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;
using NotifierAPI.Services;

namespace Notifier.Calls.Infrastructure.Incoming;

public sealed class IncomingCallPoller
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<IncomingCallPoller> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IncomingCallWatcherSettings _settings;
    private long _lastSeenId = 0;
    private bool _initialized = false;

    public IncomingCallPoller(
        IServiceProvider serviceProvider,
        ILogger<IncomingCallPoller> logger,
        IHttpClientFactory httpClientFactory,
        IncomingCallWatcherSettings settings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _settings = settings;
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            var maxId = await dbContext.IncomingCalls
                .OrderByDescending(c => c.Id)
                .Select(c => c.Id)
                .FirstOrDefaultAsync(ct);

            _lastSeenId = maxId;
            _initialized = true;
            _logger.LogInformation("IncomingCallWatcher inicializado. LastSeenId = {LastSeenId}", _lastSeenId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar LastSeenId. Se usará 0 como valor inicial");
            _lastSeenId = 0;
            _initialized = true;
        }
    }

    public async Task PollAsync(CancellationToken ct)
    {
        if (!_initialized)
        {
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        var newCalls = await dbContext.IncomingCalls
            .Where(c => c.Id > _lastSeenId)
            .OrderBy(c => c.Id)
            .Take(_settings.MaxBatch)
            .ToListAsync(ct);

        if (newCalls.Count == 0)
        {
            return;
        }

        var maxId = newCalls.Max(c => c.Id);

        var missedCalls = newCalls.Where(c => c.Status == 1).ToList();

        if (missedCalls.Count == 0)
        {
            _lastSeenId = maxId;
            return;
        }

        var newCountMissed = missedCalls.Count;
        var maxIdMissed = missedCalls.Max(c => c.Id);
        var latestAtUtcMissed = missedCalls.Max(c => c.DateAndTime);

        _logger.LogInformation("Detectadas {Count} nuevas llamadas perdidas (de {Total} nuevas). MaxIdMissed: {MaxIdMissed}, LatestAt: {LatestAt}",
            newCountMissed, newCalls.Count, maxIdMissed, latestAtUtcMissed);

        await NotifyNotifierApiAsync(newCountMissed, maxIdMissed, latestAtUtcMissed, ct);

        _lastSeenId = maxId;
    }

    private async Task NotifyNotifierApiAsync(int newCount, long maxId, DateTime latestAtUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.NotifierApiBaseUrl) ||
            string.IsNullOrWhiteSpace(_settings.NotifierApiNotifyPath))
        {
            _logger.LogWarning("NotifierApi BaseUrl o NotifyPath no configurado. Omitiendo notificación");
            return;
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(_settings.NotifierApiBaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            if (!string.IsNullOrWhiteSpace(_settings.NotifierApiKey))
            {
                httpClient.DefaultRequestHeaders.Add("X-Api-Key", _settings.NotifierApiKey);
            }

            var payload = new
            {
                newCountMissed = newCount,
                maxIdMissed = maxId,
                latestAtUtcMissed = latestAtUtc.ToString("O")
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_settings.NotifierApiNotifyPath, content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Notificación enviada exitosamente a Notifier-API. NewCount: {NewCount}, MaxId: {MaxId}",
                    newCount, maxId);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Error al notificar a Notifier-API. Status: {Status}, Response: {Response}",
                    response.StatusCode, responseBody);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Error de red al notificar a Notifier-API. Se reintentará en el próximo tick");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning("Timeout al notificar a Notifier-API. Se reintentará en el próximo tick");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado al notificar a Notifier-API");
        }
    }
}
