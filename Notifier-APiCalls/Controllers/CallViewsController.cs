using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;
using NotifierAPI.Models;

namespace NotifierAPI.Controllers;

[ApiController]
[Route("api/v1/calls/views")]
public class CallViewsController : ControllerBase
{
    private readonly NotificationDbContext _context;
    private readonly ILogger<CallViewsController> _logger;

    public CallViewsController(NotificationDbContext context, ILogger<CallViewsController> logger)
    {
        _context = context;
        _logger = logger;
    }


    [HttpGet("outgoing-24h")]
    public async Task<ActionResult<List<CallWithClientDto>>> GetOutgoing24h(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📞 Obteniendo llamadas salientes 24h");

            var result = await _context.Outgoing24h
                .AsNoTracking()
                .OrderByDescending(c => c.DateAndTime)
                .Take(200)
                .Select(c => new CallWithClientDto
                {
                    Id = c.Id,
                    DateAndTime = c.DateAndTime, // UTC, navegador formatea
                    PhoneNumber = c.PhoneNumber,
                    NombreCompleto = c.NombreCompleto ?? "",
                    NombrePila = c.NombrePila ?? ""
                })
                .ToListAsync(cancellationToken);

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

            var result = await _context.IncomingNoAtendidas24h
                .AsNoTracking()
                .OrderByDescending(c => c.DateAndTime)
                .Take(200)
                .Select(c => new CallWithClientDto
                {
                    Id = c.Id,
                    DateAndTime = c.DateAndTime, // UTC, navegador formatea
                    PhoneNumber = c.PhoneNumber,
                    NombreCompleto = c.NombreCompleto ?? "",
                    NombrePila = c.NombrePila ?? ""
                })
                .ToListAsync(cancellationToken);

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

            var result = await _context.IncomingAtendidas24h
                .AsNoTracking()
                .OrderByDescending(c => c.DateAndTime)
                .Take(200)
                .Select(c => new CallWithClientDto
                {
                    Id = c.Id,
                    DateAndTime = c.DateAndTime, // UTC, navegador formatea
                    PhoneNumber = c.PhoneNumber,
                    NombreCompleto = c.NombreCompleto ?? "",
                    NombrePila = c.NombrePila ?? ""
                })
                .ToListAsync(cancellationToken);

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
