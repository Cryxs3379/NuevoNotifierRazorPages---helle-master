namespace NotifierAPI.Data;

public class ConversationState
{
    public string CustomerPhone { get; set; } = string.Empty; // PK
    public DateTime? LastInboundAt { get; set; }
    public DateTime? LastOutboundAt { get; set; }
    public DateTime? LastReadInboundAt { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? AssignedUntil { get; set; }
    public DateTime UpdatedAt { get; set; }
}
