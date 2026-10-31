using quillbridge.Application.DTOs.Auth;

namespace quillbridge.Application.Services.IServices;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<UserDto> GetCurrentUserAsync(Guid userId);
}
