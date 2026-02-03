using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notifier.Calls.Domain;
using NotifierAPI.Configuration;
using NotifierAPI.Data;
using NotifierAPI.Hubs;
using NotifierAPI.Models;

namespace Notifier.Calls.Infrastructure.Ingest;

public sealed class CallsIngestProcessor
{
    private readonly CallsIngestSettings _settings;
    private readonly ILogger<CallsIngestProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;

    public CallsIngestProcessor(
        IOptions<CallsIngestSettings> settings,
        ILogger<CallsIngestProcessor> logger,
        IServiceProvider serviceProvider)
    {
        _settings = settings.Value;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<bool> ProcessFileAsync(string fullPath, string fileName, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iniciando procesamiento de archivo: {FileName}", fileName);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<MessagesHub>>();

            if (await IsAlreadyProcessedAsync(dbContext, fileName, stoppingToken))
            {
                _logger.LogInformation("Saltando (ya procesado): {FileName}", fileName);
                return false;
            }

            var rows = await ReadFileWithRetryAsync(fullPath, stoppingToken);

            if (rows.Count == 0)
            {
                _logger.LogInformation("Archivo sin filas válidas: {FileName}. Registrando como procesado para evitar reintentos", fileName);
                await MarkFileAsProcessedAsync(dbContext, fileName, stoppingToken);
                return false;
            }

            var insertedCalls = await BulkInsertAsync(dbContext, rows, fileName, stoppingToken);

            if (insertedCalls.Count > 0)
            {
                _logger.LogInformation("Procesado OK: {FileName} (insertadas {RowCount} filas)", fileName, insertedCalls.Count);
                await EmitSignalREventsAsync(hubContext, insertedCalls, stoppingToken);
                return true;
            }
            else
            {
                _logger.LogWarning("⚠️ No se insertaron filas desde {FileName} (insertedCalls.Count=0). Registrando como procesado", fileName);
                await MarkFileAsProcessedAsync(dbContext, fileName, stoppingToken);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando archivo {FileName}: {ErrorMessage}. El archivo permanece en la carpeta raíz.",
                fileName, ex.Message);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
                await MarkFileAsProcessedWithErrorAsync(dbContext, fileName, ex.Message, stoppingToken);
            }
            catch (Exception markEx)
            {
                _logger.LogWarning(markEx, "No se pudo registrar el error del archivo {FileName} en la BD", fileName);
            }

            return false;
        }
    }

    public async Task ExecuteStoredProcedureAsync(IHubContext<MessagesHub> hubContext, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔄 Ejecutando ProcessNotifierCalls...");
            var startTime = DateTime.UtcNow;

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

            await dbContext.Database.ExecuteSqlRawAsync(
                "EXEC dbo.ProcessNotifierCalls",
                cancellationToken);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogInformation("✅ ProcessNotifierCalls completado en {Duration} ms", duration);

            try
            {
                await hubContext.Clients.All.SendAsync("CallViewsUpdated", cancellationToken: cancellationToken);
                _logger.LogInformation("✅ CallViewsUpdated emitido OK");
            }
            catch (Exception signalREx)
            {
                _logger.LogError(signalREx, "❌ Error emitiendo CallViewsUpdated");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error ejecutando ProcessNotifierCalls");
        }
    }

    private async Task EmitSignalREventsAsync(
        IHubContext<MessagesHub> hubContext,
        List<NotifierCallsStaging> insertedCalls,
        CancellationToken stoppingToken)
    {
        try
        {
            int batchThreshold = 5;

            if (insertedCalls.Count <= batchThreshold)
            {
                _logger.LogInformation("📡 Emitiendo {Count} eventos NewMissedCall individuales (archivo pequeño)", insertedCalls.Count);

                foreach (var call in insertedCalls)
                {
                    var spainTime = SpainTime.ToSpainTime(call.DateAndTime);

                    await hubContext.Clients.All.SendAsync("NewMissedCall", new
                    {
                        id = call.Id,
                        dateAndTime = spainTime,
                        phoneNumber = call.PhoneNumber,
                        statusText = call.StatusText ?? "N/A",
                        sourceFile = call.SourceFile,
                        loadedAt = call.LoadedAt
                    }, cancellationToken: stoppingToken);
                }

                _logger.LogInformation("✅ {Count} eventos NewMissedCall emitidos OK", insertedCalls.Count);
            }
            else
            {
                _logger.LogInformation("📡 Emitiendo CallsUpdated (archivo grande con {Count} filas)", insertedCalls.Count);
                await hubContext.Clients.All.SendAsync("CallsUpdated", cancellationToken: stoppingToken);
                _logger.LogInformation("✅ CallsUpdated emitido OK");
            }
        }
        catch (Exception signalREx)
        {
            _logger.LogError(signalREx, "❌ Error emitiendo eventos SignalR");
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

    private async Task MarkFileAsProcessedAsync(
        NotificationDbContext dbContext,
        string fileName,
        CancellationToken stoppingToken)
    {
        try
        {
            var markerRow = new NotifierCallsStaging
            {
                DateAndTime = DateTime.UtcNow,
                PhoneNumber = "[ARCHIVO_VACIO]",
                StatusText = "[PROCESADO_SIN_FILAS]",
                SourceFile = fileName,
                LoadedAt = DateTime.UtcNow
            };

            dbContext.NotifierCallsStaging.Add(markerRow);
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogDebug("Archivo {FileName} marcado como procesado (sin filas válidas)", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo marcar el archivo {FileName} como procesado", fileName);
        }
    }

    private async Task MarkFileAsProcessedWithErrorAsync(
        NotificationDbContext dbContext,
        string fileName,
        string errorMessage,
        CancellationToken stoppingToken)
    {
        try
        {
            var errorRow = new NotifierCallsStaging
            {
                DateAndTime = DateTime.UtcNow,
                PhoneNumber = "[ERROR]",
                StatusText = $"[ERROR_PROCESAMIENTO: {errorMessage}]",
                SourceFile = fileName,
                LoadedAt = DateTime.UtcNow
            };

            dbContext.NotifierCallsStaging.Add(errorRow);
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogDebug("Archivo {FileName} marcado como procesado con error para evitar reintentos", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo marcar el archivo {FileName} como procesado con error", fileName);
        }
    }

    private async Task<List<NotifierRow>> ReadFileWithRetryAsync(
        string fullPath,
        CancellationToken stoppingToken)
    {
        const int MaxReadAttempts = 15;
        var readRetryDelay = TimeSpan.FromMilliseconds(300);

        for (var attempt = 1; attempt <= MaxReadAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    _logger.LogWarning("Archivo no existe: {FilePath}", fullPath);
                    return new List<NotifierRow>();
                }

                if (!await IsFileStableAsync(fullPath, stoppingToken))
                {
                    throw new IOException("Archivo en copia (tamaño inestable)");
                }

                return await ReadCsvAsync(fullPath, stoppingToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == MaxReadAttempts)
                {
                    _logger.LogError(ex, "No se pudo leer el archivo tras {Attempts} intentos: {FilePath}",
                        attempt, fullPath);
                    return new List<NotifierRow>();
                }

                await Task.Delay(readRetryDelay, stoppingToken);
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

        var timeWindow = now.AddSeconds(-2);
        var insertedCalls = await dbContext.NotifierCallsStaging
            .Where(c => c.SourceFile == sourceFile &&
                       c.LoadedAt.HasValue &&
                       c.LoadedAt.Value >= timeWindow &&
                       c.LoadedAt.Value <= now.AddSeconds(1))
            .OrderByDescending(c => c.Id)
            .Take(rows.Count)
            .ToListAsync(stoppingToken);

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

    private sealed record NotifierRow(
        DateTime DateAndTime,
        string PhoneNumber,
        string StatusText,
        string SourceFile);
}
