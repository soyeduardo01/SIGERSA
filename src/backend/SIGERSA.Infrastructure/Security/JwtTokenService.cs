using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SIGERSA.Domain.Security;
using SIGERSA.Infrastructure.Configuration;

namespace SIGERSA.Infrastructure.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public IssuedToken CreateAccessToken(AuthenticationUser user, DateTimeOffset now) =>
        Create(user, now, now.AddMinutes(_options.AccessTokenMinutes), "access", includeRoles: true);

    public IssuedToken CreatePasswordResetToken(AuthenticationUser user, DateTimeOffset now) =>
        Create(user, now, now.AddMinutes(_options.ResetTokenMinutes), "password_reset", includeRoles: false);

    private IssuedToken Create(AuthenticationUser user, DateTimeOffset now, DateTimeOffset expiresAt, string purpose, bool includeRoles)
    {
        var tokenId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Correo),
            new(ClaimTypes.Name, user.NombreCompleto),
            new("purpose", purpose),
            new(JwtRegisteredClaimNames.Jti, tokenId.ToString())
        };
        if (user.EmpresaId is { } companyId) claims.Add(new Claim("company_id", companyId.ToString()));
        if (includeRoles) claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            credentials);
        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt, tokenId);
    }
}
