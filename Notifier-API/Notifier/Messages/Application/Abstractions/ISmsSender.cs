namespace Notifier.Messages.Application.Abstractions;

public interface ISmsSender
{
    Task<SendResult> SendAsync(string toE164, string message, string? accountRef, CancellationToken ct);
}

public sealed record SendResult(string Id, DateTime SubmittedUtc);
