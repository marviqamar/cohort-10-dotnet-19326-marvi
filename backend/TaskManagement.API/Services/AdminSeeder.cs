
using Microsoft.AspNetCore.Identity;
using TaskManagement.API.Models;

namespace TaskManagement.API.Services;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration)
    {
        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        var fullName = configuration["Admin:FullName"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException(
                "Admin credentials are missing from User Secrets."
            );
        }

        if (!email.EndsWith(
            "@gmail.com",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Admin email must be a Gmail address."
            );
        }

        var admin = await userManager.FindByEmailAsync(email);

        if (admin == null)
        {
            admin = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(
                admin,
                password
            );

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ",
                        result.Errors.Select(e => e.Description))
                );
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await userManager.AddToRoleAsync(
                admin,
                "Admin"
            );

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join(", ",
                        roleResult.Errors.Select(e => e.Description))
                );
            }
        }
    }
}
