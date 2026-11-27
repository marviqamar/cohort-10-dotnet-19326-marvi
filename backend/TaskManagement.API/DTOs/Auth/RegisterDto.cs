
using System.ComponentModel.DataAnnotations;

namespace TaskManagement.API.DTOs.Auth;

public class RegisterDto
{
    [Required]
    [MaxLength(100)]
    [EmailAddress]
    [RegularExpression(
        @"^[A-Za-z0-9._%+-]+@gmail\.com$",
        ErrorMessage = "Only Gmail addresses (@gmail.com) are allowed."
    )]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(64)]
    public string Password { get; set; } = string.Empty;
}
