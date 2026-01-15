namespace NotifierAPI.Models;

public class ClaimRequestDto
{
    public string OperatorName { get; set; } = string.Empty;
    public int Minutes { get; set; } = 5;
}
