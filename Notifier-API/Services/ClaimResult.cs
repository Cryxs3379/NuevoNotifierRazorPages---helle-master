namespace NotifierAPI.Services;

public record ClaimResult(bool Success, bool WasAlreadyAssigned, string? AssignedTo, DateTime? AssignedUntil);
