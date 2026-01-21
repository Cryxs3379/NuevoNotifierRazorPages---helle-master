using Microsoft.EntityFrameworkCore;
using NotifierAPI.Data;
using NotifierAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

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

// Register IncomingCallWatcher as BackgroundService
builder.Services.AddHostedService<IncomingCallWatcher>();

// Add CORS
builder.Services.AddCors(options =>
{
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

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation("APiCalls starting on http://localhost:5001 ...");
app.Run("http://localhost:5001");
