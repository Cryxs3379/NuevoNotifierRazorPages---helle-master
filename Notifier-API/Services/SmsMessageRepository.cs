using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;

namespace NotifierAPI.Services;

public class SmsMessageRepository
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ILogger<SmsMessageRepository> _logger;

    public SmsMessageRepository(
        NotificationsDbContext dbContext,
        ILogger<SmsMessageRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Guarda un mensaje recibido en la base de datos
    /// </summary>
    public async Task<bool> SaveReceivedAsync(
        string originator,
        string recipient,
        string body,
        string type,
        DateTime? messageAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new NotifierSmsMessage
            {
                Originator = originator ?? string.Empty,
                Recipient = recipient ?? string.Empty,
                Body = body ?? string.Empty,
                Type = type ?? string.Empty,
                Direction = 0, // Received
                MessageAt = messageAt ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SmsMessages.Add(message);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Saved received SMS message to database. Originator: {Originator}, Recipient: {Recipient}", 
                originator, recipient);
            
            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, 
                "SQL error saving received SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, 
                "Database update error saving received SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, 
                "Invalid operation error saving received SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error saving received SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Guarda un mensaje enviado en la base de datos
    /// </summary>
    public async Task<bool> SaveSentAsync(
        string originator,
        string recipient,
        string body,
        string type,
        DateTime? messageAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new NotifierSmsMessage
            {
                Originator = originator ?? string.Empty,
                Recipient = recipient ?? string.Empty,
                Body = body ?? string.Empty,
                Type = type ?? string.Empty,
                Direction = 1, // Sent
                MessageAt = messageAt ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SmsMessages.Add(message);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Saved sent SMS message to database. Originator: {Originator}, Recipient: {Recipient}", 
                originator, recipient);
            
            return true;
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, 
                "SQL error saving sent SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, 
                "Database update error saving sent SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, 
                "Invalid operation error saving sent SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error saving sent SMS message. Originator: {Originator}, Recipient: {Recipient}, Error: {Error}", 
                originator, recipient, ex.Message);
            return false;
        }
    }
}
