using Microsoft.AspNetCore.Http;

namespace quillbridge.Application.DTOs.Prospects;

public class ImportProspectsRequest
{
    public IFormFile? File { get; set; }
}
