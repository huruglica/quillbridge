using Microsoft.AspNetCore.Identity;
using quillbridge.Application.CustomExceptions;
using quillbridge.Application.DTOs.Auth;
using quillbridge.Application.Interfaces;
using quillbridge.Application.Services.IServices;
using quillbridge.Domain.Models;

namespace quillbridge.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<User> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(UserManager<User> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new QBUnauthorizedException("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.CreateAccessToken(user, roles);

        return new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            User = ToDto(user, roles)
        };
    }

    public async Task<UserDto> GetCurrentUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new QBNotFoundException("User not found.");

        var roles = await _userManager.GetRolesAsync(user);

        return ToDto(user, roles);
    }

    private static UserDto ToDto(User user, IList<string> roles)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            StartDate = user.StartDate,
            Region = user.Region,
            Languages = user.Languages,
            Roles = roles
        };
    }
}
