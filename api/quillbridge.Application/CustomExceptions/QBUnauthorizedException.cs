using quillbridge.Application.Wrappers;

namespace quillbridge.Application.CustomExceptions;

public class QBUnauthorizedException : Exception
{
    public QBUnauthorizedException(string message, string code = ErrorCodes.Unauthorized) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
