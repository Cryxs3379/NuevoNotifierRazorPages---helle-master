namespace NotifierAPI.Configuration;

public class CallsIngestSettings
{
    public string WatchPath { get; set; } = string.Empty;
    public string ProcessedPath { get; set; } = string.Empty;
    public string ErrorPath { get; set; } = string.Empty;
    public string[] FileExtensions { get; set; } = { ".csv", ".xlsx" };
    public int PollingSeconds { get; set; } = 5;
    public int MaxReadAttempts { get; set; } = 10;
    public int ReadRetryDelayMs { get; set; } = 300;
    public int BulkInsertBatchSize { get; set; } = 2000;
}
