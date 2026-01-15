using NotifierDesktop.Models;
using NotifierDesktop.Services;
using NotifierDesktop.ViewModels;

namespace NotifierDesktop.Controllers;

public class ChatController
{
    private readonly ApiClient _apiClient;
    public string? CurrentPhone { get; private set; }
    public List<MessageVm> Messages { get; private set; } = new();
    private readonly HashSet<long> _messageIds = new(); // Para deduplicación

    public ChatController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task LoadChatAsync(string phone)
    {
        CurrentPhone = phone;
        Messages.Clear();
        _messageIds.Clear();

        var messages = await _apiClient.GetConversationMessagesAsync(phone, take: 200);
        if (messages != null)
        {
            foreach (var msg in messages)
            {
                if (!_messageIds.Contains(msg.Id))
                {
                    Messages.Add(new MessageVm
                    {
                        Id = msg.Id,
                        Direction = (MessageDirection)msg.Direction,
                        At = msg.MessageAt,
                        Text = msg.Body,
                        From = msg.Originator,
                        To = msg.Recipient
                    });
                    _messageIds.Add(msg.Id);
                }
            }
        }
    }

    public void AddMessage(MessageVm message)
    {
        // Deduplicación por Id
        if (_messageIds.Contains(message.Id))
            return;

        Messages.Add(message);
        _messageIds.Add(message.Id);
    }

    public void AutoScroll()
    {
        // Se manejará en el UI (MainForm)
    }

    public void Clear()
    {
        Messages.Clear();
        _messageIds.Clear();
        CurrentPhone = null;
    }
}
