using Microsoft.AspNetCore.Identity;
using quillbridge.Domain.Common;

namespace quillbridge.Domain.Models;

public class User : IdentityUser<Guid>, IBaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    // experience is derived from this date, so it never goes stale
    public DateTime StartDate { get; set; }
    public string Region { get; set; } = string.Empty;
    public List<string> Languages { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
