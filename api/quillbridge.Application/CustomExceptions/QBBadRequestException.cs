using quillbridge.Application.Wrappers;

namespace quillbridge.Application.CustomExceptions;

public class QBBadRequestException : Exception
{
    public QBBadRequestException(string message, string code = ErrorCodes.BadRequest) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
