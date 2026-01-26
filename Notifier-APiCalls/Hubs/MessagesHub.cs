using Microsoft.AspNetCore.SignalR;

namespace NotifierAPI.Hubs;

public class MessagesHub : Hub
{
    // Hub vacío, usaremos IHubContext para emitir desde BackgroundService
}
