using System.Net.Http.Headers;
using System.Text;
using NotifierAPI.Services;
using NotifierAPI.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// 1) Razor Pages
builder.Services.AddRazorPages();

// 2) Configuración: leer credenciales desde appsettings.json
var esendexSettings = builder.Configuration.GetSection("Esendex").Get<EsendexSettings>() ?? new EsendexSettings();
var missedCallsBaseUrl = builder.Configuration["MissedCallsAPI:BaseUrl"] ?? "http://localhost:5000";

// 3) Servicios: usar MOCK por defecto; si hay credenciales se usa implementación real
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

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// Razor Pages
app.MapRazorPages();

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

app.Run("http://localhost:5080");