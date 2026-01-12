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

        public MissedCallsController(NotificationDbContext context)
        {
            _context = context;
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

                return Ok(new { 
                    success = true, 
                    count = calls.Count, 
                    data = calls 
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
                    .Select(call => new
                    {
                        call.Id,
                        call.DateAndTime,
                        call.PhoneNumber,
                        call.Status,
                        call.ClientCalledAgain,
                        call.AnswerCall,
                        IsMissedCall = call.Status == 0 && call.AnswerCall == null,
                        IsAnswered = call.Status != 0 || call.AnswerCall != null,
                        TimeAgo = DateTime.Now - call.DateAndTime
                    })
                    .ToListAsync();

                return Ok(calls);
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
                    .CountAsync(call => call.Status == 0 && call.AnswerCall == null);

                var todayMissedCalls = await _context.IncomingCalls
                    .CountAsync(call => call.Status == 0 && call.AnswerCall == null && 
                                      call.DateAndTime.Date == DateTime.Today);

                var thisWeekMissedCalls = await _context.IncomingCalls
                    .CountAsync(call => call.Status == 0 && call.AnswerCall == null && 
                                      call.DateAndTime >= DateTime.Today.AddDays(-7));

                var lastMissedCall = await _context.IncomingCalls
                    .Where(call => call.Status == 0 && call.AnswerCall == null)
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
                        lastMissedCall.DateAndTime,
                        lastMissedCall.PhoneNumber
                    } : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
            }
        }
    }
}
