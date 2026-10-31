using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using quillbridge.api.Auth;
using quillbridge.api.Controllers.BaseController;
using quillbridge.Application.DTOs.Common;
using quillbridge.Application.DTOs.Prospects;
using quillbridge.Application.Services.IServices;
using quillbridge.Domain.Constants;

namespace quillbridge.api.Controllers;

public class ProspectsController : BaseApiController
{
    private readonly IProspectService _prospectService;

    public ProspectsController(IProspectService prospectService)
    {
        _prospectService = prospectService;
    }

    [Authorize(Roles = Roles.Sdr)]
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Import([FromForm] ImportProspectsRequest request)
    {
        var response = await _prospectService.ImportAsync(request);

        return ReturnToBaseResponse(response);
    }

    [Authorize(Roles = Roles.Sdr)]
    [HttpDelete]
    public async Task<IActionResult> DeleteAll()
    {
        var response = await _prospectService.DeleteAllAsync();

        return ReturnToBaseResponse(response);
    }
    
    [Authorize(Roles = Roles.Sdr)]
    [HttpGet("call-list")]
    public async Task<IActionResult> GetCallList([FromQuery] PagedQueryParameters query)
    {
        var sdrId = Guid.Parse(User.FindFirst(JwtClaimTypes.UserId)!.Value);
        var response = await _prospectService.GetCallListAsync(sdrId, query);

        return ReturnToBasePagedResponse(response);
    }

    [Authorize(Roles = Roles.Sdr)]
    [HttpGet("do-not-call")]
    public async Task<IActionResult> GetDoNotCallList([FromQuery] PagedQueryParameters query)
    {
        var sdrId = Guid.Parse(User.FindFirst(JwtClaimTypes.UserId)!.Value);
        var response = await _prospectService.GetDoNotCallListAsync(sdrId, query);

        return ReturnToBasePagedResponse(response);
    }
}
