namespace Notifier.Messages.Application;

public sealed record SmsSentEvent(
    long MessageId,
    string CustomerPhoneCanonical,
    string Originator,
    string RecipientE164,
    string Body,
    DateTime MessageAtUtc,
    string? SentBy);
