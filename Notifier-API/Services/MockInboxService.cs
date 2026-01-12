using NotifierAPI.Models;

namespace NotifierAPI.Services;

public class MockInboxService : IInboxService
{
    private readonly ILogger<MockInboxService> _logger;
    private static readonly List<MessageDto> _mockMessages = new()
    {
        new MessageDto
        {
            Id = "mock-001",
            From = "+34123456789",
            To = "+34987654321",
            Message = "Este es un mensaje mock de prueba 1",
            ReceivedUtc = DateTime.UtcNow.AddHours(-2)
        },
        new MessageDto
        {
            Id = "mock-002",
            From = "+34111222333",
            To = "+34987654321",
            Message = "Este es un mensaje mock de prueba 2",
            ReceivedUtc = DateTime.UtcNow.AddHours(-1)
        },
        new MessageDto
        {
            Id = "mock-003",
            From = "+34444555666",
            To = "+34987654321",
            Message = "Este es un mensaje mock de prueba 3",
            ReceivedUtc = DateTime.UtcNow.AddMinutes(-30)
        }
    };

    public MockInboxService(ILogger<MockInboxService> logger)
    {
        _logger = logger;
    }

    public Task<MessagesResponse> GetMessagesAsync(string direction, int page, int pageSize, string? accountRef = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MockInboxService: Returning mock data (credentials not configured)");

        var skip = (page - 1) * pageSize;
        var items = _mockMessages.Skip(skip).Take(pageSize).ToList();

        var response = new MessagesResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = _mockMessages.Count
        };

        return Task.FromResult(response);
    }

    public bool IsConfigured() => false;

    public Task<bool> DeleteMessageAsync(string id, CancellationToken cancellationToken = default)
    {
        var removed = _mockMessages.RemoveAll(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)) > 0;
        return Task.FromResult(removed);
    }

    public Task<MessageDto?> GetMessageByIdAsync(string id, CancellationToken ct = default)
    {
        _logger.LogInformation("MockInboxService: Getting message by ID {Id}", id);
        
        var message = _mockMessages.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        
        if (message != null)
        {
            // Devolver una copia con el mensaje completo (en mock ya está completo)
            return Task.FromResult<MessageDto?>(new MessageDto
            {
                Id = message.Id,
                From = message.From,
                To = message.To,
                Message = message.Message, // Ya está completo en mock
                ReceivedUtc = message.ReceivedUtc
            });
        }

        return Task.FromResult<MessageDto?>(null);
    }
}


