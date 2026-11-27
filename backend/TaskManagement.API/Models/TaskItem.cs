using TaskManagement.API.Models.Enums;

namespace TaskManagement.API.Models;

public class TaskItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskPriority Priority { get; set; } = TaskPriority.Medium;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;

    public DateTime? DueDate { get; set; }

    // Category relationship
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    // User who created the task
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    // User assigned to the task
    public string? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Notifications related to this task
    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();
}