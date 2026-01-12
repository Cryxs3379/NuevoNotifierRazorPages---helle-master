namespace NotifierAPI.Models;

public class SendMessageResponse
{
    public string Id { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SubmittedUtc { get; set; }
}
