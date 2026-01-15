using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using NotifierAPI.Services;
using NotifierAPI.Models;
using NotifierAPI.Configuration;
using NotifierAPI.Hubs;
using System.Text.RegularExpressions;

namespace NotifierAPI.Pages.Messages;

public class MessagesReplyModel : PageModel
{
    private readonly ISendService _sendService;
    private readonly ILogger<MessagesReplyModel> _logger;
    private readonly SmsMessageRepository _smsRepository;
    private readonly IHubContext<MessagesHub> _hubContext;
    private readonly EsendexSettings _esendexSettings;

    public MessagesReplyModel(
        ISendService sendService, 
        ILogger<MessagesReplyModel> logger,
        SmsMessageRepository smsRepository,
        IHubContext<MessagesHub> hubContext,
        EsendexSettings esendexSettings)
    {
        _sendService = sendService;
        _logger = logger;
        _smsRepository = smsRepository;
        _hubContext = hubContext;
        _esendexSettings = esendexSettings;
    }

    [BindProperty]
    [Required(ErrorMessage = "El número de destino es requerido")]
    [RegularExpression(@"^\+\d{6,15}$", ErrorMessage = "Formato inválido. Usa +34600123456")]
    public string To { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "El mensaje es requerido")]
    [StringLength(612, MinimumLength = 1, ErrorMessage = "El mensaje debe tener entre 1 y 612 caracteres")]
    public string Message { get; set; } = string.Empty;

    [BindProperty]
    public string? AccountRef { get; set; }

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet(string? to)
    {
        // Si viene número por query (responder), normalizar y prellenar; si no, dejar en blanco
        if (!string.IsNullOrWhiteSpace(to))
        {
            var normalized = to.Trim();
            // Quitar espacios/separadores comunes
            normalized = Regex.Replace(normalized, @"[\s-]", "");
            // Añadir '+' si falta y son solo dígitos
            if (!normalized.StartsWith("+") && Regex.IsMatch(normalized, @"^\d{6,15}$"))
            {
                normalized = "+" + normalized;
            }
            To = normalized;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Por favor, corrige los errores del formulario.";
            return Page();
        }

        try
        {
            _logger.LogInformation("Sending message to {To}, length={Length}", 
                To.Substring(To.Length - 3), Message.Length);

            var result = await _sendService.SendAsync(To, Message, AccountRef, HttpContext.RequestAborted);

            _logger.LogInformation("Message sent successfully. ID={Id}", result.Id);

            // Intentar guardar en BD (después del envío exitoso)
            var originator = _esendexSettings.AccountReference ?? AccountRef ?? "UNKNOWN";
            var savedId = await _smsRepository.SaveSentAsync(
                originator: originator,
                recipient: To,
                body: Message,
                type: "SMS",
                messageAt: result.SubmittedUtc,
                cancellationToken: HttpContext.RequestAborted);
            
            if (!savedId.HasValue)
            {
                // Emitir evento SignalR para notificar el error
                try
                {
                    await _hubContext.Clients.All.SendAsync("DbError", 
                        $"No se pudo guardar mensaje enviado en BD: To={To}", 
                        HttpContext.RequestAborted);
                }
                catch (Exception signalREx)
                {
                    _logger.LogWarning(signalREx, "Failed to emit DbError event via SignalR");
                }
                
                // Mostrar advertencia al usuario (pero el mensaje sí se envió)
                ErrorMessage = $"Mensaje enviado exitosamente, pero no se pudo guardar en la base de datos. ID: {result.Id}";
                return Page();
            }

            // Emitir SignalR "NewSentMessage" para notificar a clientes (WinForms, etc.)
            try
            {
                await _hubContext.Clients.All.SendAsync("NewSentMessage", new
                {
                    id = savedId.Value.ToString(),
                    customerPhone = To,
                    originator = originator,
                    recipient = To,
                    body = Message,
                    direction = 1,
                    messageAt = result.SubmittedUtc.ToString("O")
                }, HttpContext.RequestAborted);
            }
            catch (Exception signalREx)
            {
                _logger.LogWarning(signalREx, "Failed to emit NewSentMessage event via SignalR");
            }

            SuccessMessage = $"Mensaje enviado exitosamente a {To}. ID: {result.Id}";
            
            // Limpiar el formulario tras enviar (cuando el usuario entra desde menú Enviar)
            ModelState.Clear();
            To = string.Empty;
            Message = string.Empty;
            AccountRef = null;

            return Page();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to send message");
            ErrorMessage = "No se pudo enviar el mensaje. Verifica tu conexión e inténtalo de nuevo.";
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending message");
            ErrorMessage = $"Error al enviar el mensaje: {ex.Message}";
            return Page();
        }
    }
}

