using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NotifierAPI.Services;
using NotifierAPI.Models;

namespace NotifierAPI.Pages.Calls;

public class CallsIndexModel : PageModel
{
    private readonly IMissedCallsService _missedCallsService;
    private readonly ILogger<CallsIndexModel> _logger;
    private readonly IConfiguration _configuration;

    public CallsIndexModel(
        IMissedCallsService missedCallsService, 
        ILogger<CallsIndexModel> logger,
        IConfiguration configuration)
    {
        _missedCallsService = missedCallsService;
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty(SupportsGet = true)]
    public int Limit { get; set; } = 100;

    public List<MissedCallDto>? MissedCalls { get; set; }
    public MissedCallsStatsResponse? Stats { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsApiAvailable { get; set; } = true;
    public string ApiBaseUrl => _configuration["MissedCallsAPI:BaseUrl"] ?? "http://localhost:5000";
    public string LastCallTime { get; set; } = "N/A";

    public async Task OnGetAsync()
    {
        try
        {
            // Validar límite
            if (Limit < 10) Limit = 10;
            if (Limit > 500) Limit = 500;

            _logger.LogInformation("Fetching missed calls with limit={Limit}", Limit);

            // Primero verificar si la API está disponible
            IsApiAvailable = await _missedCallsService.TestConnectionAsync();

            if (!IsApiAvailable)
            {
                _logger.LogWarning("Missed Calls API is not available");
                ErrorMessage = "La API de llamadas perdidas no está disponible.";
                return;
            }

            // Obtener estadísticas
            Stats = await _missedCallsService.GetMissedCallsStatsAsync(HttpContext.RequestAborted);

            if (Stats?.LastMissedCall != null)
            {
                try
                {
                    var lastCall = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        Stats.LastMissedCall.ToString() ?? "{}");
                    
                    if (lastCall != null && lastCall.ContainsKey("dateAndTime"))
                    {
                        if (DateTime.TryParse(lastCall["dateAndTime"].ToString(), out var lastCallDate))
                        {
                            LastCallTime = FormatRelativeTime(lastCallDate);
                        }
                    }
                }
                catch
                {
                    LastCallTime = "N/A";
                }
            }

            // Obtener llamadas perdidas
            var response = await _missedCallsService.GetMissedCallsAsync(Limit, HttpContext.RequestAborted);

            if (response == null)
            {
                ErrorMessage = "No se pudieron obtener las llamadas perdidas.";
                return;
            }

            MissedCalls = response.Data;

            _logger.LogInformation("Retrieved {Count} missed calls", MissedCalls?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching missed calls");
            ErrorMessage = "Ocurrió un error al cargar las llamadas perdidas.";
            IsApiAvailable = false;
        }
    }

    private string FormatRelativeTime(DateTime dateTime)
    {
        var diff = DateTime.Now - dateTime;

        if (diff.TotalMinutes < 1)
            return "Hace unos segundos";
        if (diff.TotalMinutes < 60)
            return $"Hace {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24)
            return $"Hace {(int)diff.TotalHours}h";
        if (diff.TotalDays < 7)
            return $"Hace {(int)diff.TotalDays}d";

        return dateTime.ToString("dd/MM/yyyy HH:mm");
    }
}

