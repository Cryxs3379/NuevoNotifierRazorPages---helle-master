namespace NotifierDesktop.ViewModels;

public enum MessageDirection { Inbound = 0, Outbound = 1 }

public class MessageVm
{
    public long Id { get; set; }
    public MessageDirection Direction { get; set; }
    public DateTime At { get; set; }
    public string Text { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string? SentBy { get; set; } // Nombre del recepcionista (solo para OUTBOUND, nullable)
}
