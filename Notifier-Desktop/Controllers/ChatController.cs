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
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[ChatController] LoadChatAsync called with phone: '{phone}'");
#endif
        
        CurrentPhone = phone;
        Messages.Clear();
        _messageIds.Clear();

        var messages = await _apiClient.GetConversationMessagesAsync(phone, take: 200);
        
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[ChatController] GetConversationMessagesAsync returned {messages?.Count ?? 0} messages");
        
        if (messages != null && messages.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatController] First message: Id={messages[0].Id}, Originator='{messages[0].Originator}', Recipient='{messages[0].Recipient}', Body='{messages[0].Body?.Substring(0, Math.Min(50, messages[0].Body?.Length ?? 0))}...'");
        }
        else if (messages == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatController] WARNING: GetConversationMessagesAsync returned null");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ChatController] WARNING: GetConversationMessagesAsync returned empty list");
        }
#endif
        
        if (messages != null)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ChatController] Processing {messages.Count} messages from API");
#endif
            
            foreach (var msg in messages)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ChatController] Mapping message: Id={msg.Id}, Direction={msg.Direction}, Originator='{msg.Originator}', Recipient='{msg.Recipient}', Body length={msg.Body?.Length ?? 0}");
                
                if (msg.Id == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatController] WARNING: Message has Id=0, this may indicate a mapping issue");
                }
#endif
                
                if (!_messageIds.Contains(msg.Id))
                {
                    var messageVm = new MessageVm
                    {
                        Id = msg.Id,
                        Direction = (MessageDirection)msg.Direction,
                        At = msg.MessageAt,
                        Text = msg.Body,
                        From = msg.Originator,
                        To = msg.Recipient
                    };
                    
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[ChatController] Created MessageVm: Id={messageVm.Id}, Direction={messageVm.Direction}, Text length={messageVm.Text?.Length ?? 0}");
#endif
                    
                    Messages.Add(messageVm);
                    _messageIds.Add(msg.Id);
                }
#if DEBUG
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ChatController] Skipping duplicate message Id={msg.Id}");
                }
#endif
            }
        }
        
#if DEBUG
        System.Diagnostics.Debug.WriteLine($"[ChatController] LoadChatAsync completed. Total messages in controller: {Messages.Count}");
#endif
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
