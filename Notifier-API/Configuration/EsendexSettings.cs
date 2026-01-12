namespace NotifierAPI.Configuration;

public class EsendexSettings
{
    public string? BaseUrl { get; set; } = "https://api.esendex.com/v1.0/";
    public string Username { get; set; } = string.Empty;        // usuario de login
    public string ApiPassword { get; set; } = string.Empty;      // API password (no la del portal)
    public string AccountReference { get; set; } = string.Empty; // EXnnnnnn
    
    // Propiedades adicionales necesarias para el funcionamiento completo
    public string? AlternativeBaseUrl { get; set; } = "https://api.esendex.es/v1.0/";
    public string PreferredFormat { get; set; } = "xml"; // "xml" | "json"
    public int TimeoutSeconds { get; set; } = 10;
    public int RetryCount { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 1000;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerSamplingDuration { get; set; } = 30;
    public int CircuitBreakerBreakDuration { get; set; } = 30;
    public int MinPageSize { get; set; } = 1;
    public int MaxPageSize { get; set; } = 200;
}