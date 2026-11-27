using System.ComponentModel.DataAnnotations;

namespace TaskManagement.API.DTOs.Users;

public class UpdateProfileDto
{
    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }
}