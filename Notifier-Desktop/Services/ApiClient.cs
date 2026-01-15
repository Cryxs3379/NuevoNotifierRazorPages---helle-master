using System.Net.Http.Json;
using NotifierDesktop.Models;

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
        catch
        {
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
        }
        catch
        {
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
            System.Diagnostics.Debug.WriteLine($"[ApiClient] GetConversationMessagesAsync - Input phone: '{phone}', Normalized: '{phoneNormalized}', URL: '{url}'");
            
            var response = await _httpClient.GetAsync(url, ct);
            
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Response status: {response.StatusCode}");
            
            if (response.IsSuccessStatusCode)
            {
                var messages = await response.Content.ReadFromJsonAsync<List<MessageDto>>(ct);
                System.Diagnostics.Debug.WriteLine($"[ApiClient] Successfully deserialized {messages?.Count ?? 0} messages");
                return messages;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                System.Diagnostics.Debug.WriteLine($"[ApiClient] Error response ({response.StatusCode}): {errorContent}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Exception in GetConversationMessagesAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ApiClient] Stack trace: {ex.StackTrace}");
        }
        return null;
    }

    public async Task<SendMessageResponse?> SendMessageAsync(
        string to,
        string message,
        CancellationToken ct = default)
    {
        try
        {
            var request = new SendMessageRequest { To = to, Message = message };
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
        }
        catch
        {
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
        catch
        {
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
        }
        catch
        {
            // Retornar null en caso de error
        }
        return null;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
