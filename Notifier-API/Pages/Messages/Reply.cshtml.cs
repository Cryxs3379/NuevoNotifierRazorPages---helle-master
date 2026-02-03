using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Notifier.Messages.Application.SendSms;
using NotifierAPI.Configuration;
using System.Text.RegularExpressions;

namespace NotifierAPI.Pages.Messages;

public class MessagesReplyModel : PageModel
{
    private readonly ILogger<MessagesReplyModel> _logger;
    private readonly EsendexSettings _esendexSettings;
    private readonly ISendSmsAndNotifyUseCase _useCase;

    public MessagesReplyModel(
        ILogger<MessagesReplyModel> logger,
        EsendexSettings esendexSettings,
        ISendSmsAndNotifyUseCase useCase)
    {
        _logger = logger;
        _esendexSettings = esendexSettings;
        _useCase = useCase;
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

            var originator = _esendexSettings.AccountReference ?? AccountRef ?? "UNKNOWN";
            var result = await _useCase.ExecuteAsync(
                new SendSmsCommand(To, Message, originator, AccountRef, null),
                HttpContext.RequestAborted);

            if (result.ErrorType == SendSmsErrorType.InvalidRequest ||
                result.ErrorType == SendSmsErrorType.InvalidPhone)
            {
                ErrorMessage = result.ErrorMessage ?? "Solicitud inválida.";
                return Page();
            }

            if (result.ErrorType == SendSmsErrorType.SendFailed)
            {
                ErrorMessage = "No se pudo enviar el mensaje. Verifica tu conexión e inténtalo de nuevo.";
                return Page();
            }

            if (result.ErrorType == SendSmsErrorType.Unexpected)
            {
                ErrorMessage = $"Error al enviar el mensaje: {result.ErrorMessage}";
                return Page();
            }

            if (result.Sent && !result.Saved)
            {
                ErrorMessage = "Mensaje enviado exitosamente, pero no se pudo guardar en la base de datos.";
                return Page();
            }

            SuccessMessage = $"Mensaje enviado exitosamente a {To}.";
            
            // Limpiar el formulario tras enviar (cuando el usuario entra desde menú Enviar)
            ModelState.Clear();
            To = string.Empty;
            Message = string.Empty;
            AccountRef = null;

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

