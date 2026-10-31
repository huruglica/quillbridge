using quillbridge.Domain.Models;

namespace quillbridge.Application.Interfaces;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user, IEnumerable<string> roles);
}
