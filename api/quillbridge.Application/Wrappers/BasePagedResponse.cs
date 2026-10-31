namespace quillbridge.Application.Wrappers;

public class BasePagedResponse<T> : BaseResponse<T>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    public BasePagedResponse(T data, int pageNumber, int pageSize, int totalCount, string? message = null)
        : base(data, message)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
        TotalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;
    }
}
