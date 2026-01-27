using Microsoft.EntityFrameworkCore;
using NotifierAPI.Configuration;
using NotifierAPI.Data;
using NotifierAPI.Hubs;
using NotifierAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// SignalR
builder.Services.AddSignalR();

// HttpClientFactory para IncomingCallWatcher
builder.Services.AddHttpClient();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "Notifier API", 
        Version = "v1",
        Description = "API para consultar llamadas perdidas del sistema de notificaciones"
    });
});

// Configure Entity Framework
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// IncomingCallWatcher Configuration
var watcherSettings = builder.Configuration.GetSection("CallsWatcher").Get<IncomingCallWatcherSettings>() ?? new IncomingCallWatcherSettings();
var notifierApiSettings = builder.Configuration.GetSection("NotifierApi").Get<NotifierApiSettings>() ?? new NotifierApiSettings();

var watcherConfig = new IncomingCallWatcherSettings
{
    Enabled = watcherSettings.Enabled,
    PollSeconds = watcherSettings.PollSeconds,
    MaxBatch = watcherSettings.MaxBatch,
    NotifierApiBaseUrl = notifierApiSettings.BaseUrl ?? string.Empty,
    NotifierApiNotifyPath = notifierApiSettings.CallsNotifyPath ?? string.Empty,
    NotifierApiKey = notifierApiSettings.ApiKey ?? string.Empty
};

builder.Services.AddSingleton(watcherConfig);

// CallsIngest Configuration
builder.Services.Configure<CallsIngestSettings>(
    builder.Configuration.GetSection("CallsIngest"));

// Register BackgroundServices
builder.Services.AddHostedService<IncomingCallWatcher>();
builder.Services.AddHostedService<CallsIngestBackgroundService>();

// Add CORS
builder.Services.AddCors(options =>
{
    // Policy para SignalR (requiere AllowCredentials)
    options.AddPolicy("SignalRCors", policy =>
    {
        policy
            .WithOrigins("http://localhost:5080", "http://localhost") // Razor Pages y Desktop
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Requerido para SignalR
    });
    
    // Policy general para API
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger in all environments for testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notifier API v1");
    c.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
});

// REMOVER UseHttpsRedirection() - APiCalls corre solo en HTTP
// app.UseHttpsRedirection(); // ❌ COMENTADO: APiCalls corre solo en HTTP

// Agregar UseWebSockets para SignalR
app.UseWebSockets();

app.UseRouting();

// CORS debe estar después de UseRouting pero antes de UseAuthorization
app.UseCors("SignalRCors");

app.UseAuthorization();

app.MapControllers();
app.MapHub<MessagesHub>("/hubs/messages");

// Validar configuración CallsIngest
var callsIngestSettings = builder.Configuration.GetSection("CallsIngest").Get<CallsIngestSettings>();
if (callsIngestSettings != null)
{
    app.Logger.LogInformation("CallsIngest configurado: WatchPath={WatchPath}", callsIngestSettings.WatchPath);
}
else
{
    app.Logger.LogWarning("CallsIngest no está configurado en appsettings.json");
}

app.Logger.LogInformation("APiCalls starting on http://localhost:5001 ...");
app.Run("http://localhost:5001");
