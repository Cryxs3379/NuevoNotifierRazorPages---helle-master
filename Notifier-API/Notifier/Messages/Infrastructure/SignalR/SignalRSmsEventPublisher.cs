using Microsoft.AspNetCore.SignalR;
using Notifier.Messages.Application;
using Notifier.Messages.Application.Abstractions;
using NotifierAPI.Hubs;

namespace Notifier.Messages.Infrastructure.SignalR;

public sealed class SignalRSmsEventPublisher : IDomainEventPublisher
{
    private readonly IHubContext<MessagesHub> _hubContext;
    private readonly ILogger<SignalRSmsEventPublisher> _logger;

    public SignalRSmsEventPublisher(
        IHubContext<MessagesHub> hubContext,
        ILogger<SignalRSmsEventPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(SmsSentEvent evt, CancellationToken ct)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("NewSentMessage", new
            {
                id = evt.MessageId.ToString(),
                customerPhone = evt.CustomerPhoneCanonical,
                originator = evt.Originator,
                recipient = evt.RecipientE164,
                body = evt.Body,
                direction = 1,
                messageAt = evt.MessageAtUtc.ToString("O"),
                sentBy = evt.SentBy
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to emit NewSentMessage event via SignalR");
        }
    }
}
