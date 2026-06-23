using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EProcurement.AuthService.Models;
using Microsoft.IdentityModel.Tokens;

namespace EProcurement.AuthService.Security;

public sealed class JwtTokenFactory
{
    private readonly IConfiguration _configuration;

    public JwtTokenFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string Create(AppUser user)
    {
        var secret = _configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is required.");
        var issuer = _configuration["Jwt:Issuer"] ?? "eprocurement";
        var audience = _configuration["Jwt:Audience"] ?? "eprocurement-web";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("username", user.Username),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };

        if (user.TenantId is not null)
        {
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
