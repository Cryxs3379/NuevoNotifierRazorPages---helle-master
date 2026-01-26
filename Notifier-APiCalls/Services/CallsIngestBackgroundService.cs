using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotifierAPI.Configuration;
using NotifierAPI.Data;
using NotifierAPI.Hubs;
using NotifierAPI.Models;

namespace NotifierAPI.Services;

// Helper para conversión de zona horaria (igual que en MissedCallsController)
internal static class TimeZoneHelper
{
    private static readonly TimeZoneInfo SpainTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    public static DateTime ToSpainTime(DateTime utcDate)
    {
        if (utcDate.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);
        }

        if (utcDate.Kind == DateTimeKind.Local)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcDate.ToUniversalTime(), SpainTimeZone);
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDate, SpainTimeZone);
    }
}

public class CallsIngestBackgroundService : BackgroundService
{
    private readonly CallsIngestSettings _settings;
    private readonly ILogger<CallsIngestBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<string> _queue;
    private readonly ConcurrentDictionary<string, byte> _inFlight;
    private FileSystemWatcher? _watcher;

    public CallsIngestBackgroundService(
        IOptions<CallsIngestSettings> settings,
        ILogger<CallsIngestBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _settings = settings.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
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
            Directory.CreateDirectory(_settings.ProcessedPath);
            Directory.CreateDirectory(_settings.ErrorPath);
            _logger.LogInformation("Directorios verificados/creados: Watch={Watch}, Processed={Processed}, Error={Error}",
                _settings.WatchPath, _settings.ProcessedPath, _settings.ErrorPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando directorios");
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

        if (_queue.Writer.TryWrite(fullPath))
        {
            _logger.LogInformation("Archivo encolado para procesamiento: {FilePath}", fullPath);
        }
        else
        {
            _logger.LogWarning("No se pudo encolar el archivo (cola cerrada): {FilePath}", fullPath);
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
                await ProcessFileAsync(fullPath, fileName, stoppingToken);
            }
            finally
            {
                _inFlight.TryRemove(fileName, out _);
            }
        }
    }

    private async Task ProcessFileAsync(string fullPath, string fileName, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando procesamiento de archivo: {FileName}", fileName);
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MessagesHub>>();

            // Verificar si ya fue procesado
            if (await IsAlreadyProcessedAsync(dbContext, fileName, stoppingToken))
            {
                _logger.LogInformation("Archivo saltado (ya procesado): {FileName}", fileName);
                return;
            }

            // Leer archivo con reintentos
            var rows = await ReadFileWithRetryAsync(fullPath, stoppingToken);
            if (rows.Count == 0)
            {
                _logger.LogInformation("Archivo sin filas válidas: {FileName}", fileName);
                return;
            }

            // Insertar en BD y obtener las filas insertadas
            var insertedCalls = await BulkInsertAsync(dbContext, rows, fileName, stoppingToken);
            
            if (insertedCalls.Count > 0)
            {
                _logger.LogInformation("Insertadas {RowCount} filas desde {FileName}", insertedCalls.Count, fileName);
                
                // Emitir eventos SignalR para cada llamada nueva
                try
                {
                    foreach (var call in insertedCalls)
                    {
                        // Convertir fecha a zona horaria de España (como en el controller)
                        var spainTime = TimeZoneHelper.ToSpainTime(call.DateAndTime);
                        
                        await hubContext.Clients.All.SendAsync("NewMissedCall", new
                        {
                            id = call.Id,
                            dateAndTime = spainTime,
                            phoneNumber = call.PhoneNumber,
                            statusText = call.StatusText ?? "N/A",
                            sourceFile = call.SourceFile,
                            loadedAt = call.LoadedAt
                        }, cancellationToken: stoppingToken);
                        
                        _logger.LogDebug("SignalR NewMissedCall emitido: Id={Id}, Phone={Phone}", call.Id, call.PhoneNumber);
                    }
                    
                    // También emitir CallsUpdated como fallback/compatibilidad
                    await hubContext.Clients.All.SendAsync("CallsUpdated", cancellationToken: stoppingToken);
                    _logger.LogDebug("Evento SignalR CallsUpdated emitido (fallback)");
                }
                catch (Exception signalREx)
                {
                    _logger.LogWarning(signalREx, "Error emitiendo eventos SignalR");
                }
            }
            else
            {
                _logger.LogWarning("No se insertaron filas desde {FileName}", fileName);
            }

            // Mover a Processed (opcional, como en el original no se movía, pero lo dejamos)
            MoveToProcessed(fullPath, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando archivo {FileName}", fileName);
            MoveToError(fullPath, fileName);
        }
    }

