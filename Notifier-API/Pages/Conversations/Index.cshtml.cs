using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NotifierAPI.Pages.Conversations;

public class ConversationsIndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ConversationsIndexModel> _logger;

    public ConversationsIndexModel(IHttpClientFactory httpClientFactory, ILogger<ConversationsIndexModel> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    public ConversationsResponse? Conversations { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var url = $"{baseUrl}/api/v1/conversations?page=1&pageSize=200";
            if (!string.IsNullOrWhiteSpace(Q))
            {
                url += $"&q={Uri.EscapeDataString(Q)}";
            }

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, HttpContext.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "No se pudieron cargar las conversaciones en este momento.";
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            Conversations = await response.Content.ReadFromJsonAsync<ConversationsResponse>(options, HttpContext.RequestAborted);
            if (Conversations == null)
            {
                ErrorMessage = "La respuesta de conversaciones no es válida.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading conversations list");
            ErrorMessage = "Ocurrió un error al cargar las conversaciones.";
        }
    }

    public class ConversationsResponse
    {
        public List<ConversationItem> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
    }

    public class ConversationItem
    {
        public string? CustomerPhone { get; set; }
        public string? Preview { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public bool PendingReply { get; set; }
        public bool Unread { get; set; }
        public string? AssignedTo { get; set; }
        public string? LastRespondedBy { get; set; }
        public DateTime? LastInboundAt { get; set; }
        public DateTime? LastOutboundAt { get; set; }
        public DateTime? LastReadInboundAt { get; set; }
        public DateTime? AssignedUntil { get; set; }
    }
}
