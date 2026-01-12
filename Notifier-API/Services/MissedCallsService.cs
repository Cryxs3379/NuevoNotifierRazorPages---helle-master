using System.Text.Json;
using NotifierAPI.Models;

namespace NotifierAPI.Services;

public class MissedCallsService : IMissedCallsService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MissedCallsService> _logger;

    public MissedCallsService(IHttpClientFactory httpClientFactory, ILogger<MissedCallsService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<MissedCallsResponse?> GetMissedCallsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MissedCallsAPI");
            
            _logger.LogInformation("Fetching missed calls from API: {BaseUrl}", client.BaseAddress);
            
            var response = await client.GetAsync($"/api/MissedCalls?limit={limit}", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch missed calls. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<MissedCallsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Retrieved {Count} missed calls", result?.Count ?? 0);
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when fetching missed calls");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching missed calls");
            return null;
        }
    }

    public async Task<MissedCallsStatsResponse?> GetMissedCallsStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MissedCallsAPI");
            
            _logger.LogInformation("Fetching missed calls stats from API");
            
            var response = await client.GetAsync("/api/MissedCalls/stats", cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch missed calls stats. Status: {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<MissedCallsStatsResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _logger.LogInformation("Retrieved missed calls stats");
            
            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed when fetching missed calls stats");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching missed calls stats");
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("MissedCallsAPI");
            var response = await client.GetAsync("/api/MissedCalls/test");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

