namespace NotifierAPI.Models;

public class SendMessageRequest
{
    public string To { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AccountRef { get; set; }
    public string? SentBy { get; set; } // Nombre del recepcionista que envía el mensaje (opcional)
}
