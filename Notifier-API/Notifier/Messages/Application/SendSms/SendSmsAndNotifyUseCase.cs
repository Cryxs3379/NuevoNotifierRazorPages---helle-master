using Notifier.Messages.Application.Abstractions;
using Notifier.Messages.Domain;

namespace Notifier.Messages.Application.SendSms;

public sealed class SendSmsAndNotifyUseCase : ISendSmsAndNotifyUseCase
{
    private readonly ISmsSender _smsSender;
    private readonly ISmsMessageRepository _smsRepository;
    private readonly IConversationStateUpdater _conversationStateUpdater;
    private readonly IDomainEventPublisher _eventPublisher;

    public SendSmsAndNotifyUseCase(
        ISmsSender smsSender,
        ISmsMessageRepository smsRepository,
        IConversationStateUpdater conversationStateUpdater,
        IDomainEventPublisher eventPublisher)
    {
        _smsSender = smsSender;
        _smsRepository = smsRepository;
        _conversationStateUpdater = conversationStateUpdater;
        _eventPublisher = eventPublisher;
    }

    public async Task<SendSmsResult> ExecuteAsync(SendSmsCommand command, CancellationToken ct)
    {
        if (command == null ||
            string.IsNullOrWhiteSpace(command.To) ||
            string.IsNullOrWhiteSpace(command.Message))
        {
            return SendSmsResult.Fail(SendSmsErrorType.InvalidRequest, "to y message son requeridos");
        }

        if (!PhoneNumber.TryParse(command.To, out var phoneNumber))
        {
            return SendSmsResult.Fail(SendSmsErrorType.InvalidPhone, "to debe ser un número telefónico válido (formato E.164: +XXXXXXXX)");
        }

        var originator = string.IsNullOrWhiteSpace(command.Originator) ? "UNKNOWN" : command.Originator.Trim();
        var sentBy = string.IsNullOrWhiteSpace(command.SentBy) ? null : command.SentBy.Trim();

        try
        {
            var sendResult = await _smsSender.SendAsync(phoneNumber.E164, command.Message, command.AccountRef, ct);

            var savedId = await _smsRepository.SaveSentAsync(
                originator,
                phoneNumber.E164,
                command.Message,
                "SMS",
                sendResult.SubmittedUtc,
                sentBy,
                ct);

            if (savedId.HasValue)
            {
                await _conversationStateUpdater.UpsertOutboundAsync(phoneNumber.Canonical, sendResult.SubmittedUtc, ct);

                await _eventPublisher.PublishAsync(
                    new SmsSentEvent(
                        savedId.Value,
                        phoneNumber.Canonical,
                        originator,
                        phoneNumber.E164,
                        command.Message,
                        sendResult.SubmittedUtc,
                        sentBy),
                    ct);
            }

            return SendSmsResult.Success(sendResult.SubmittedUtc, savedId, savedId.HasValue);
        }
        catch (HttpRequestException ex)
        {
            return SendSmsResult.Fail(SendSmsErrorType.SendFailed, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return SendSmsResult.Fail(SendSmsErrorType.InvalidRequest, ex.Message);
        }
        catch (Exception ex)
        {
            return SendSmsResult.Fail(SendSmsErrorType.Unexpected, ex.Message);
        }
    }
}
