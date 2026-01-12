using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NotifierAPI.Services;
using NotifierAPI.Models;

namespace NotifierAPI.Pages.Messages;

public class MessagesIndexModel : PageModel
{
    private readonly IInboxService _inboxService;
    private readonly ILogger<MessagesIndexModel> _logger;

    public MessagesIndexModel(IInboxService inboxService, ILogger<MessagesIndexModel> logger)
    {
        _inboxService = inboxService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string Direction { get; set; } = "inbound";

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? AccountRef { get; set; }

    public MessagesResponse? Messages { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalPages => Messages != null && PageSize > 0 
        ? (int)Math.Ceiling((double)Messages.Total / PageSize) 
        : 0;
    
    // Propiedad para la vista (alias de CurrentPage)
    public int PageNumber => CurrentPage;

    public async Task OnGetAsync()
    {
        try
        {
            // Validar dirección
            if (Direction != "inbound" && Direction != "outbound")
            {
                Direction = "inbound";
            }

            // Validar página
            if (CurrentPage < 1) CurrentPage = 1;

            // Validar tamaño de página
            if (PageSize < 10) PageSize = 10;
            if (PageSize > 200) PageSize = 200;

            _logger.LogInformation("Fetching messages: direction={Direction}, page={Page}, pageSize={PageSize}, accountRef={AccountRef}", 
                Direction, CurrentPage, PageSize, AccountRef);

            Messages = await _inboxService.GetMessagesAsync(
                Direction, 
                CurrentPage, 
                PageSize, 
                AccountRef, 
                HttpContext.RequestAborted);

            _logger.LogInformation("Retrieved {Count} messages out of {Total}", 
                Messages?.Items?.Count ?? 0, 
                Messages?.Total ?? 0);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Authentication failed");
            ErrorMessage = "Error de autenticación con Esendex. Verifica las credenciales.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed");
            ErrorMessage = "No se pudo conectar con el servicio Esendex. Inténtalo más tarde.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error");
            ErrorMessage = "Ocurrió un error inesperado al cargar los mensajes.";
        }
    }
}