    private async Task<bool> IsAlreadyProcessedAsync(
        NotificationDbContext dbContext, 
        string fileName, 
        CancellationToken stoppingToken)
    {
        return await dbContext.NotifierCallsStaging
            .AnyAsync(c => c.SourceFile == fileName, stoppingToken);
    }

    private async Task<List<NotifierRow>> ReadFileWithRetryAsync(
        string fullPath, 
        CancellationToken stoppingToken)
    {
        const int MaxReadAttempts = 15;
        var ReadRetryDelay = TimeSpan.FromMilliseconds(300);

        for (var attempt = 1; attempt <= MaxReadAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Archivo no existe: {FilePath}", fullPath);
                    return new List<NotifierRow>();
                }

                // Verificar que el archivo esté estable (no se esté copiando)
                if (!await IsFileStableAsync(fullPath, stoppingToken))
                {
                    throw new IOException("Archivo en copia (tamaño inestable)");
                }

                // Solo CSV por ahora
                var rows = await ReadCsvAsync(fullPath, stoppingToken);
                return rows;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == MaxReadAttempts)
                {
                    _logger.LogError(ex, "No se pudo leer el archivo tras {Attempts} intentos: {FilePath}", 
                        attempt, fullPath);
                    return new List<NotifierRow>();
                }

