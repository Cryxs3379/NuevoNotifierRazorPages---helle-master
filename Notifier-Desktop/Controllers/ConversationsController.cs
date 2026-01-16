using NotifierDesktop.Models;
using NotifierDesktop.Services;
using NotifierDesktop.ViewModels;
using NotifierDesktop.Helpers;

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

    /// <summary>
    /// Crea o actualiza una conversación desde un mensaje SignalR (tiempo real)
    /// </summary>
    public void UpsertFromSignalR(MessageDto message, bool isInbound)
    {
        if (message == null) return;

        // Normalizar phone
        string normalizedPhone;
        try
        {
            var phoneToNormalize = !string.IsNullOrWhiteSpace(message.CustomerPhone)
                ? message.CustomerPhone
                : (isInbound ? message.Originator : message.Recipient);
            
            if (string.IsNullOrWhiteSpace(phoneToNormalize))
            {
                System.Diagnostics.Debug.WriteLine($"[ConversationsController] Cannot upsert: phone is empty. " +
                    $"CustomerPhone='{message.CustomerPhone}', Originator='{message.Originator}', Recipient='{message.Recipient}'");
                return;
            }

            normalizedPhone = PhoneNormalizer.Normalize(phoneToNormalize);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Failed to normalize phone: {ex.Message}");
            return;
        }

        // Buscar conversación existente
        var conv = Conversations.FirstOrDefault(c => c.Phone == normalizedPhone);

        if (conv == null)
        {
            // Crear nueva conversación
            conv = new ConversationVm
            {
                Phone = normalizedPhone,
                Preview = !string.IsNullOrWhiteSpace(message.Body) ? message.Body : string.Empty,
                LastMessageAt = message.MessageAt,
                LastOutboundAt = isInbound ? null : message.MessageAt,
                Unread = isInbound,
                PendingReply = isInbound,
                AssignedTo = null,
                AssignedUntil = null
            };
            Conversations.Add(conv);
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Created new conversation from SignalR: Phone={normalizedPhone}, IsInbound={isInbound}");
        }
        else
        {
            // Actualizar conversación existente
            if (!string.IsNullOrWhiteSpace(message.Body))
            {
                conv.Preview = message.Body;
            }
            conv.LastMessageAt = message.MessageAt;
            
            if (isInbound)
            {
                conv.Unread = true;
                conv.PendingReply = true;
            }
            else
            {
                conv.LastOutboundAt = message.MessageAt;
                // Si hay un outbound más reciente que el último inbound, puede que ya no esté pendiente
                // Pero no podemos saberlo sin consultar el backend, así que solo actualizamos LastOutboundAt
                // El backend actualizará PendingReply cuando se consulte
            }
            
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Updated conversation from SignalR: Phone={normalizedPhone}, IsInbound={isInbound}");
        }

        // Reordenar lista por LastMessageAt desc
        RefreshList();
    }

    public ConversationVm? GetSelected(string phone)
    {
        return Conversations.FirstOrDefault(c => c.Phone == phone);
    }
}
