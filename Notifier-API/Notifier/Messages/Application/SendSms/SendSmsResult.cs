namespace Notifier.Messages.Application.SendSms;

public enum SendSmsErrorType
{
    None,
    InvalidRequest,
    InvalidPhone,
    SendFailed,
    SaveFailed,
    Unexpected
}

public sealed record SendSmsResult(
    bool Sent,
    bool Saved,
    long? SavedId,
    DateTime? SubmittedUtc,
    SendSmsErrorType ErrorType,
    string? ErrorMessage)
{
    public static SendSmsResult Success(DateTime submittedUtc, long? savedId, bool saved) =>
        new(true, saved, savedId, submittedUtc, saved ? SendSmsErrorType.None : SendSmsErrorType.SaveFailed, null);

    public static SendSmsResult Fail(SendSmsErrorType errorType, string? message) =>
        new(false, false, null, null, errorType, message);
}
