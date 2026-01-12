using Microsoft.Extensions.Logging;

namespace NotifierAPI.Services;

public class MockSendService : ISendService
{
    private readonly ILogger<MockSendService> _logger;
    public MockSendService(ILogger<MockSendService> logger) => _logger = logger;

    public Task<SendResult> SendAsync(string to, string message, string? accountRef, CancellationToken ct = default)
    {
        var id = $"mock-out-{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        _logger.LogInformation("Mock SEND -> To:{To} Msg:{Msg} AccRef:{Acc}", to, message, accountRef);
        return Task.FromResult(new SendResult(id, DateTime.UtcNow));
    }
}
