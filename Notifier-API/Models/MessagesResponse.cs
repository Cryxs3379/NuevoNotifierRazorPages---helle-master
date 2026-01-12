namespace NotifierAPI.Models;

public class MessagesResponse
{
    public List<MessageDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}


