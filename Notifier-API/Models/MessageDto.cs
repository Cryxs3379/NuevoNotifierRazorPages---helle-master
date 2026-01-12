namespace NotifierAPI.Models;

public class MessageDto
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime ReceivedUtc { get; set; }
}

