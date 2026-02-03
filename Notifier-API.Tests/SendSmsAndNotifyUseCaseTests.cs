using Notifier.Messages.Application;
using Notifier.Messages.Application.Abstractions;
using Notifier.Messages.Application.SendSms;
using Xunit;

namespace NotifierApi.Tests;

public class SendSmsAndNotifyUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_InvalidPhone_ReturnsInvalidPhone()
    {
        var useCase = new SendSmsAndNotifyUseCase(
            new FakeSmsSender(),
            new FakeSmsRepository(),
            new FakeConversationStateUpdater(),
            new FakeEventPublisher());

        var result = await useCase.ExecuteAsync(
            new SendSmsCommand("abc", "hello", "EX000000", null, null),
            CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(SendSmsErrorType.InvalidPhone, result.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_SendsAndPublishesEvent()
    {
        var sender = new FakeSmsSender();
        var repo = new FakeSmsRepository();
        var updater = new FakeConversationStateUpdater();
        var publisher = new FakeEventPublisher();

        var useCase = new SendSmsAndNotifyUseCase(sender, repo, updater, publisher);

        var result = await useCase.ExecuteAsync(
            new SendSmsCommand("+34600123456", "hola", "EX1234567", null, "Alice"),
            CancellationToken.None);

        Assert.True(result.Sent);
        Assert.True(result.Saved);
        Assert.Equal(1, sender.SentCount);
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal("34600123456", updater.LastCustomerPhone);
        Assert.NotNull(publisher.LastEvent);
    }

    private sealed class FakeSmsSender : ISmsSender
    {
        public int SentCount { get; private set; }

        public Task<SendResult> SendAsync(string toE164, string message, string? accountRef, CancellationToken ct)
        {
            SentCount++;
            return Task.FromResult(new SendResult("id-1", new DateTime(2024, 01, 01, 0, 0, 0, DateTimeKind.Utc)));
        }
    }

    private sealed class FakeSmsRepository : ISmsMessageRepository
    {
        public int SaveCount { get; private set; }

        public Task<long?> SaveSentAsync(
            string originator,
            string recipientE164,
            string body,
            string type,
            DateTime messageAtUtc,
            string? sentBy,
            CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult<long?>(123);
        }
    }

    private sealed class FakeConversationStateUpdater : IConversationStateUpdater
    {
        public string? LastCustomerPhone { get; private set; }

        public Task UpsertOutboundAsync(string customerPhoneCanonical, DateTime messageAtUtc, CancellationToken ct)
        {
            LastCustomerPhone = customerPhoneCanonical;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventPublisher : IDomainEventPublisher
    {
        public SmsSentEvent? LastEvent { get; private set; }

        public Task PublishAsync(SmsSentEvent evt, CancellationToken ct)
        {
            LastEvent = evt;
            return Task.CompletedTask;
        }
    }
}
