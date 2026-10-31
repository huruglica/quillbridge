namespace quillbridge.Application.DTOs.Prospects;

public class DoNotCallItemDto
{
    public string ProspectId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Hq { get; set; } = string.Empty;
    public string? DoNotCallReason { get; set; }
}
