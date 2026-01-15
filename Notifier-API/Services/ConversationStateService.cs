using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;

namespace NotifierAPI.Services;

public class ConversationStateService
{
    private readonly NotificationsDbContext _dbContext;
    private readonly ILogger<ConversationStateService> _logger;

    public ConversationStateService(
        NotificationsDbContext dbContext,
        ILogger<ConversationStateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Actualiza o crea el estado de conversación cuando llega un mensaje inbound
    /// </summary>
    public async Task UpsertInboundAsync(string customerPhone, DateTime messageAtUtc, CancellationToken ct)
    {
        try
        {
            var existing = await _dbContext.ConversationStates
                .FirstOrDefaultAsync(cs => cs.CustomerPhone == customerPhone, ct);

            if (existing == null)
            {
                // Crear nueva fila
                var newState = new ConversationState
                {
                    CustomerPhone = customerPhone,
                    LastInboundAt = messageAtUtc,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.ConversationStates.Add(newState);
            }
            else
            {
                // Actualizar LastInboundAt y UpdatedAt (NO tocar LastReadInboundAt)
                existing.LastInboundAt = messageAtUtc;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Updated ConversationState for inbound: CustomerPhone={Phone}, LastInboundAt={At}", 
                customerPhone, messageAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ConversationState for inbound: CustomerPhone={Phone}", customerPhone);
            throw; // Re-lanzar para que el caller maneje
        }
    }

    /// <summary>
    /// Actualiza o crea el estado de conversación cuando se envía un mensaje outbound
    /// </summary>
    public async Task UpsertOutboundAsync(string customerPhone, DateTime messageAtUtc, CancellationToken ct)
    {
        try
        {
            var existing = await _dbContext.ConversationStates
                .FirstOrDefaultAsync(cs => cs.CustomerPhone == customerPhone, ct);

            if (existing == null)
            {
                // Crear nueva fila
                var newState = new ConversationState
                {
                    CustomerPhone = customerPhone,
                    LastOutboundAt = messageAtUtc,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.ConversationStates.Add(newState);
            }
            else
            {
                // Actualizar LastOutboundAt y UpdatedAt
                existing.LastOutboundAt = messageAtUtc;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Updated ConversationState for outbound: CustomerPhone={Phone}, LastOutboundAt={At}", 
                customerPhone, messageAtUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating ConversationState for outbound: CustomerPhone={Phone}", customerPhone);
            throw; // Re-lanzar para que el caller maneje
        }
    }

    /// <summary>
    /// Marca la conversación como leída
    /// </summary>
    public async Task MarkReadAsync(string customerPhone, CancellationToken ct)
    {
        try
        {
            var state = await _dbContext.ConversationStates
                .FirstOrDefaultAsync(cs => cs.CustomerPhone == customerPhone, ct);

            if (state == null)
            {
                _logger.LogWarning("ConversationState not found for MarkRead: CustomerPhone={Phone}", customerPhone);
                return;
            }

            if (state.LastInboundAt.HasValue)
            {
                state.LastReadInboundAt = state.LastInboundAt;
                state.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogDebug("Marked conversation as read: CustomerPhone={Phone}", customerPhone);
            }
            else
            {
                _logger.LogDebug("Cannot mark as read: LastInboundAt is null for CustomerPhone={Phone}", customerPhone);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking conversation as read: CustomerPhone={Phone}", customerPhone);
            throw;
        }
    }

    /// <summary>
    /// Asigna una conversación a un operador (soft lock)
    /// </summary>
    public async Task<ClaimResult> ClaimAsync(string customerPhone, string operatorName, int minutes, CancellationToken ct)
    {
        try
        {
            var state = await _dbContext.ConversationStates
                .FirstOrDefaultAsync(cs => cs.CustomerPhone == customerPhone, ct);

            if (state == null)
            {
                // Crear nueva fila si no existe
                state = new ConversationState
                {
                    CustomerPhone = customerPhone,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.ConversationStates.Add(state);
            }

            var now = DateTime.UtcNow;
            var wasAlreadyAssigned = state.AssignedUntil.HasValue && state.AssignedUntil > now;

            if (!wasAlreadyAssigned)
            {
                // Asignar
                state.AssignedTo = operatorName;
                state.AssignedUntil = now.AddMinutes(minutes);
                state.UpdatedAt = now;
                await _dbContext.SaveChangesAsync(ct);
                
                _logger.LogDebug("Claimed conversation: CustomerPhone={Phone}, Operator={Operator}, Until={Until}", 
                    customerPhone, operatorName, state.AssignedUntil);
                
                return new ClaimResult(
                    Success: true,
                    WasAlreadyAssigned: false,
                    AssignedTo: operatorName,
                    AssignedUntil: state.AssignedUntil);
            }
            else
            {
                // Ya está asignado y válido, retornar estado actual sin cambiar
                _logger.LogDebug("Conversation already claimed: CustomerPhone={Phone}, AssignedTo={Operator}, Until={Until}", 
                    customerPhone, state.AssignedTo, state.AssignedUntil);
                
                return new ClaimResult(
                    Success: true,
                    WasAlreadyAssigned: true,
                    AssignedTo: state.AssignedTo,
                    AssignedUntil: state.AssignedUntil);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error claiming conversation: CustomerPhone={Phone}, Operator={Operator}", 
                customerPhone, operatorName);
            throw;
        }
    }
}
