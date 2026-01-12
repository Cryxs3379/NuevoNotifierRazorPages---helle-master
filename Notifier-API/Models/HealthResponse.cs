namespace NotifierAPI.Models;

public class HealthResponse
{
    public string Status { get; set; } = "ok";
    public bool EsendexConfigured { get; set; }
}


