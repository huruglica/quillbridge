using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using quillbridge.Application.CustomExceptions;
using quillbridge.Application.Wrappers;

namespace quillbridge.api.Filters;

public class ExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ExceptionFilter> _logger;

    public ExceptionFilter(ILogger<ExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var response = context.Exception switch
        {
            QBNotFoundException notFound => BaseResponse<object>.NotFound(notFound.Message),
            QBBadRequestException badRequest => BaseResponse<object>.BadRequest(badRequest.Message),
            QBUnauthorizedException unauthorized => BaseResponse<object>.Unauthorized(unauthorized.Message),
            _ => Unexpected(context.Exception)
        };

        context.Result = new ObjectResult(response) { StatusCode = response.StatusCode };
        context.ExceptionHandled = true;
    }

    private BaseResponse<object> Unexpected(Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception while processing the request.");

        return BaseResponse<object>.InternalServerError("An unexpected error occurred. Please try again later.");
    }
}
