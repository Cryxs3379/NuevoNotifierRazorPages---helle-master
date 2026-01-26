namespace NotifierAPI.Models;

public class MissedCallRealtimeDto
{
    public long Id { get; set; }
    public DateTime DateAndTime { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? StatusText { get; set; }
    public string? SourceFile { get; set; }
    public DateTime? LoadedAt { get; set; }
}
