namespace Notifier.Messages.Application.SendSms;

public sealed record SendSmsCommand(
    string To,
    string Message,
    string Originator,
    string? AccountRef,
    string? SentBy);
