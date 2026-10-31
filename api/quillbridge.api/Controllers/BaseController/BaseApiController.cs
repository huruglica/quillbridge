using Microsoft.AspNetCore.Mvc;
using quillbridge.Application.DTOs.Common;
using quillbridge.Application.Wrappers;

namespace quillbridge.api.Controllers.BaseController;

[ApiController]
[Route("api/[controller]")]
public class BaseApiController : ControllerBase
{
    public IActionResult ReturnToBaseResponse<T>(T data)
    {
        var response = BaseResponse<T>.Ok(data);

        return Ok(response);
    }

    public IActionResult ReturnToBasePagedResponse<T>(PagedResult<T> result)
    {
        var response = new BasePagedResponse<List<T>>(result.Items, result.PageNumber, result.PageSize, result.TotalCount);

        return Ok(response);
    }

    public IActionResult ReturnToBaseCreatedResponse<T>(T data, string actionName, object routeValues)
    {
        var response = BaseResponse<T>.Ok(data);

        return CreatedAtAction(actionName, routeValues, response);
    }
}
