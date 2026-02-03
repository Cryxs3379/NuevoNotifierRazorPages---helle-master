using Notifier.Calls.Infrastructure.Incoming;

namespace NotifierAPI.Services
{
    public class IncomingCallWatcher : BackgroundService
    {
        private readonly ILogger<IncomingCallWatcher> _logger;
        private readonly IncomingCallWatcherSettings _settings;
        private readonly IncomingCallPoller _poller;

        public IncomingCallWatcher(
            ILogger<IncomingCallWatcher> logger,
            IncomingCallWatcherSettings settings,
            IncomingCallPoller poller)
        {
            _logger = logger;
            _settings = settings;
            _poller = poller;
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
            await _poller.InitializeAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _poller.PollAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en IncomingCallWatcher durante el polling");
                }

                await Task.Delay(TimeSpan.FromSeconds(_settings.PollSeconds), stoppingToken);
            }

            _logger.LogInformation("IncomingCallWatcher detenido");
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
