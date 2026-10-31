namespace quillbridge.Application.Wrappers;

public class BaseResponse<T>
{
    public T? Data { get; set; }
    public string? Message { get; set; }
    public bool Success { get; set; }
    public int StatusCode { get; set; }

    protected BaseResponse(string message, bool success = true, int statusCode = 200)
    {
        Message = message;
        Success = success;
        StatusCode = statusCode;
    }

    protected BaseResponse(T data, string? message = null, bool success = true, int statusCode = 200)
    {
        Data = data;
        Message = message;
        Success = success;
        StatusCode = statusCode;
    }

    public static BaseResponse<T> Ok(T data)
    {
        return new BaseResponse<T>(data, null, true, 200);
    }

    public static BaseResponse<T> NotFound(string message)
    {
        return new BaseResponse<T>(message, false, 404);
    }

    public static BaseResponse<T> BadRequest(string message)
    {
        return new BaseResponse<T>(message, false, 400);
    }

    public static BaseResponse<T> Unauthorized(string message)
    {
        return new BaseResponse<T>(message, false, 401);
    }

    public static BaseResponse<T> InternalServerError(string message)
    {
        return new BaseResponse<T>(message, false, 500);
    }
}