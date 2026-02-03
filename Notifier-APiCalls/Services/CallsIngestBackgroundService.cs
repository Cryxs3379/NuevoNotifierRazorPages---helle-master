using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Notifier.Calls.Infrastructure.Ingest;
using NotifierAPI.Configuration;
using NotifierAPI.Hubs;

namespace NotifierAPI.Services;

public class CallsIngestBackgroundService : BackgroundService
{
    private readonly CallsIngestSettings _settings;
    private readonly ILogger<CallsIngestBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly CallsIngestProcessor _processor;
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, byte> _inFlight;
    private FileSystemWatcher? _watcher;
    
    // Semaphore para evitar ejecuciones paralelas del SP
    private readonly SemaphoreSlim _spExecutionSemaphore = new SemaphoreSlim(1, 1);
    
    // Timer para debounce de ejecución del SP
    private Timer? _spDebounceTimer;
    private readonly object _spDebounceLock = new object();
    private bool _spExecutionPending = false;

    public CallsIngestBackgroundService(
        IOptions<CallsIngestSettings> settings,
        ILogger<CallsIngestBackgroundService> logger,
        IServiceProvider serviceProvider,
        CallsIngestProcessor processor)
    {
        _settings = settings.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _processor = processor;
        _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _inFlight = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("========================================");
        _logger.LogInformation("CallsIngestBackgroundService iniciando...");
        _logger.LogInformation("WatchPath: {WatchPath}", _settings.WatchPath);
        _logger.LogInformation("========================================");

        try
        {
            // Crear carpetas si no existen
            EnsureDirectoriesExist();

            // Iniciar FileSystemWatcher
            StartWatcher();

            _logger.LogInformation("CallsIngestBackgroundService iniciado correctamente. Esperando archivos...");

            // Procesar cola
            await ProcessQueueAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico en CallsIngestBackgroundService.ExecuteAsync");
            throw; // Re-lanzar para que el host sepa que falló
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _watcher?.Dispose();
        return base.StopAsync(cancellationToken);
    }

    private void EnsureDirectoriesExist()
    {
        try
        {
            Directory.CreateDirectory(_settings.WatchPath);
            // ProcessedPath y ErrorPath ya no se usan, pero se mantienen opcionales por compatibilidad
            // No se crean automáticamente para evitar carpetas innecesarias
            _logger.LogInformation("Directorio WatchPath verificado/creado: {WatchPath}", _settings.WatchPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando directorio WatchPath");
        }
    }

    private void StartWatcher()
    {
        try
        {
            // Verificar que la ruta existe
            if (!Directory.Exists(_settings.WatchPath))
            {
                _logger.LogError("La ruta WatchPath no existe: {WatchPath}", _settings.WatchPath);
                throw new DirectoryNotFoundException($"La ruta WatchPath no existe: {_settings.WatchPath}");
            }

            _watcher = new FileSystemWatcher(_settings.WatchPath, "*.csv")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size
            };

            _watcher.Created += (_, e) => 
            {
                _logger.LogInformation("Evento Created detectado: {FilePath}", e.FullPath);
                Enqueue(e.FullPath);
            };
            _watcher.Renamed += (_, e) => 
            {
                _logger.LogInformation("Evento Renamed detectado: {FilePath}", e.FullPath);
                Enqueue(e.FullPath);
            };
            _watcher.Error += (_, e) => _logger.LogWarning(e.GetException(), "Error en FileSystemWatcher");

            _watcher.EnableRaisingEvents = true;
            _logger.LogInformation("FileSystemWatcher iniciado correctamente en {WatchPath} con filtro *.csv", 
                _settings.WatchPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error iniciando FileSystemWatcher en {WatchPath}", _settings.WatchPath);
            throw; // Re-lanzar para que se sepa que falló
        }
    }

    private void Enqueue(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension != ".csv")
        {
            _logger.LogDebug("Archivo ignorado (no es CSV): {FilePath}", fullPath);
            return;
        }

        var fileName = Path.GetFileName(fullPath);
        if (_queue.Writer.TryWrite(fullPath))
        {
            _logger.LogInformation("Detectado archivo: {FileName}", fileName);
        }
        else
        {
            _logger.LogWarning("No se pudo encolar el archivo (cola cerrada): {FileName}", fileName);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var fullPath in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            if (!_inFlight.TryAdd(fileName, 0))
            {
                _logger.LogDebug("Archivo ya en procesamiento, saltando: {FileName}", fileName);
                continue;
            }

            try
            {
                var shouldSchedule = await _processor.ProcessFileAsync(fullPath, fileName, stoppingToken);
                if (shouldSchedule)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MessagesHub>>();
                    ScheduleStoredProcedureExecution(hubContext, stoppingToken);
                }
            }
            finally
            {
                _inFlight.TryRemove(fileName, out _);
            }
        }
    }

    // Programar ejecución del SP con debounce (espera 2-3 segundos antes de ejecutar)
    private void ScheduleStoredProcedureExecution(IHubContext<MessagesHub> hubContext, CancellationToken cancellationToken)
    {
        lock (_spDebounceLock)
        {
            // Marcar que hay una ejecución pendiente
            _spExecutionPending = true;

            // Cancelar timer anterior si existe
            _spDebounceTimer?.Dispose();

            // Crear nuevo timer que ejecutará el SP después de 2.5 segundos de inactividad
            _spDebounceTimer = new Timer(async _ =>
            {
                lock (_spDebounceLock)
                {
                    if (!_spExecutionPending)
                    {
                        return; // Ya se ejecutó o se canceló
                    }
                    _spExecutionPending = false;
                }

                _ = Task.Run(async () =>
                {
                    await ExecuteStoredProcedureAsync(hubContext, cancellationToken);
                }, cancellationToken);
            }, null, TimeSpan.FromSeconds(2.5), Timeout.InfiniteTimeSpan);
        }
    }

    // Ejecutar el stored procedure ProcessNotifierCalls de forma segura (una sola vez a la vez)
    private async Task ExecuteStoredProcedureAsync(IHubContext<MessagesHub> hubContext, CancellationToken cancellationToken)
    {
        // Usar SemaphoreSlim para garantizar que solo se ejecuta una vez a la vez
        if (!await _spExecutionSemaphore.WaitAsync(0, cancellationToken))
        {
            _logger.LogWarning("⚠️ ProcessNotifierCalls ya está en ejecución, saltando esta ejecución");
            return;
        }

        try
        {
            _logger.LogInformation("🔄 Ejecutando ProcessNotifierCalls...");
            var startTime = DateTime.UtcNow;

            await _processor.ExecuteStoredProcedureAsync(hubContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error ejecutando ProcessNotifierCalls");
            // No emitir CallViewsUpdated si el SP falla
        }
        finally
        {
            _spExecutionSemaphore.Release();
        }
    }


    public override void Dispose()
    {
        _spDebounceTimer?.Dispose();
        _spExecutionSemaphore?.Dispose();
        _watcher?.Dispose();
        base.Dispose();
    }
}
