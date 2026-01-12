namespace NotifierAPI.Models;

public class MissedCallDto
{
    public long Id { get; set; }
    public DateTime DateAndTime { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public byte Status { get; set; }
    public long? ClientCalledAgain { get; set; }
    public long? AnswerCall { get; set; }
}

public class MissedCallsResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public List<MissedCallDto> Data { get; set; } = new();
}

public class MissedCallsStatsResponse
{
    public int TotalMissedCalls { get; set; }
    public int TodayMissedCalls { get; set; }
    public int ThisWeekMissedCalls { get; set; }
    public object? LastMissedCall { get; set; }
}

