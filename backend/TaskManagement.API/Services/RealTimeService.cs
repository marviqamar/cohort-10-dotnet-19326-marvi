
using Microsoft.AspNetCore.SignalR;
using TaskManagement.API.Hubs;
using TaskManagement.API.Interfaces;

namespace TaskManagement.API.Services;

public class RealTimeService : IRealTimeService
{
    private readonly IHubContext<TaskHub> _hubContext;

    public RealTimeService(IHubContext<TaskHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendToUserAsync(
        string userId,
        string eventName,
        object data)
    {
        await _hubContext.Clients
            .Group($"User_{userId}")
            .SendAsync(eventName, data);
    }

    public async Task SendToGroupAsync(
        string groupName,
        string eventName,
        object data)
    {
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync(eventName, data);
    }

    public async Task SendToAllAsync(
        string eventName,
        object data)
    {
        await _hubContext.Clients
            .All
            .SendAsync(eventName, data);
    }
}
