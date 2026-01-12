namespace NotifierAPI.Models;

public class SendMessageRequest
{
    public string To { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? AccountRef { get; set; }
}
