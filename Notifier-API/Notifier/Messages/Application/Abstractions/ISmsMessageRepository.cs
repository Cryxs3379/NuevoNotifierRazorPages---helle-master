namespace Notifier.Messages.Application.Abstractions;

public interface ISmsMessageRepository
{
    Task<long?> SaveSentAsync(
        string originator,
        string recipientE164,
        string body,
        string type,
        DateTime messageAtUtc,
        string? sentBy,
        CancellationToken ct);
}
