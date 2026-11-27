using TaskManagement.API.Models.Enums;

namespace TaskManagement.API.DTOs.Tasks;

public class TaskResponseDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TaskPriority Priority { get; set; }

    public TaskItemStatus Status { get; set; }

    public DateTime? DueDate { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? CreatedByName { get; set; }

    public string? AssignedToUserId { get; set; }

    public string? AssignedToName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}