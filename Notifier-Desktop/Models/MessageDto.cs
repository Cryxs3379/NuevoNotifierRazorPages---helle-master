namespace NotifierDesktop.Models;

public class MessageDto
{
    public long Id { get; set; }
    public string CustomerPhone { get; set; } = string.Empty; // Para WinForms
    public string Originator { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public byte Direction { get; set; }
    public DateTime MessageAt { get; set; }
    public string? SentBy { get; set; } // Nombre del recepcionista (solo para OUTBOUND, nullable)
}

public class MessagesResponse
{
    public List<MessageDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class SendMessageRequest
{
    public string To { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SentBy { get; set; } // Nombre del recepcionista que envía el mensaje (opcional)
}

public class SendMessageResponse
{
    public bool Success { get; set; }
    public long? Id { get; set; }
    public string? Error { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public bool EsendexConfigured { get; set; }
}

public class ConversationDto
{
    public string CustomerPhone { get; set; } = string.Empty;
    public DateTime? LastInboundAt { get; set; }
    public DateTime? LastOutboundAt { get; set; }
    public DateTime? LastReadInboundAt { get; set; }
    public bool PendingReply { get; set; }
    public bool Unread { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? AssignedUntil { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public string Preview { get; set; } = string.Empty;
}

public class ConversationsResponse
{
    public List<ConversationDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class ClaimResponse
{
    public bool Success { get; set; }
    public bool WasAlreadyAssigned { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? AssignedUntil { get; set; }
}
