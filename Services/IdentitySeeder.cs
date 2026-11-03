using Microsoft.AspNetCore.Identity;
using AcxiomCRM.Security;

namespace AcxiomCRM.Services;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, IHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var roleName in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Unable to seed Identity role '{roleName}': {string.Join("; ", result.Errors.Select(error => error.Description))}");
                }
            }
        }

        if (!environment.IsDevelopment())
        {
            return;
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Development Admin seeding skipped because SeedAdmin:Email or SeedAdmin:Password is not configured.");
            return;
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Unable to seed the development Admin account: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, RoleNames.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(adminUser, RoleNames.Admin);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Unable to assign the development Admin role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
            }
        }
    }
}
