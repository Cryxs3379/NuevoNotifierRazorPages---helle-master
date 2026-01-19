namespace NotifierDesktop.ViewModels;

public class ConversationVm
{
    public string Phone { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public DateTime? LastMessageAt { get; set; }
    public DateTime? LastOutboundAt { get; set; } // Para filtrar tab "Enviados"
    public bool PendingReply { get; set; }
    public bool Unread { get; set; }
    public int? UnreadCount { get; set; } // Opcional
    public string? AssignedTo { get; set; }
    public DateTime? AssignedUntil { get; set; }
    public string? LastRespondedBy { get; set; } // Quién respondió por última vez (SentBy del último OUTBOUND)
}
