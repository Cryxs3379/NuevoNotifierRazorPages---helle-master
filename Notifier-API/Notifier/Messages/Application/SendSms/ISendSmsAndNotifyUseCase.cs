namespace Notifier.Messages.Application.SendSms;

public interface ISendSmsAndNotifyUseCase
{
    Task<SendSmsResult> ExecuteAsync(SendSmsCommand command, CancellationToken ct);
}
