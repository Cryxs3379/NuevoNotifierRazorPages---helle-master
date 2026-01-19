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
            // Mapear y normalizar teléfonos
            Conversations = response.Items.Select(dto =>
            {
                // Normalizar CustomerPhone al formato canónico (sin '+')
                string normalizedPhone;
                try
                {
                    normalizedPhone = !string.IsNullOrWhiteSpace(dto.CustomerPhone)
                        ? PhoneNormalizer.Normalize(dto.CustomerPhone)
                        : string.Empty;
                    
#if DEBUG
                    if (string.IsNullOrEmpty(normalizedPhone) && !string.IsNullOrWhiteSpace(dto.CustomerPhone))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ConversationsController] Failed to normalize phone in LoadConversationsAsync: '{dto.CustomerPhone}'");
                    }
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[ConversationsController] Exception normalizing phone '{dto.CustomerPhone}': {ex.Message}");
#endif
                    normalizedPhone = string.Empty;
                }

                return new ConversationVm
                {
                    Phone = normalizedPhone,
                    Preview = dto.Preview ?? string.Empty,
                    LastMessageAt = dto.LastMessageAt,
                    LastOutboundAt = dto.LastOutboundAt,
                    PendingReply = dto.PendingReply,
                    Unread = dto.Unread,
                    AssignedTo = dto.AssignedTo,
                    AssignedUntil = dto.AssignedUntil
                };
            })
            .Where(c => !string.IsNullOrEmpty(c.Phone)) // Filtrar conversaciones sin teléfono válido
            .ToList();

            // Deduplicar por Phone (mantener el más reciente por LastMessageAt)
            Conversations = Conversations
                .GroupBy(c => c.Phone)
                .Select(g => g.OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue).First())
                .ToList();

#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] LoadConversationsAsync: Loaded {Conversations.Count} unique conversations");
#endif
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
        // Normalizar phone antes de buscar
        string normalizedPhone;
        try
        {
            normalizedPhone = PhoneNormalizer.Normalize(phone);
            if (string.IsNullOrEmpty(normalizedPhone))
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ConversationsController] Cannot update badges: phone '{phone}' could not be normalized");
#endif
                return;
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Exception normalizing phone '{phone}' in UpdateBadges: {ex.Message}");
#endif
            return;
        }

        var conv = Conversations.FirstOrDefault(c => c.Phone == normalizedPhone);
        if (conv != null)
        {
            conv.Unread = unread;
            conv.PendingReply = pending;
        }
#if DEBUG
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Conversation not found for UpdateBadges: normalizedPhone='{normalizedPhone}'");
        }
#endif
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
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ConversationsController] Cannot upsert: phone is empty. " +
                    $"CustomerPhone='{message.CustomerPhone}', Originator='{message.Originator}', Recipient='{message.Recipient}'");
#endif
                return;
            }

            normalizedPhone = PhoneNormalizer.Normalize(phoneToNormalize);
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Failed to normalize phone: {ex.Message}");
#endif
            return;
        }

        // Buscar conversación existente (por teléfono normalizado)
        var conv = Conversations.FirstOrDefault(c => c.Phone == normalizedPhone);

        // Si no se encuentra, buscar también variantes con/sin '+' por si hay duplicados
        if (conv == null)
        {
            // Intentar buscar variantes (con/sin '+') para detectar duplicados
            var variantWithPlus = "+" + normalizedPhone;
            var variantWithoutPlus = normalizedPhone;
            
            var duplicateWithPlus = Conversations.FirstOrDefault(c => c.Phone == variantWithPlus);
            var duplicateWithoutPlus = Conversations.FirstOrDefault(c => c.Phone == variantWithoutPlus && c.Phone != normalizedPhone);
            
            if (duplicateWithPlus != null)
            {
                // Encontrar duplicado con '+', unificar: actualizar el duplicado y eliminar
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ConversationsController] Found duplicate with '+': '{variantWithPlus}', unifying to '{normalizedPhone}'");
#endif
                conv = duplicateWithPlus;
                conv.Phone = normalizedPhone; // Normalizar el teléfono
            }
            else if (duplicateWithoutPlus != null)
            {
                // Ya existe uno sin '+', usar ese
                conv = duplicateWithoutPlus;
            }
        }

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
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Created new conversation from SignalR: Phone={normalizedPhone}, IsInbound={isInbound}");
#endif
        }
        else
        {
            // Asegurar que el teléfono está normalizado
            if (conv.Phone != normalizedPhone)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ConversationsController] Normalizing existing conversation phone: '{conv.Phone}' -> '{normalizedPhone}'");
#endif
                conv.Phone = normalizedPhone;
            }

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
            
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Updated conversation from SignalR: Phone={normalizedPhone}, IsInbound={isInbound}");
#endif
        }

        // Deduplicar después de upsert (por si quedaron duplicados)
        DeduplicateConversations();

        // Reordenar lista por LastMessageAt desc
        RefreshList();
    }

    public ConversationVm? GetSelected(string phone)
    {
        // Normalizar phone antes de buscar
        string normalizedPhone;
        try
        {
            normalizedPhone = PhoneNormalizer.Normalize(phone);
            if (string.IsNullOrEmpty(normalizedPhone))
            {
                return null;
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Exception normalizing phone '{phone}' in GetSelected: {ex.Message}");
#endif
            return null;
        }

        return Conversations.FirstOrDefault(c => c.Phone == normalizedPhone);
    }

    /// <summary>
    /// Elimina duplicados por Phone, manteniendo el más reciente por LastMessageAt
    /// </summary>
    private void DeduplicateConversations()
    {
        var beforeCount = Conversations.Count;
        Conversations = Conversations
            .GroupBy(c => c.Phone)
            .Select(g => g.OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue).First())
            .ToList();
        
#if DEBUG
        if (Conversations.Count < beforeCount)
        {
            System.Diagnostics.Debug.WriteLine($"[ConversationsController] Deduplicated conversations: {beforeCount} -> {Conversations.Count}");
        }
#endif
    }
}
