namespace NotifierAPI.Services;

public interface ISendService
{
    Task<SendResult> SendAsync(string to, string message, string? accountRef, CancellationToken ct = default);
}

public record SendResult(string Id, DateTime SubmittedUtc);