namespace TaskManagement.API.Models;

public class Notification
{
    public int Id { get; set; }

    // User who receives the notification
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // Related task (optional)
    public int? TaskId { get; set; }
    public TaskItem? Task { get; set; }

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}