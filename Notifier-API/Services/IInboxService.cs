using NotifierAPI.Models;

namespace NotifierAPI.Services;

public interface IInboxService
{
    Task<MessagesResponse> GetMessagesAsync(string direction, int page, int pageSize, string? accountRef = null, CancellationToken cancellationToken = default);
    bool IsConfigured();
    Task<bool> DeleteMessageAsync(string id, CancellationToken cancellationToken = default);
    Task<MessageDto?> GetMessageByIdAsync(string id, CancellationToken ct = default);
}


