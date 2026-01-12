using Microsoft.AspNetCore.Mvc.RazorPages;
using NotifierAPI.Services;
using NotifierAPI.Configuration;

namespace NotifierAPI.Pages;

public class IndexModel : PageModel
{
    private readonly IInboxService _inboxService;
    private readonly EsendexSettings _esendexSettings;

    public IndexModel(IInboxService inboxService, EsendexSettings esendexSettings)
    {
        _inboxService = inboxService;
        _esendexSettings = esendexSettings;
    }

    public bool IsEsendexConfigured { get; set; }
    public string AccountReference { get; set; } = "N/A";

    public void OnGet()
    {
        IsEsendexConfigured = _inboxService.IsConfigured();
        AccountReference = _esendexSettings.AccountReference ?? "N/A";
    }
}

