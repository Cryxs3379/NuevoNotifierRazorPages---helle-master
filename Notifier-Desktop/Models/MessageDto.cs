namespace NotifierDesktop.Models;

public class MessageDto
{
    public long Id { get; set; }
    public string Originator { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public byte Direction { get; set; }
    public DateTime MessageAt { get; set; }
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
