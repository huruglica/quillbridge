using Microsoft.AspNetCore.Identity;
using quillbridge.Domain.Constants;
using quillbridge.Domain.Models;

namespace quillbridge.Persistence.Seed;

public static class QuillbridgeDbSeeder
{
    private const string DefaultPassword = "Test1234";

    private static readonly User[] Sdrs =
    [
        NewSdr("Maya", "Brennan", new DateTime(2023, 10, 31), "Eastern US", ["English"]),
        NewSdr("Elif", "Demir", new DateTime(2025, 4, 30), "Nordics, Netherlands, Belgium", ["English", "Turkish"]),
        NewSdr("Sam", "Ortega", new DateTime(2025, 8, 31), "Western US and Canada", ["English", "Spanish"]),
        NewSdr("Priya", "Raman", new DateTime(2025, 11, 30), "UK and Ireland", ["English"]),
        NewSdr("Jonas", "Weber", new DateTime(2026, 8, 31), "Germany, Austria, Switzerland", ["German", "English"]),
        NewSdr("Luca", "Ferri", new DateTime(2026, 11, 2), "Southern Europe", ["Italian", "English"])
    ];

    public static async Task SeedAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        await SeedRolesAsync(roleManager);
        await SeedSdrsAsync(userManager);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)));
            }
        }
    }

    private static async Task SeedSdrsAsync(UserManager<User> userManager)
    {
        foreach (var sdr in Sdrs)
        {
            await SeedUserAsync(userManager, sdr, Roles.Sdr);
        }
    }

    private static async Task SeedUserAsync(UserManager<User> userManager, User user, string role)
    {
        if (await userManager.FindByEmailAsync(user.Email!) is not null)
        {
            return;
        }

        EnsureSucceeded(await userManager.CreateAsync(user, DefaultPassword));
        EnsureSucceeded(await userManager.AddToRoleAsync(user, role));
    }

    private static User NewSdr(string firstName, string lastName, DateTime startDate, string region, List<string> languages)
    {
        var email = $"{firstName}.{lastName}@quillbridge.com".ToLowerInvariant();

        return new User
        {
            FirstName = firstName,
            LastName = lastName,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
            Region = region,
            Languages = languages
        };
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));

            throw new InvalidOperationException($"Seeding failed: {errors}");
        }
    }
}
