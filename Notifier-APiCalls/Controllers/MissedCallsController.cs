using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;
using NotifierAPI.Models;

namespace NotifierAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MissedCallsController : ControllerBase
    {
        private readonly NotificationDbContext _context;
        private readonly ILogger<MissedCallsController> _logger;

        public MissedCallsController(NotificationDbContext context, ILogger<MissedCallsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private (bool IsMissed, bool IsAnswered, string StatusText) GetStatusInfo(byte status)
        {
            var isMissed = status == 1;
            var isAnswered = status == 0;
            var statusText = isMissed ? "Perdida" : "Respondida";
            return (isMissed, isAnswered, statusText);
        }

        /// <summary>
        /// Endpoint de prueba para verificar que la API funciona
        /// </summary>
        /// <returns>Mensaje de confirmación</returns>
        [HttpGet("test")]
        public ActionResult<object> Test()
        {
            return Ok(new { 
                success = true, 
                message = "API funcionando correctamente", 
                timestamp = DateTime.Now,
                version = "1.0.0"
            });
        }

        /// <summary>
        /// Endpoint para probar la conexión a la base de datos
        /// </summary>
        /// <returns>Estado de la conexión</returns>
        [HttpGet("test-db")]
        public async Task<ActionResult<object>> TestDatabase()
        {
            try
            {
                // Probar conexión básica
                await _context.Database.OpenConnectionAsync();
                _context.Database.CloseConnection();

                // Probar consulta simple
                var count = await _context.Database.ExecuteSqlRawAsync("SELECT 1");

                return Ok(new { 
                    success = true, 
                    message = "Conexión a base de datos exitosa", 
                    timestamp = DateTime.Now,
                    connectionTest = "OK"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false,
                    message = "Error de conexión a base de datos", 
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Obtiene las últimas llamadas perdidas
        /// </summary>
        /// <param name="limit">Número máximo de registros a retornar (por defecto 100)</param>
        /// <returns>Lista de llamadas perdidas ordenadas por fecha descendente</returns>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<IncomingCall>>> GetMissedCalls([FromQuery] int limit = 100)
        {
            try
            {
                // Test database connection first
                await _context.Database.OpenConnectionAsync();
                _context.Database.CloseConnection();

                // Devolver todas las llamadas (perdidas y respondidas), más recientes primero
                var calls = await _context.IncomingCalls
                    .OrderByDescending(call => call.DateAndTime)
                    .Take(limit)
                    .ToListAsync();

                var mappedCalls = calls.Select(call =>
                {
                    var statusInfo = GetStatusInfo(call.Status);
                    return new
                    {
                        call.Id,
                        DateAndTime = call.DateAndTime,
                        call.PhoneNumber,
                        call.Status,
                        call.ClientCalledAgain,
                        Recall = call.Recall,
                        IsMissed = statusInfo.IsMissed,
                        IsAnswered = statusInfo.IsAnswered,
                        StatusText = statusInfo.StatusText
                    };
                }).ToList();

                return Ok(new { 
                    success = true, 
                    count = mappedCalls.Count, 
                    data = mappedCalls 
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    success = false,
                    message = "Error interno del servidor", 
                    error = ex.Message,
                    innerException = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// Obtiene las últimas llamadas perdidas con información adicional
        /// </summary>
        /// <param name="limit">Número máximo de registros a retornar (por defecto 100)</param>
        /// <returns>Lista de llamadas perdidas con información detallada</returns>
        [HttpGet("detailed")]
        public async Task<ActionResult<IEnumerable<object>>> GetMissedCallsDetailed([FromQuery] int limit = 100)
        {
            try
            {
                var calls = await _context.IncomingCalls
                    .OrderByDescending(call => call.DateAndTime)
                    .Take(limit)
                    .ToListAsync();

                var utcNow = DateTime.UtcNow;

                var payload = calls.Select(call =>
                {
                    var statusInfo = GetStatusInfo(call.Status);
                    return new
                    {
                        call.Id,
                        DateAndTime = call.DateAndTime,
                        call.PhoneNumber,
                        call.Status,
                        call.ClientCalledAgain,
                        Recall = call.Recall,
                        IsMissedCall = statusInfo.IsMissed,
                        IsAnswered = statusInfo.IsAnswered,
                        StatusText = statusInfo.StatusText,
                        TimeAgo = utcNow - call.DateAndTime
                    };
                }).ToList();

                return Ok(payload);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de llamadas perdidas
        /// </summary>
        /// <returns>Estadísticas de llamadas perdidas</returns>
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetMissedCallsStats()
        {
            try
            {
                var totalMissedCalls = await _context.IncomingCalls
                    .CountAsync(call => call.Status == 1);

                var todayMissedCalls = await _context.IncomingCalls
                    .CountAsync(call => call.Status == 1 &&
                                      call.DateAndTime.Date == DateTime.Today);

                var thisWeekMissedCalls = await _context.IncomingCalls
                    .CountAsync(call => call.Status == 1 &&
                                      call.DateAndTime >= DateTime.Today.AddDays(-7));

                var lastMissedCall = await _context.IncomingCalls
                    .Where(call => call.Status == 1)
                    .OrderByDescending(call => call.DateAndTime)
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    TotalMissedCalls = totalMissedCalls,
                    TodayMissedCalls = todayMissedCalls,
                    ThisWeekMissedCalls = thisWeekMissedCalls,
                    LastMissedCall = lastMissedCall != null ? new
                    {
                        lastMissedCall.Id,
                        DateAndTime = lastMissedCall.DateAndTime,
                        lastMissedCall.PhoneNumber
                    } : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }

        /// <summary>
        /// Diagnóstico de conversión de zona horaria para llamadas perdidas
        /// </summary>
        [HttpGet("diagnostics")]
        public async Task<ActionResult<object>> Diagnostics()
        {
            var lastCalls = await _context.IncomingCalls
                .OrderByDescending(call => call.DateAndTime)
                .Take(20)
                .ToListAsync();

            var serverNow = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            var serverOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);

            var records = lastCalls.Select(call =>
            {
                var statusInfo = GetStatusInfo(call.Status);
                return new
                {
                    call.Id,
                    DateAndTime = call.DateAndTime,
                    call.PhoneNumber,
                    call.Status,
                    call.ClientCalledAgain,
                    Recall = call.Recall,
                    IsMissed = statusInfo.IsMissed,
                    IsAnswered = statusInfo.IsAnswered,
                    StatusText = statusInfo.StatusText
                };
            }).ToList();

            return Ok(new
            {
                ServerNow = serverNow,
                UtcNow = utcNow,
                ServerOffset = serverOffset.ToString(),
                Records = records
            });
        }

        /// <summary>
        /// Obtiene llamadas perdidas desde la vista vw_Incoming_NoAtendidas_24h_ConCliente
        /// </summary>
        /// <param name="limit">Número máximo de registros a devolver (default: 200, max: 500)</param>
        /// <returns>Lista de llamadas perdidas con información del cliente</returns>
        [HttpGet("view")]
        public async Task<ActionResult<List<MissedCallWithClientNameDto>>> GetMissedCallsFromView([FromQuery] int limit = 200)
        {
            try
            {
                // Validar límite
                if (limit < 1) limit = 200;
                if (limit > 500) limit = 500;

                // Consultar desde la vista vw_Incoming_NoAtendidas_24h_ConCliente
                var calls = await _context.IncomingNoAtendidas24h
                    .AsNoTracking()
                    .OrderByDescending(c => c.DateAndTime)
                    .Take(limit)
                    .ToListAsync();

                // Convertir a DTO sin conversión de timezone
                var dtos = calls.Select(c => new MissedCallWithClientNameDto
                {
                    Id = c.Id,
                    DateAndTime = c.DateAndTime,
                    PhoneNumber = c.PhoneNumber,
                    NombrePila = c.NombrePila ?? "",
                    NombreCompleto = c.NombreCompleto ?? "",
                    AnswerCall = null, // Esta vista no tiene AnswerCall
                    Recall = c.Recall ?? 0
                }).ToList();

                return Ok(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo llamadas perdidas desde la vista");
                return StatusCode(500, new { error = "Error al obtener las llamadas perdidas", message = ex.Message });
            }
        }
    }
}
