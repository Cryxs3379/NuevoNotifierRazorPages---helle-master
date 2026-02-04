using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using NotifierAPI.Helpers;

namespace NotifierAPI.Pages.Conversations;

public class ChatModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(IHttpClientFactory httpClientFactory, ILogger<ChatModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Phone { get; set; }

    [BindProperty]
    public string? NewMessage { get; set; }

    [BindProperty]
    public string? SentBy { get; set; }

    public List<ChatMessage> Messages { get; set; } = new();
    public List<QuickReplyOption> QuickReplies { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadMessagesAsync(markRead: true);
        SetQuickReplies();
    }

    public async Task<IActionResult> OnPostSendAsync()
    {
        if (string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "Debe indicar un teléfono para la conversación.";
            SetQuickReplies();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(NewMessage))
        {
            ErrorMessage = "El mensaje no puede estar vacío.";
            await LoadMessagesAsync(markRead: false);
            SetQuickReplies();
            return Page();
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = $"{baseUrl}/api/v1/db/messages/send";

            var payload = new SendMessageRequest
            {
                To = Phone,
                Message = NewMessage,
                SentBy = string.IsNullOrWhiteSpace(SentBy) ? null : SentBy.Trim()
            };

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsJsonAsync(url, payload, HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "No se pudo enviar el mensaje. Inténtalo de nuevo.";
                await LoadMessagesAsync(markRead: false);
                SetQuickReplies();
                return Page();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message for conversation {Phone}", Phone);
            ErrorMessage = "Ocurrió un error al enviar el mensaje.";
            await LoadMessagesAsync(markRead: false);
            SetQuickReplies();
            return Page();
        }

        return RedirectToPage("./Chat", new { phone = Phone });
    }

    private async Task LoadMessagesAsync(bool markRead)
    {
        if (string.IsNullOrWhiteSpace(Phone))
        {
            ErrorMessage = "Debe indicar un teléfono para la conversación.";
            SetQuickReplies();
            return;
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var encodedPhone = Uri.EscapeDataString(Phone);
            var url = $"{baseUrl}/api/v1/db/conversations/{encodedPhone}/messages?take=200";

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, HttpContext.RequestAborted);
            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var items = await response.Content.ReadFromJsonAsync<List<ChatMessage>>(options, HttpContext.RequestAborted);
                Messages = items ?? new List<ChatMessage>();
            }
            else
            {
                ErrorMessage = "No se pudieron cargar los mensajes de la conversación.";
            }

            if (markRead)
            {
                await TryMarkReadAsync(baseUrl, encodedPhone);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversation messages for {Phone}", Phone);
            ErrorMessage = "Ocurrió un error al cargar la conversación.";
            SetQuickReplies();
        }
    }

    private void SetQuickReplies()
    {
        QuickReplies = string.IsNullOrWhiteSpace(Phone)
            ? new List<QuickReplyOption>()
            : QuickReplyProvider.GetForPhone(Phone);
    }

    private async Task TryMarkReadAsync(string baseUrl, string encodedPhone)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{baseUrl}/api/v1/conversations/{encodedPhone}/read";
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content, HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Mark read returned status {Status} for {Phone}", response.StatusCode, Phone);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark conversation read for {Phone}", Phone);
        }
    }

    public class ChatMessage
    {
        public long Id { get; set; }
        public string? Originator { get; set; }
        public string? Recipient { get; set; }
        public string? Body { get; set; }
        public int Direction { get; set; }
        public DateTime? MessageAt { get; set; }
        public string? SentBy { get; set; }
    }

    public class SendMessageRequest
    {
        public string To { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? SentBy { get; set; }
    }
}
