using System.Net.Http.Json;
using System.Text.Json;
using NotifierDesktop.Models;
using NotifierDesktop.ViewModels;

namespace NotifierDesktop.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<HealthResponse?> GetHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/health", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<HealthResponse>(ct);
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Error in GetHealthAsync: {ex.Message}");
#endif
            // Retornar null en caso de error
        }
        return null;
    }

    public async Task<MessagesResponse?> GetMessagesAsync(
        int direction,
        int page = 1,
        int pageSize = 50,
        string? phone = null,
        CancellationToken ct = default)
    {
        try
        {
            var queryParams = $"direction={direction}&page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(phone))
            {
                queryParams += $"&phone={Uri.EscapeDataString(phone)}";
            }

            var response = await _httpClient.GetAsync($"/api/v1/db/messages?{queryParams}", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<MessagesResponse>(ct);
            }
#if DEBUG
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ApiClient] GetMessagesAsync failed with status: {response.StatusCode}");
            }
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Error in GetMessagesAsync: {ex.Message}");
#endif
            // Retornar null en caso de error
        }
        return null;
    }

    public async Task<List<MessageDto>?> GetConversationMessagesAsync(
        string phone,
        int take = 200,
        CancellationToken ct = default)
    {
        try
        {
            // El endpoint ya es tolerante, pero normalizar para consistencia
            var phoneNormalized = NotifierDesktop.Helpers.PhoneNormalizer.Normalize(phone);
            if (string.IsNullOrEmpty(phoneNormalized))
            {
                phoneNormalized = phone; // Usar original si no se puede normalizar
            }
            
            var url = $"/api/v1/db/conversations/{Uri.EscapeDataString(phoneNormalized)}/messages?take={take}";
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] GetConversationMessagesAsync - Input phone: '{phone}', Normalized: '{phoneNormalized}', URL: '{url}'");
#endif
            
            var response = await _httpClient.GetAsync(url, ct);
            
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Response status: {response.StatusCode}");
#endif
            
            if (response.IsSuccessStatusCode)
            {
                var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>(ct);
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ApiClient] Successfully deserialized {messages?.Count ?? 0} messages");
#endif
                return messages;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[ApiClient] Error response ({response.StatusCode}): {errorContent}");
#endif
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Exception in GetConversationMessagesAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Stack trace: {ex.StackTrace}");
#endif
        }
        return null;
    }

    public async Task<SendMessageResponse?> SendMessageAsync(
        string to,
        string message,
        string? sentBy = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new SendMessageRequest { To = to, Message = message, SentBy = sentBy };
            var response = await _httpClient.PostAsJsonAsync("/api/v1/db/messages/send", request, ct);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SendMessageResponse>(ct);
            }
            else
            {
                var errorText = await response.Content.ReadAsStringAsync(ct);
                return new SendMessageResponse
                {
                    Success = false,
                    Error = $"HTTP {(int)response.StatusCode}: {errorText}"
                };
            }
        }
        catch (Exception ex)
        {
            return new SendMessageResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<ConversationsResponse?> GetConversationsAsync(
        string? q,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var queryParams = $"page={page}&pageSize={pageSize}";
            if (!string.IsNullOrWhiteSpace(q))
            {
                queryParams += $"&q={Uri.EscapeDataString(q)}";
            }

            var response = await _httpClient.GetAsync($"/api/v1/conversations?{queryParams}", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ConversationsResponse>(ct);
            }
#if DEBUG
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ApiClient] GetConversationsAsync failed with status: {response.StatusCode}");
            }
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Error in GetConversationsAsync: {ex.Message}");
#endif
            // Retornar null en caso de error
        }
        return null;
    }

    public async Task<bool> MarkConversationReadAsync(string phone, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsync(
                $"/api/v1/conversations/{Uri.EscapeDataString(phone)}/read",
                null,
                ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Error in MarkConversationReadAsync: {ex.Message}");
#endif
            return false;
        }
    }

    public async Task<ClaimResponse?> ClaimConversationAsync(
        string phone,
        string operatorName,
        int minutes,
        CancellationToken ct = default)
    {
        try
        {
            var request = new { operatorName, minutes };
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/conversations/{Uri.EscapeDataString(phone)}/claim",
                request,
                ct);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ClaimResponse>(ct);
            }
#if DEBUG
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ApiClient] ClaimConversationAsync failed with status: {response.StatusCode}");
            }
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Error in ClaimConversationAsync: {ex.Message}");
#endif
            // Retornar null en caso de error
        }
        return null;
    }

    public async Task<List<MissedCallVm>?> GetMissedCallsFromViewAsync(int limit = 200, CancellationToken ct = default)
    {
        try
        {
            var lmt = limit > 0 ? limit : 200;
            if (lmt > 500) lmt = 500;

            var response = await _httpClient.GetAsync($"/api/v1/calls/missed/view?limit={lmt}", ct);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                
                // El endpoint devuelve { items: [...] }
                var wrapper = JsonSerializer.Deserialize<JsonElement>(json, options);
                if (wrapper.TryGetProperty("items", out var itemsElement))
                {
                    var calls = JsonSerializer.Deserialize<List<MissedCallVm>>(itemsElement.GetRawText(), options);
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[ApiClient] GetMissedCallsFromViewAsync: Retrieved {calls?.Count ?? 0} missed calls");
#endif
                    return calls;
                }
            }
#if DEBUG
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ApiClient] GetMissedCallsFromViewAsync failed with status: {response.StatusCode}");
            }
#endif
        }
        catch (Exception ex)
        {
#if DEBUG
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Error in GetMissedCallsFromViewAsync: {ex.Message}");
#endif
        }
        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
