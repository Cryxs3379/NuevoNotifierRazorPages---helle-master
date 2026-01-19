using System.Net.Http.Headers;
using System.Text;
using NotifierAPI.Services;
using NotifierAPI.Configuration;
using NotifierAPI.Hubs;
using NotifierAPI.Extensions;
using NotifierAPI.Data;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Razor Pages
builder.Services.AddRazorPages();

// 2) SignalR
builder.Services.AddSignalR();

// 3) Configuración: leer credenciales desde appsettings.json
var esendexSettings = builder.Configuration.GetSection("Esendex").Get<EsendexSettings>() ?? new EsendexSettings();
var watcherSettings = builder.Configuration.GetSection("Watcher").Get<WatcherSettings>() ?? new WatcherSettings();
var missedCallsBaseUrl = builder.Configuration["MissedCallsAPI:BaseUrl"] ?? "http://localhost:5000";

// Registrar WatcherSettings
builder.Services.AddSingleton(watcherSettings);

// 4) Servicios: usar MOCK por defecto; si hay credenciales se usa implementación real
builder.Services.AddHttpClient();
builder.Services.AddSingleton(esendexSettings);

var hasCredentials = !string.IsNullOrWhiteSpace(esendexSettings.Username) && 
                   !string.IsNullOrWhiteSpace(esendexSettings.ApiPassword) &&
                   !string.IsNullOrWhiteSpace(esendexSettings.AccountReference);

if (hasCredentials)
{
    // Inbox real (Esendex)
    builder.Services.AddHttpClient<EsendexInboxService>(client =>
    {
        client.BaseAddress = new Uri(esendexSettings.BaseUrl ?? "https://api.esendex.com/v1.0/");
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{esendexSettings.Username}:{esendexSettings.ApiPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    });
    builder.Services.AddScoped<IInboxService>(sp =>
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var httpClient = httpClientFactory.CreateClient(nameof(EsendexInboxService));
        var logger = sp.GetRequiredService<ILogger<EsendexInboxService>>();
        return new EsendexInboxService(httpClient, logger, esendexSettings, esendexSettings.Username!, esendexSettings.ApiPassword!);
    });

    // Sender real (Esendex)
    builder.Services.Configure<EsendexSettings>(opt =>
    {
        opt.BaseUrl = esendexSettings.BaseUrl;
        opt.Username = esendexSettings.Username;
        opt.ApiPassword = esendexSettings.ApiPassword;
        opt.AccountReference = esendexSettings.AccountReference;
    });
    builder.Services.AddHttpClient<EsendexSendService>(client =>
    {
        client.BaseAddress = new Uri(esendexSettings.BaseUrl ?? "https://api.esendex.com/v1.0/");
        client.Timeout = TimeSpan.FromSeconds(30);
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{esendexSettings.Username}:{esendexSettings.ApiPassword}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    });
    builder.Services.AddScoped<ISendService>(sp => sp.GetRequiredService<EsendexSendService>());
}
else
{
    builder.Services.AddSingleton<IInboxService, MockInboxService>();
    builder.Services.AddSingleton<ISendService, MockSendService>();
}

// Missed Calls API (SQL propia)
builder.Services.AddHttpClient("MissedCallsAPI", c =>
{
    c.BaseAddress = new Uri(missedCallsBaseUrl);
});
builder.Services.AddScoped<IMissedCallsService, MissedCallsService>();

// Database Context (SQL Server)
var dbConnectionString = builder.Configuration.GetConnectionString("Db") 
    ?? builder.Configuration["ConnectionStrings:Db"] 
    ?? throw new InvalidOperationException("Connection string 'Db' is required");
builder.Services.AddDbContext<NotificationsDbContext>(options =>
    options.UseSqlServer(dbConnectionString));

// SMS Message Repository (con acceso a ConversationStateService opcional)
builder.Services.AddScoped<SmsMessageRepository>(sp => 
    new SmsMessageRepository(
        sp.GetRequiredService<NotificationsDbContext>(),
        sp.GetRequiredService<ILogger<SmsMessageRepository>>(),
        sp));

// Conversation State Service
builder.Services.AddScoped<ConversationStateService>();

// EsendexMessageWatcher (BackgroundService) - solo si está habilitado
if (watcherSettings.Enabled)
{
    builder.Services.AddHostedService<EsendexMessageWatcher>();
}

var app = builder.Build();

// Log del estado del watcher después de construir la app
if (watcherSettings.Enabled)
{
    app.Logger.LogInformation("EsendexMessageWatcher enabled with interval {IntervalSeconds}s", watcherSettings.IntervalSeconds);
}
else
{
    app.Logger.LogInformation("EsendexMessageWatcher disabled");
}

app.UseStaticFiles();
app.UseRouting();

// Mapear endpoints organizados
app.MapRazorPagesEndpoints();
app.MapSignalRHubs();
app.MapApiV1Endpoints();

app.Logger.LogInformation("Razor Pages UI: http://localhost:5080");
app.Logger.LogInformation("Esendex credentials configured: {IsConfigured}", hasCredentials);

app.Run("http://localhost:5080");