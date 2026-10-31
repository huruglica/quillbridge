namespace quillbridge.Application.DTOs.Prospects;

public class ProspectImportResult
{
    public int TotalRows { get; set; }
    public int Imported { get; set; }
    public int Skipped => SkippedRows.Count;
    public List<ProspectImportIssue> SkippedRows { get; set; } = [];
    public List<ProspectImportIssue> Warnings { get; set; } = [];
}
