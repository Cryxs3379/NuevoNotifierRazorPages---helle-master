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
        private static readonly TimeZoneInfo SpainTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time");

        private readonly NotificationDbContext _context;
        private readonly ILogger<MissedCallsController> _logger;

        public MissedCallsController(NotificationDbContext context, ILogger<MissedCallsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private DateTime NormalizeUtc(DateTime date)
        {
            if (date.Kind == DateTimeKind.Unspecified)
            {
                _logger.LogInformation("IncomingCall.DateAndTime has Unspecified kind. Forcing to UTC. Value: {Date}", date);
                return DateTime.SpecifyKind(date, DateTimeKind.Utc);
            }

            if (date.Kind == DateTimeKind.Local)
            {
                return date.ToUniversalTime();
            }

            return date;
        }

        private DateTime ToSpainTime(DateTime utcDate)
        {
            var utc = NormalizeUtc(utcDate);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, SpainTimeZone);
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
                        DateAndTime = ToSpainTime(call.DateAndTime),
                        call.PhoneNumber,
                        call.Status,
                        call.ClientCalledAgain,
                        call.AnswerCall,
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

                var spainNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SpainTimeZone);

                var payload = calls.Select(call =>
                {
                    var spainTime = ToSpainTime(call.DateAndTime);
                    var statusInfo = GetStatusInfo(call.Status);
                    return new
                    {
                        call.Id,
                        DateAndTime = spainTime,
                        call.PhoneNumber,
                        call.Status,
                        call.ClientCalledAgain,
                        call.AnswerCall,
                        IsMissedCall = statusInfo.IsMissed,
                        IsAnswered = statusInfo.IsAnswered,
                        StatusText = statusInfo.StatusText,
                        TimeAgo = spainNow - spainTime
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
                        DateAndTime = ToSpainTime(lastMissedCall.DateAndTime),
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
                    DateAndTime = ToSpainTime(call.DateAndTime),
                    call.PhoneNumber,
                    call.Status,
                    call.ClientCalledAgain,
                    call.AnswerCall,
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
    }
}
