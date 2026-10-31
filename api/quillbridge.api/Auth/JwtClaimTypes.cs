using System.IdentityModel.Tokens.Jwt;

namespace quillbridge.api.Auth;

// inbound claim mapping is off, so tokens keep their short JWT claim names
public static class JwtClaimTypes
{
    public const string Role = "role";
    public const string UserId = JwtRegisteredClaimNames.Sub;
}
