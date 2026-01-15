namespace NotifierAPI.Data;

public class NotifierSmsMessage
{
    public long Id { get; set; }
    public string Originator { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public byte Direction { get; set; } // 0 = Received, 1 = Sent
    public DateTime MessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ProviderMessageId { get; set; }
}