                await Task.Delay(ReadRetryDelay, stoppingToken);
            }
        }

        return new List<NotifierRow>();
    }

    private static async Task<bool> IsFileStableAsync(string fullPath, CancellationToken stoppingToken)
    {
        const int ReadRetryDelayMs = 300;
        var initialLength = new FileInfo(fullPath).Length;
        await Task.Delay(ReadRetryDelayMs, stoppingToken);
        var finalLength = new FileInfo(fullPath).Length;
        return initialLength == finalLength;
    }

    private async Task<List<NotifierRow>> ReadCsvAsync(string fullPath, CancellationToken stoppingToken)
    {
        var rows = new List<NotifierRow>();
        var fileName = Path.GetFileName(fullPath);

        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            stoppingToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryParseCsvLine(line, out var dateAndTime, out var phoneNumber, out var statusText))
            {
                rows.Add(new NotifierRow(dateAndTime, phoneNumber, statusText, fileName));
            }
        }

        _logger.LogDebug("Leídas {Count} filas válidas desde {FileName}", rows.Count, fileName);
        return rows;
    }

    private static bool TryParseCsvLine(
        string line, 
        out DateTime dateAndTime, 
        out string phoneNumber, 
        out string statusText)
    {
        dateAndTime = default;
        phoneNumber = string.Empty;
        statusText = string.Empty;

        var firstComma = line.IndexOf(',');
        if (firstComma <= 0) return false;

        var secondComma = line.IndexOf(',', firstComma + 1);
        if (secondComma <= firstComma + 1) return false;

        var datePart = Unquote(line[..firstComma].Trim());
        var phonePart = Unquote(line.Substring(firstComma + 1, secondComma - firstComma - 1).Trim());
        var statusPart = Unquote(line[(secondComma + 1)..].Trim());

        if (!DateTime.TryParseExact(datePart, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, 
            DateTimeStyles.None, out dateAndTime))
        {
            if (!DateTime.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateAndTime))
            {
                return false;
            }
        }

        phoneNumber = phonePart;
        statusText = statusPart;
        return true;
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed[1..^1];
        }
        return trimmed;
    }

    private async Task<List<NotifierCallsStaging>> BulkInsertAsync(
        NotificationDbContext dbContext,
        List<NotifierRow> rows,
        string sourceFile,
        CancellationToken stoppingToken)
    {
        var connectionString = dbContext.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string no disponible");
        }

        var table = new DataTable();
        table.Columns.Add("DateAndTime", typeof(DateTime));
        table.Columns.Add("PhoneNumber", typeof(string));
        table.Columns.Add("StatusText", typeof(string));
        table.Columns.Add("SourceFile", typeof(string));
        table.Columns.Add("LoadedAt", typeof(DateTime));

        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            table.Rows.Add(row.DateAndTime, row.PhoneNumber, row.StatusText, row.SourceFile, now);
        }

        _logger.LogDebug("Preparando bulk insert de {Count} filas", rows.Count);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(stoppingToken);

        using var bulkCopy = new SqlBulkCopy(connection)
        {
            DestinationTableName = "NewNotifier.dbo.NotifierCalls_Staging",
            BatchSize = 2000
        };

        bulkCopy.ColumnMappings.Add("DateAndTime", "DateAndTime");
        bulkCopy.ColumnMappings.Add("PhoneNumber", "PhoneNumber");
        bulkCopy.ColumnMappings.Add("StatusText", "StatusText");
        bulkCopy.ColumnMappings.Add("SourceFile", "SourceFile");
        bulkCopy.ColumnMappings.Add("LoadedAt", "LoadedAt");

        await bulkCopy.WriteToServerAsync(table, stoppingToken);
        _logger.LogDebug("Bulk insert completado: {Count} filas insertadas", rows.Count);

        // Consultar las filas recién insertadas para obtener los IDs
        // Usamos SourceFile y LoadedAt para identificar las filas que acabamos de insertar
        // Consultamos con una ventana de tiempo para evitar race conditions
        var timeWindow = now.AddSeconds(-2); // 2 segundos antes del now
        var insertedCalls = await dbContext.NotifierCallsStaging
            .Where(c => c.SourceFile == sourceFile && 
                       c.LoadedAt.HasValue && 
                       c.LoadedAt.Value >= timeWindow &&
                       c.LoadedAt.Value <= now.AddSeconds(1)) // Ventana de 1 segundo después
            .OrderByDescending(c => c.Id)
            .Take(rows.Count)
            .ToListAsync(stoppingToken);

        // Si no encontramos todas las filas, intentar obtener las más recientes por SourceFile
        if (insertedCalls.Count < rows.Count)
        {
            _logger.LogWarning("No se encontraron todas las filas insertadas ({Found}/{Expected}), consultando por SourceFile", 
                insertedCalls.Count, rows.Count);
            
            var fallbackCalls = await dbContext.NotifierCallsStaging
                .Where(c => c.SourceFile == sourceFile)
                .OrderByDescending(c => c.Id)
                .Take(rows.Count)
                .ToListAsync(stoppingToken);
            
            insertedCalls = fallbackCalls;
        }

        _logger.LogDebug("Consultadas {Count} filas insertadas para SignalR", insertedCalls.Count);
        return insertedCalls;
    }

    private void MoveToProcessed(string fullPath, string fileName)
    {
        try
        {
            var destPath = Path.Combine(_settings.ProcessedPath, fileName);
            if (File.Exists(destPath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                destPath = Path.Combine(_settings.ProcessedPath, $"{nameWithoutExt}_{timestamp}{ext}");
            }
            File.Move(fullPath, destPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo mover archivo a Processed: {FileName}", fileName);
        }
    }

    private void MoveToError(string fullPath, string fileName)
    {
        try
        {
            var destPath = Path.Combine(_settings.ErrorPath, fileName);
            if (File.Exists(destPath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var ext = Path.GetExtension(fileName);
                destPath = Path.Combine(_settings.ErrorPath, $"{nameWithoutExt}_{timestamp}{ext}");
            }
            File.Move(fullPath, destPath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo mover archivo a Error: {FileName}", fileName);
        }
    }

    private sealed record NotifierRow(
        DateTime DateAndTime, 
        string PhoneNumber, 
        string StatusText, 
        string SourceFile);
}
