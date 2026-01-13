using System.Net.Http.Headers;
using System.Text;
using NotifierAPI.Services;
using NotifierAPI.Configuration;
using NotifierAPI.Hubs;
using Microsoft.Extensions.Options;

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

// Razor Pages
app.MapRazorPages();

// SignalR Hub
app.MapHub<MessagesHub>("/hubs/messages");

app.Logger.LogInformation("Razor Pages UI: http://localhost:5080");
app.Logger.LogInformation("Esendex credentials configured: {IsConfigured}", hasCredentials);

// Endpoint para obtener mensaje completo por ID
app.MapGet("/api/v1/messages/{id}", async (string id, IInboxService inbox, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(id)) 
        return Results.BadRequest(new { error = "Id requerido" });
    
    try
    {
        var message = await inbox.GetMessageByIdAsync(id, ct);
        if (message == null)
            return Results.NotFound(new { error = "Mensaje no encontrado" });
        
        return Results.Ok(message);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Problem(statusCode: 401, title: "Error de autenticación con Esendex");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error obteniendo mensaje {Id}", id);
        return Results.Problem(statusCode: 500, title: "Error al obtener el mensaje");
    }
})
.WithName("GetMessageById");

// Endpoint para eliminar mensajes de Esendex desde la UI
app.MapDelete("/api/v1/messages/{id}", async (string id, IInboxService inbox, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(id)) return Results.BadRequest(new { error = "Id requerido" });
    var ok = await inbox.DeleteMessageAsync(id, ct);
    return ok ? Results.NoContent() : Results.Problem(statusCode: 502, title: "No se pudo eliminar en Esendex");
})
.WithName("DeleteMessage");

// Endpoint para obtener lista de mensajes (JSON) - para refresh parcial
app.MapGet("/api/v1/messages", async (
    string? direction, 
    int? page, 
    int? pageSize, 
    string? accountRef,
    IInboxService inbox, 
    CancellationToken ct) =>
{
    try
    {
        var dir = direction ?? "inbound";
        if (dir != "inbound" && dir != "outbound") dir = "inbound";
        
        var p = page ?? 1;
        if (p < 1) p = 1;
        
        var ps = pageSize ?? 25;
        if (ps < 10) ps = 10;
        if (ps > 200) ps = 200;

        var response = await inbox.GetMessagesAsync(dir, p, ps, accountRef, ct);
        return Results.Ok(response);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Problem(statusCode: 401, title: "Error de autenticación con Esendex");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error obteniendo lista de mensajes");
        return Results.Problem(statusCode: 500, title: "Error al obtener los mensajes");
    }
})
.WithName("GetMessages");

// Endpoint para obtener llamadas perdidas (JSON) - para refresh parcial
app.MapGet("/api/v1/calls/missed", async (int? limit, IMissedCallsService callsService, CancellationToken ct) =>
{
    try
    {
        var lmt = limit ?? 100;
        if (lmt < 10) lmt = 10;
        if (lmt > 500) lmt = 500;

        var response = await callsService.GetMissedCallsAsync(lmt, ct);
        if (response == null)
        {
            return Results.Problem(statusCode: 502, title: "No se pudieron obtener las llamadas perdidas");
        }
        
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error obteniendo llamadas perdidas");
        return Results.Problem(statusCode: 500, title: "Error al obtener las llamadas perdidas");
    }
})
.WithName("GetMissedCalls");

// Endpoint para obtener estadísticas de llamadas perdidas (JSON) - para refresh parcial
app.MapGet("/api/v1/calls/stats", async (IMissedCallsService callsService, CancellationToken ct) =>
{
    try
    {
        var stats = await callsService.GetMissedCallsStatsAsync(ct);
        if (stats == null)
        {
            return Results.Problem(statusCode: 502, title: "No se pudieron obtener las estadísticas");
        }
        
        return Results.Ok(stats);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error obteniendo estadísticas de llamadas perdidas");
        return Results.Problem(statusCode: 500, title: "Error al obtener las estadísticas");
    }
})
.WithName("GetMissedCallsStats");

app.Run("http://localhost:5080");