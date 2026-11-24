using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace SkyMate.Infrastructure.Identity;

/// <summary>Ensures the required Identity roles exist when the application starts.</summary>
public static class IdentityRoleSeeder
{
    private static readonly string[] RequiredRoles = ["User", "Admin"];

    /// <summary>Creates any required roles that are missing.</summary>
    /// <param name="services">The application's service provider.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A required role could not be created.</exception>
    public static async Task SeedAsync(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();

        foreach (var roleName in RequiredRoles)
        {
            // Checking first makes seeding safe to run more than once.
            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<int> { Name = roleName });
            if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
            {
                var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
                throw new InvalidOperationException($"Could not seed the '{roleName}' Identity role. {errors}");
            }
        }
    }
}
