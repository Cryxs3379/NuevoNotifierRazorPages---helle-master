using Microsoft.AspNetCore.SignalR;

namespace NotifierAPI.Hubs;

public class MessagesHub : Hub
{
    // No necesitamos métodos personalizados, usaremos IHubContext para emitir desde el BackgroundService
}
