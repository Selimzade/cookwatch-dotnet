using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using CookWatch.API.Models;

namespace CookWatch.API.Services;

public class TokenService
{
    private readonly IConfiguration _configuration;
    private readonly string _secret;
    private readonly TimeSpan _expiresIn;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        _secret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT secret is not configured.");

        var expiresInStr = Environment.GetEnvironmentVariable("JWT_EXPIRES_IN")
            ?? configuration["Jwt:ExpiresIn"]
            ?? "7d";

        _expiresIn = ParseExpiry(expiresInStr);
    }

    public string GenerateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("username", user.Username),
            new Claim("shareId", user.ShareId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.Add(_expiresIn),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }

    public string GetSecretKey() => _secret;

    private static TimeSpan ParseExpiry(string expiry)
    {
        if (expiry.EndsWith('d') && int.TryParse(expiry[..^1], out var days))
            return TimeSpan.FromDays(days);
        if (expiry.EndsWith('h') && int.TryParse(expiry[..^1], out var hours))
            return TimeSpan.FromHours(hours);
        if (expiry.EndsWith('m') && int.TryParse(expiry[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);
        if (int.TryParse(expiry, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        return TimeSpan.FromDays(7); // default 7 days
    }
}
