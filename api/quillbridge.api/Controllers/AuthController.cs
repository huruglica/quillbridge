using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using quillbridge.api.Auth;
using quillbridge.api.Controllers.BaseController;
using quillbridge.Application.DTOs.Auth;
using quillbridge.Application.Services.IServices;

namespace quillbridge.api.Controllers;

public class AuthController : BaseApiController
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);

        return ReturnToBaseResponse(response);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = Guid.Parse(User.FindFirst(JwtClaimTypes.UserId)!.Value);
        var response = await _authService.GetCurrentUserAsync(userId);

        return ReturnToBaseResponse(response);
    }
}
