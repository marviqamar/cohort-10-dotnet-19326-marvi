using Microsoft.AspNetCore.Identity;

namespace TaskManagement.API.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    // Tasks created by this user
    public ICollection<TaskItem> CreatedTasks { get; set; }
        = new List<TaskItem>();

    // Tasks assigned to this user
    public ICollection<TaskItem> AssignedTasks { get; set; }
        = new List<TaskItem>();

    // Notifications received by this user
    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();
}