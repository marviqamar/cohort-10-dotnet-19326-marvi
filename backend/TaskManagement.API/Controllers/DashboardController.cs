
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskManagement.API.Data;
using TaskManagement.API.DTOs.Dashboard;
using TaskManagement.API.Models;
using TaskManagement.API.Models.Enums;

namespace TaskManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET: api/dashboard/summary
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = _userManager.GetUserId(User);

        if (userId == null)
            return Unauthorized();

        var query = _context.Tasks.AsQueryable();

        // Regular users see tasks they created or were assigned.
        // Admins see all tasks.
        if (!User.IsInRole("Admin"))
        {
            query = query.Where(t =>
                t.CreatedByUserId == userId ||
                t.AssignedToUserId == userId
            );
        }

        var now = DateTime.UtcNow;

        var summary = new DashboardSummaryDto
        {
            TotalTasks = await query.CountAsync(),

            PendingTasks = await query.CountAsync(
                t => t.Status == TaskItemStatus.Pending
            ),

            InProgressTasks = await query.CountAsync(
                t => t.Status == TaskItemStatus.InProgress
            ),

            CompletedTasks = await query.CountAsync(
                t => t.Status == TaskItemStatus.Completed
            ),

            OverdueTasks = await query.CountAsync(
                t => t.DueDate.HasValue &&
                     t.DueDate.Value < now &&
                     t.Status != TaskItemStatus.Completed
            )
        };

        return Ok(summary);
    }
}
