using quillbridge.Domain.Enums;

namespace quillbridge.Application.DTOs.Prospects;

public class CallListItemDto
{
    public string ProspectId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Company { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public int Employees { get; set; }
    public string Hq { get; set; } = string.Empty;
    public string? AccountingSystem { get; set; }
    public FitTier FitTier { get; set; }
    public Trigger Trigger { get; set; }
    public int? InterestScore { get; set; }
    public PhoneType PhoneType { get; set; }
    public DateTime? LastTouch { get; set; }
    public string? LastTouchType { get; set; }
    public double PriorityScore { get; set; }
}
