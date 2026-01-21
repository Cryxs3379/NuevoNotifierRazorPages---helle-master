using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;
using NotifierAPI.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NotifierAPI.Services
{
    public class IncomingCallWatcher : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<IncomingCallWatcher> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IncomingCallWatcherSettings _settings;
        private long _lastSeenId = 0;
        private bool _initialized = false;

        public IncomingCallWatcher(
            IServiceProvider serviceProvider,
            ILogger<IncomingCallWatcher> logger,
            IHttpClientFactory httpClientFactory,
            IncomingCallWatcherSettings settings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_settings.Enabled)
            {
                _logger.LogInformation("IncomingCallWatcher está deshabilitado en configuración");
                return;
            }

            _logger.LogInformation("IncomingCallWatcher iniciado. Polling cada {PollSeconds} segundos", _settings.PollSeconds);

            // Inicializar lastSeenId al arrancar
            await InitializeLastSeenIdAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForNewCallsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en IncomingCallWatcher durante el polling");
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.PollSeconds), stoppingToken);
            }

            _logger.LogInformation("IncomingCallWatcher detenido");
        }

        private async Task InitializeLastSeenIdAsync(CancellationToken ct)
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

        private async Task CheckForNewCallsAsync(CancellationToken ct)
        {
            if (!_initialized)
            {
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            // Consultar nuevos registros
            var newCalls = await dbContext.IncomingCalls
                .Where(c => c.Id > _lastSeenId)
                .OrderBy(c => c.Id)
                .Take(_settings.MaxBatch)
                .ToListAsync(ct);

            if (newCalls.Count == 0)
            {
                return;
            }

            // Actualizar lastSeenId
            var maxId = newCalls.Max(c => c.Id);
            var newCount = newCalls.Count;
            var latestAtUtc = newCalls.Max(c => c.DateAndTime);

            _logger.LogInformation("Detectadas {Count} nuevas llamadas. MaxId: {MaxId}, LatestAt: {LatestAt}",
                newCount, maxId, latestAtUtc);

            // Notificar a Notifier-API
            await NotifyNotifierApiAsync(newCount, maxId, latestAtUtc, ct);

            // Actualizar lastSeenId después de notificar exitosamente
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

                // Añadir ApiKey si está configurado
                if (!string.IsNullOrWhiteSpace(_settings.NotifierApiKey))
                {
                    httpClient.DefaultRequestHeaders.Add("X-Api-Key", _settings.NotifierApiKey);
                }

                var payload = new
                {
                    newCount = newCount,
                    maxId = maxId,
                    latestAtUtc = latestAtUtc.ToString("O")
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

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

    public class IncomingCallWatcherSettings
    {
        public bool Enabled { get; set; } = false;
        public int PollSeconds { get; set; } = 3;
        public int MaxBatch { get; set; } = 50;
        public string NotifierApiBaseUrl { get; set; } = string.Empty;
        public string NotifierApiNotifyPath { get; set; } = string.Empty;
        public string NotifierApiKey { get; set; } = string.Empty;
    }

    public class NotifierApiSettings
    {
        public string? BaseUrl { get; set; }
        public string? CallsNotifyPath { get; set; }
        public string? ApiKey { get; set; }
    }
}
