using quillbridge.Application.Wrappers;

namespace quillbridge.Application.CustomExceptions;

public class QBNotFoundException : Exception
{
    public QBNotFoundException(string message, string code = ErrorCodes.NotFound) : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
