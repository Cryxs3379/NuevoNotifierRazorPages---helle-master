namespace Notifier.Messages.Application.Abstractions;

public interface IConversationStateUpdater
{
    Task UpsertOutboundAsync(string customerPhoneCanonical, DateTime messageAtUtc, CancellationToken ct);
}
