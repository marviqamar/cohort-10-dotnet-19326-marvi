
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaskManagement.API.Data;
using TaskManagement.API.DTOs.Tasks;
using TaskManagement.API.Interfaces;
using TaskManagement.API.Models;
using TaskManagement.API.Models.Enums;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly IRealTimeService _realTimeService;
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TaskController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IRealTimeService realTimeService)
    {
        _context = context;
        _userManager = userManager;
        _realTimeService = realTimeService;
    }

    // GET: api/task
    [HttpGet]
    public async Task<IActionResult> GetTasks(
        string? search,
        TaskItemStatus? status,
        TaskPriority? priority,
        int? categoryId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        var query = _context.Tasks
            .Include(t => t.Category)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(t =>
                t.CreatedByUserId == userId ||
                t.AssignedToUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t =>
                t.Title.Contains(search) ||
                (t.Description != null &&
                 t.Description.Contains(search)));
        }

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (priority.HasValue)
            query = query.Where(t => t.Priority == priority.Value);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId.Value);

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TaskResponseDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                Priority = t.Priority,
                Status = t.Status,
                DueDate = t.DueDate,
                CategoryId = t.CategoryId,
                CategoryName = t.Category != null ? t.Category.Name : null,
                CreatedByUserId = t.CreatedByUserId,
                CreatedByName = t.CreatedByUser != null
                    ? t.CreatedByUser.FullName : null,
                AssignedToUserId = t.AssignedToUserId,
                AssignedToName = t.AssignedToUser != null
                    ? t.AssignedToUser.FullName : null,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync();

        return Ok(tasks);
    }

    // GET: api/task/5
    [HttpGet("{id}")]
    public async Task<IActionResult> GetTask(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var task = await _context.Tasks
            .Include(t => t.Category)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null)
            return NotFound(new { message = "Task not found." });

        if (!User.IsInRole("Admin") &&
            task.CreatedByUserId != userId &&
            task.AssignedToUserId != userId)
        {
            return Forbid();
        }

        return Ok(MapTask(task));
    }

    // POST: api/task
    [HttpPost]
    public async Task<IActionResult> CreateTask(CreateTaskDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        if (userId == null)
            return Unauthorized();

        if (!Enum.IsDefined(dto.Priority))
            return BadRequest(new { message = "Invalid priority." });

        if (!isAdmin && !string.IsNullOrEmpty(dto.AssignedToUserId))
            return Forbid();

        if (dto.CategoryId.HasValue &&
            !await _context.Categories.AnyAsync(
                c => c.Id == dto.CategoryId.Value))
        {
            return BadRequest(new { message = "Invalid category." });
        }

        ApplicationUser? assignee = null;

        if (isAdmin && !string.IsNullOrEmpty(dto.AssignedToUserId))
        {
            assignee = await _userManager.FindByIdAsync(
                dto.AssignedToUserId);

            if (assignee == null)
                return BadRequest(new { message = "Assigned user not found." });

            if (!await _userManager.IsInRoleAsync(assignee, "Regular User"))
            {
                return BadRequest(new
                {
                    message = "Tasks can only be assigned to Regular Users."
                });
            }
        }

        var task = new TaskItem
        {
            Title = dto.Title.Trim(),
            Description = dto.Description,
            Priority = dto.Priority,
            DueDate = dto.DueDate,
            CategoryId = dto.CategoryId,
            CreatedByUserId = userId,
            AssignedToUserId = isAdmin ? dto.AssignedToUserId : null
        };

        _context.Tasks.Add(task);

        Notification? createdNotification = null;

        if (assignee != null)
        {
            createdNotification = new Notification
            {
                UserId = assignee.Id,
                Task = task,
                Title = "New Task Assigned",
                Message = $"You have been assigned a new task: {task.Title}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(createdNotification);
        }

        await _context.SaveChangesAsync();

        var taskResponse = await GetTaskResponseAsync(task.Id);

        if (taskResponse == null)
            return StatusCode(500, new { message = "Task was created but could not be retrieved." });

        // Notify the task creator.
        await SendTaskEventAsync(
            "TaskCreated",
            taskResponse,
            task.CreatedByUserId
        );

        // Notify the assigned user.
        if (createdNotification != null)
        {
            await _realTimeService.SendToUserAsync(
                createdNotification.UserId,
                "ReceiveNotification",
                MapNotification(createdNotification, task.Id)
            );

            await SendTaskEventAsync(
                "TaskCreated",
                taskResponse,
                createdNotification.UserId
            );
        }

        // Change 1: Notify Admin when a Regular User creates a task.
        if (!isAdmin)
        {
            await SendAdminTaskEventAsync(
                "TaskCreated",
                taskResponse
            );
        }

        return CreatedAtAction(
            nameof(GetTask),
            new { id = task.Id },
            new { task.Id, task.Title, message = "Task created successfully." }
        );
    }

    // PUT: api/task/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(
        int id,
        UpdateTaskDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
            return NotFound(new { message = "Task not found." });

        if (!isAdmin && task.CreatedByUserId != userId)
            return Forbid();

        if (!Enum.IsDefined(dto.Priority))
            return BadRequest(new { message = "Invalid priority." });

        if (isAdmin && !Enum.IsDefined(dto.Status))
            return BadRequest(new { message = "Invalid status." });

        if (dto.CategoryId.HasValue &&
            !await _context.Categories.AnyAsync(
                c => c.Id == dto.CategoryId.Value))
        {
            return BadRequest(new { message = "Invalid category." });
        }

        var previousAssigneeId = task.AssignedToUserId;

        ApplicationUser? newAssignee = null;

        if (isAdmin && !string.IsNullOrEmpty(dto.AssignedToUserId))
        {
            newAssignee = await _userManager.FindByIdAsync(
                dto.AssignedToUserId);

            if (newAssignee == null)
                return BadRequest(new { message = "Assigned user not found." });

            if (!await _userManager.IsInRoleAsync(
                newAssignee, "Regular User"))
            {
                return BadRequest(new
                {
                    message = "Tasks can only be assigned to Regular Users."
                });
            }
        }

        task.Title = dto.Title.Trim();
        task.Description = dto.Description;
        task.Priority = dto.Priority;
        task.DueDate = dto.DueDate;
        task.CategoryId = dto.CategoryId;

        if (isAdmin)
        {
            task.Status = dto.Status;
            task.AssignedToUserId = dto.AssignedToUserId;
        }

        task.UpdatedAt = DateTime.UtcNow;

        Notification? createdNotification = null;

        if (isAdmin &&
            newAssignee != null &&
            newAssignee.Id != previousAssigneeId)
        {
            createdNotification = new Notification
            {
                UserId = newAssignee.Id,
                TaskId = task.Id,
                Title = "Task Assigned to You",
                Message = $"You have been assigned the task: {task.Title}",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(createdNotification);
        }

        await _context.SaveChangesAsync();

        var taskResponse = await GetTaskResponseAsync(task.Id);

        if (taskResponse == null)
            return StatusCode(500, new { message = "Task was updated but could not be retrieved." });

        // Notify the creator.
        await SendTaskEventAsync(
            "TaskUpdated",
            taskResponse,
            task.CreatedByUserId
        );

        // Notify the previous assignee if they are no longer assigned.
        if (!string.IsNullOrEmpty(previousAssigneeId) &&
            previousAssigneeId != task.AssignedToUserId)
        {
            await SendTaskEventAsync(
                "TaskUpdated",
                taskResponse,
                previousAssigneeId
            );
        }

        // Notify the current assignee.
        if (!string.IsNullOrEmpty(task.AssignedToUserId))
        {
            await SendTaskEventAsync(
                "TaskUpdated",
                taskResponse,
                task.AssignedToUserId
            );
        }

        // Send persistent and real-time notification for reassignment.
        if (createdNotification != null)
        {
            await _realTimeService.SendToUserAsync(
                createdNotification.UserId,
                "ReceiveNotification",
                MapNotification(createdNotification, task.Id)
            );
        }

        // Change 2: Notify Admin when a Regular User updates a task.
        if (!isAdmin)
        {
            await SendAdminTaskEventAsync(
                "TaskUpdated",
                taskResponse
            );
        }

        return Ok(new { message = "Task updated successfully." });
    }

    // DELETE: api/task/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isAdmin = User.IsInRole("Admin");

        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
            return NotFound(new { message = "Task not found." });

        if (!isAdmin && task.CreatedByUserId != userId)
            return Forbid();

        var creatorId = task.CreatedByUserId;
        var assigneeId = task.AssignedToUserId;

        var deletedTask = new
        {
            Id = task.Id,
            Title = task.Title
        };

        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        await SendTaskEventAsync(
            "TaskDeleted",
            deletedTask,
            creatorId
        );

        if (!string.IsNullOrEmpty(assigneeId) &&
            assigneeId != creatorId)
        {
            await SendTaskEventAsync(
                "TaskDeleted",
                deletedTask,
                assigneeId
            );
        }

        // Change 3: Notify Admin when a Regular User deletes a task.
        if (!isAdmin)
        {
            await SendAdminTaskEventAsync(
                "TaskDeleted",
                deletedTask
            );
        }

        return Ok(new { message = "Task deleted successfully." });
    }

    // PATCH: api/task/5/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateTaskStatusDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var task = await _context.Tasks.FindAsync(id);

        if (task == null)
            return NotFound(new { message = "Task not found." });

        var isAdmin = User.IsInRole("Admin");
        var isCreator = task.CreatedByUserId == userId;
        var isAssignee = task.AssignedToUserId == userId;

        if (!isAdmin && !isCreator && !isAssignee)
            return Forbid();

        if (!Enum.IsDefined(dto.Status))
            return BadRequest(new { message = "Invalid status." });

        task.Status = dto.Status;
        task.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var taskResponse = await GetTaskResponseAsync(task.Id);

        if (taskResponse == null)
            return StatusCode(500, new { message = "Task status was updated but could not be retrieved." });

        await SendTaskEventAsync(
            "TaskStatusUpdated",
            taskResponse,
            task.CreatedByUserId
        );

        if (!string.IsNullOrEmpty(task.AssignedToUserId) &&
            task.AssignedToUserId != task.CreatedByUserId)
        {
            await SendTaskEventAsync(
                "TaskStatusUpdated",
                taskResponse,
                task.AssignedToUserId
            );
        }

        // Change 4: Notify Admin when a Regular User changes task status.
        if (!isAdmin)
        {
            await SendAdminTaskEventAsync(
                "TaskStatusUpdated",
                taskResponse
            );
        }

        return Ok(new { message = "Task status updated successfully." });
    }

    // Retrieve task response with related information.
    private async Task<TaskResponseDto?> GetTaskResponseAsync(int taskId)
    {
        var task = await _context.Tasks
            .Include(t => t.Category)
            .Include(t => t.CreatedByUser)
            .Include(t => t.AssignedToUser)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        return task == null ? null : MapTask(task);
    }

    // Send an event to a specific user's private SignalR group.
    private Task SendTaskEventAsync(
        string eventName,
        object data,
        string userId)
    {
        return _realTimeService.SendToUserAsync(
            userId,
            eventName,
            data
        );
    }

    // Change 5: Send task events to the Admin SignalR group.
    private Task SendAdminTaskEventAsync(
        string eventName,
        object data)
    {
        return _realTimeService.SendToGroupAsync(
            "Admins",
            eventName,
            data
        );
    }

    private static object MapNotification(
        Notification notification,
        int taskId)
    {
        return new
        {
            notification.Id,
            notification.Title,
            notification.Message,
            TaskId = taskId,
            notification.IsRead,
            notification.CreatedAt
        };
    }

    private static TaskResponseDto MapTask(TaskItem task)
    {
        return new TaskResponseDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status,
            DueDate = task.DueDate,
            CategoryId = task.CategoryId,
            CategoryName = task.Category?.Name,
            CreatedByUserId = task.CreatedByUserId,
            CreatedByName = task.CreatedByUser?.FullName,
            AssignedToUserId = task.AssignedToUserId,
            AssignedToName = task.AssignedToUser?.FullName,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt
        };
    }
}
