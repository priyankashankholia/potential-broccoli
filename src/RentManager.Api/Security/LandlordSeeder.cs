using Microsoft.EntityFrameworkCore;
using RentManager.Api.Data;
using RentManager.Api.Models;

namespace RentManager.Api.Security;

// Creates the landlord account on first run against an empty Users table.
// Credentials come from the Landlord__Username / Landlord__Password
// secrets. If they are missing, startup fails rather than falling back to
// a known default password.
public static class LandlordSeeder
{
    public static async Task SeedAsync(
        RentManagerDbContext db,
        IConfiguration configuration,
        ILogger logger)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var username = configuration["Landlord:Username"];
        var password = configuration["Landlord:Password"];
        var displayName = configuration["Landlord:DisplayName"];

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "No landlord account exists and Landlord__Username / " +
                "Landlord__Password are not configured. Set both as " +
                "Codespaces secrets before starting the API. See README-SETUP.md.");
        }

        if (password.Trim().Length < 8)
        {
            throw new InvalidOperationException(
                "Landlord__Password must be at least 8 characters long.");
        }

        var user = new AppUser
        {
            Username = username.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(password.Trim()),
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "Landlord"
                : displayName.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);

        await db.SaveChangesAsync();

        logger.LogInformation("Created landlord account '{Username}'.", user.Username);
    }
}
