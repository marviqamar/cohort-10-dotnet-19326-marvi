
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskManagement.API.Hubs;

[Authorize]
public class TaskHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userId))
        {
            // Add the connected user to their private group.
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"User_{userId}"
            );
        }

        // Add the Admin connection to the Admin group.
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                "Admins"
            );
        }

        await base.OnConnectedAsync();
    }
}
