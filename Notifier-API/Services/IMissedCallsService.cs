using NotifierAPI.Models;

namespace NotifierAPI.Services;

public interface IMissedCallsService
{
    Task<MissedCallsResponse?> GetMissedCallsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<MissedCallsStatsResponse?> GetMissedCallsStatsAsync(CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync();
}

