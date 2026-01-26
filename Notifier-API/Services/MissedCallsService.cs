using System.Text.Json;
using NotifierAPI.Models;
using NotifierAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace NotifierAPI.Services;

public class MissedCallsService : IMissedCallsService
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ILogger<MissedCallsService> _logger;

    public MissedCallsService(NotificationsDbContext dbContext, ILogger<MissedCallsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MissedCallsResponse?> GetMissedCallsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var calls = await _dbContext.NotifierCallsStaging
                .OrderByDescending(c => c.DateAndTime)
                .Take(limit)
                .Select(c => new MissedCallDto
                {
                    Id = c.Id,
                    DateAndTime = c.DateAndTime,
                    PhoneNumber = c.PhoneNumber,
                    StatusText = c.StatusText ?? "N/A"
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {Count} calls from NotifierCalls_Staging", calls.Count);

            return new MissedCallsResponse
            {
                Success = true,
                Count = calls.Count,
                Data = calls
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching calls from NotifierCalls_Staging");
            return null;
        }
    }

    public async Task<MissedCallsStatsResponse?> GetMissedCallsStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var weekStart = today.AddDays(-(int)today.DayOfWeek);

            var total = await _dbContext.NotifierCallsStaging.CountAsync(cancellationToken);
            var todayCount = await _dbContext.NotifierCallsStaging
                .CountAsync(c => c.DateAndTime >= today, cancellationToken);
            var weekCount = await _dbContext.NotifierCallsStaging
                .CountAsync(c => c.DateAndTime >= weekStart, cancellationToken);
            
            var lastCall = await _dbContext.NotifierCallsStaging
                .OrderByDescending(c => c.DateAndTime)
                .FirstOrDefaultAsync(cancellationToken);

            return new MissedCallsStatsResponse
            {
                TotalMissedCalls = total,
                TodayMissedCalls = todayCount,
                ThisWeekMissedCalls = weekCount,
                LastMissedCall = lastCall != null ? new
                {
                    id = lastCall.Id,
                    dateAndTime = lastCall.DateAndTime,
                    phoneNumber = lastCall.PhoneNumber
                } : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching calls stats");
            return null;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            return await _dbContext.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }
}

