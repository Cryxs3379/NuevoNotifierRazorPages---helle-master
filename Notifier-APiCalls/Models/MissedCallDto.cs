namespace NotifierAPI.Models
{
    public class MissedCallDto
    {
        public long Id { get; set; }
        public DateTime DateAndTime { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public byte Status { get; set; }
        public long? ClientCalledAgain { get; set; }
        public long? AnswerCall { get; set; }
        public bool IsMissedCall { get; set; }
        public TimeSpan TimeAgo { get; set; }
        public string FormattedTimeAgo => FormatTimeAgo(TimeAgo);

        private static string FormatTimeAgo(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{(int)timeSpan.TotalDays} día(s) atrás";
            else if (timeSpan.TotalHours >= 1)
                return $"{(int)timeSpan.TotalHours} hora(s) atrás";
            else if (timeSpan.TotalMinutes >= 1)
                return $"{(int)timeSpan.TotalMinutes} minuto(s) atrás";
            else
                return "Hace un momento";
        }
    }
}
