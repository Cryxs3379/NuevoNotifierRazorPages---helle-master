namespace Notifier.Messages.Application.Abstractions;

public interface IDomainEventPublisher
{
    Task PublishAsync(SmsSentEvent evt, CancellationToken ct);
}
