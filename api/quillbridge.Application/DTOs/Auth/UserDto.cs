namespace quillbridge.Application.DTOs.Auth;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public string Region { get; set; } = string.Empty;
    public List<string> Languages { get; set; } = [];
    public IList<string> Roles { get; set; } = [];
}
