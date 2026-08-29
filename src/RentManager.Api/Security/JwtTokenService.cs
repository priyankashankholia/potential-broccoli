using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RentManager.Api.Models;

namespace RentManager.Api.Security;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    // Comes from the Jwt__Key secret.
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "RentManager";

    public string Audience { get; set; } = "RentManager";

    public int ExpiryHours { get; set; } = 12;

    // Set explicitly in Program.cs so User.Identity.Name is predictable
    // regardless of the inbound claim mapping defaults.
    public const string UsernameClaim = "username";

    public const string DisplayNameClaim = "displayName";
}

public class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options)
    {
        _options = options;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(AppUser user)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddHours(_options.ExpiryHours);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,

            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtOptions.UsernameClaim, user.Username),
                new Claim(JwtOptions.DisplayNameClaim, user.DisplayName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),

            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}
