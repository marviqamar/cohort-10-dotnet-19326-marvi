using TaskManagement.API.Models;

namespace TaskManagement.API.Interfaces;

public interface IJwtService
{
    string GenerateToken(ApplicationUser user, IList<string> roles);
}