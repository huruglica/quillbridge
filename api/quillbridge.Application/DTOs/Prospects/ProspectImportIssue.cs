namespace quillbridge.Application.DTOs.Prospects;

public class ProspectImportIssue
{
    public int Row { get; set; }
    public string? ProspectId { get; set; }
    public string Message { get; set; } = string.Empty;
}
