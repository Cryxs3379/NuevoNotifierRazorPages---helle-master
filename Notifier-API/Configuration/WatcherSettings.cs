namespace NotifierAPI.Configuration;

public class WatcherSettings
{
    public bool Enabled { get; set; } = false;
    public int IntervalSeconds { get; set; } = 30;
    public string? AccountRef { get; set; }
}
