using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MusiQL.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
