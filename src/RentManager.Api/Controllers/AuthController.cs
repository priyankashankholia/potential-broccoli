using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentManager.Api.Common;
using RentManager.Api.Data;
using RentManager.Api.Security;

namespace RentManager.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RentManagerDbContext _db;
    private readonly JwtTokenService _tokens;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        RentManagerDbContext db,
        JwtTokenService tokens,
        ILogger<AuthController> logger)
    {
        _db = db;
        _tokens = tokens;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return ApiResults.Invalid("Username and password are required.");
        }

        var username = request.Username.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        // Same message either way so the form cannot be used to work out
        // which usernames exist.
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for '{Username}'.", username);

            return Unauthorized(new ApiErrorResponse("Incorrect username or password."));
        }

        user.LastLoginAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var (token, expiresAt) = _tokens.CreateToken(user);

        return Ok(new
        {
            token,
            expiresAt,
            username = user.Username,
            displayName = user.DisplayName
        });
    }

    // Used by Angular on refresh to check a stored token is still valid.
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            username = User.Identity?.Name ?? string.Empty,
            displayName = User.FindFirstValue(JwtOptions.DisplayNameClaim) ?? string.Empty
        });
    }

    // JWTs are stateless, so the real logout is Angular dropping the
    // token. This exists so the event is logged and so a server-side
    // blocklist can be added later without changing the client.
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        _logger.LogInformation("User '{Username}' logged out.", User.Identity?.Name);

        return Ok(new { message = "Logged out." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) ||
            request.NewPassword.Trim().Length < 8)
        {
            return ApiResults.Invalid("New password must be at least 8 characters long.");
        }

        var username = User.Identity?.Name;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            return ApiResults.Missing("User not found.");
        }

        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return ApiResults.Invalid("Current password is incorrect.");
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword.Trim());

        await _db.SaveChangesAsync();

        return Ok(new { message = "Password changed successfully." });
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
