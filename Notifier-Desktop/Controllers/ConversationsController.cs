using NotifierDesktop.Models;
using NotifierDesktop.Services;
using NotifierDesktop.ViewModels;

namespace NotifierDesktop.Controllers;

public class ConversationsController
{
    private readonly ApiClient _apiClient;
    private readonly SignalRService? _signalRService;
    public List<ConversationVm> Conversations { get; private set; } = new();

    public ConversationsController(ApiClient apiClient, SignalRService? signalRService = null)
    {
        _apiClient = apiClient;
        _signalRService = signalRService;
    }

    public async Task LoadConversationsAsync(string? searchQuery = null)
    {
        var response = await _apiClient.GetConversationsAsync(searchQuery, page: 1, pageSize: 200);
        if (response?.Items != null)
        {
            Conversations = response.Items.Select(dto => new ConversationVm
            {
                Phone = dto.CustomerPhone,
                Preview = dto.Preview,
                LastMessageAt = dto.LastMessageAt,
                LastOutboundAt = dto.LastOutboundAt,
                PendingReply = dto.PendingReply,
                Unread = dto.Unread,
                AssignedTo = dto.AssignedTo,
                AssignedUntil = dto.AssignedUntil
            }).ToList();
        }
        else
        {
            Conversations.Clear();
        }
    }

    public void RefreshList()
    {
        // Ordenar por LastMessageAt desc
        Conversations = Conversations
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ToList();
    }

    public void UpdateBadges(string phone, bool unread, bool pending)
    {
        var conv = Conversations.FirstOrDefault(c => c.Phone == phone);
        if (conv != null)
        {
            conv.Unread = unread;
            conv.PendingReply = pending;
        }
    }

    public ConversationVm? GetSelected(string phone)
    {
        return Conversations.FirstOrDefault(c => c.Phone == phone);
    }
}
