using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;
using NotifierAPI.Models;

namespace NotifierAPI.Controllers;

[ApiController]
[Route("api/v1/calls/views")]
public class CallViewsController : ControllerBase
{
    private static readonly TimeZoneInfo SpainTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

    private readonly NotificationDbContext _context;
    private readonly ILogger<CallViewsController> _logger;

    public CallViewsController(NotificationDbContext context, ILogger<CallViewsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private DateTime ToSpainTime(DateTime utcDate)
    {
        if (utcDate.Kind == DateTimeKind.Unspecified)
        {
            utcDate = DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);
        }
        else if (utcDate.Kind == DateTimeKind.Local)
        {
            utcDate = utcDate.ToUniversalTime();
        }

        return TimeZoneInfo.ConvertTimeFromUtc(utcDate, SpainTimeZone);
    }

    private CallWithClientDto MapToDto(Outgoing24hRow row)
    {
        return new CallWithClientDto
        {
            Id = row.Id,
            DateAndTime = ToSpainTime(row.DateAndTime),
            PhoneNumber = row.PhoneNumber,
            NombreCompleto = row.NombreCompleto ?? "",
            NombrePila = row.NombrePila ?? ""
        };
    }

    private CallWithClientDto MapToDto(IncomingNoAtendidas24hRow row)
    {
        return new CallWithClientDto
        {
            Id = row.Id,
            DateAndTime = ToSpainTime(row.DateAndTime),
            PhoneNumber = row.PhoneNumber,
            NombreCompleto = row.NombreCompleto ?? "",
            NombrePila = row.NombrePila ?? ""
        };
    }

    private CallWithClientDto MapToDto(IncomingAtendidas24hRow row)
    {
        return new CallWithClientDto
        {
            Id = row.Id,
            DateAndTime = ToSpainTime(row.DateAndTime),
            PhoneNumber = row.PhoneNumber,
            NombreCompleto = row.NombreCompleto ?? "",
            NombrePila = row.NombrePila ?? ""
        };
    }

    [HttpGet("outgoing-24h")]
    public async Task<ActionResult<List<CallWithClientDto>>> GetOutgoing24h(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📞 Obteniendo llamadas salientes 24h");

            var calls = await _context.Outgoing24h
                .OrderByDescending(c => c.DateAndTime)
                .Take(200)
                .ToListAsync(cancellationToken);

            var result = calls.Select(MapToDto).ToList();

            _logger.LogInformation("✅ Retornadas {Count} llamadas salientes 24h", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo llamadas salientes 24h");
            return StatusCode(500, new { error = "Error al obtener llamadas salientes 24h" });
        }
    }

    [HttpGet("incoming-noatendidas-24h")]
    public async Task<ActionResult<List<CallWithClientDto>>> GetIncomingNoAtendidas24h(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📞 Obteniendo llamadas entrantes NO atendidas 24h");

            var calls = await _context.IncomingNoAtendidas24h
                .OrderByDescending(c => c.DateAndTime)
                .Take(200)
                .ToListAsync(cancellationToken);

            var result = calls.Select(MapToDto).ToList();

            _logger.LogInformation("✅ Retornadas {Count} llamadas entrantes NO atendidas 24h", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo llamadas entrantes NO atendidas 24h");
            return StatusCode(500, new { error = "Error al obtener llamadas entrantes NO atendidas 24h" });
        }
    }

    [HttpGet("incoming-atendidas-24h")]
    public async Task<ActionResult<List<CallWithClientDto>>> GetIncomingAtendidas24h(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📞 Obteniendo llamadas entrantes atendidas 24h");

            var calls = await _context.IncomingAtendidas24h
                .OrderByDescending(c => c.DateAndTime)
                .Take(200)
                .ToListAsync(cancellationToken);

            var result = calls.Select(MapToDto).ToList();

            _logger.LogInformation("✅ Retornadas {Count} llamadas entrantes atendidas 24h", result.Count);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo llamadas entrantes atendidas 24h");
            return StatusCode(500, new { error = "Error al obtener llamadas entrantes atendidas 24h" });
        }
    }
}
