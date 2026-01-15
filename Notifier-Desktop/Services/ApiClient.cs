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
            var response = await _httpClient.GetAsync(
                $"/api/v1/db/conversations/{Uri.EscapeDataString(phone)}/messages?take={take}",
                ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MessageDto>>(ct);
            }
        }
        catch
        {
            // Retornar null en caso de error
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

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
