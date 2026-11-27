
namespace TaskManagement.API.Interfaces;

public interface IRealTimeService
{
    Task SendToUserAsync(
        string userId,
        string eventName,
        object data
    );

    Task SendToGroupAsync(
        string groupName,
        string eventName,
        object data
    );

    Task SendToAllAsync(
        string eventName,
        object data
    );
}
