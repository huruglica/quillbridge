using quillbridge.Domain.Common;
using quillbridge.Domain.Enums;

namespace quillbridge.Domain.Models;

public class Prospect : BaseEntity
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
    public string? InvoiceProcess { get; set; }
    public FitTier FitTier { get; set; }
    public Trigger Trigger { get; set; }
    public int? InterestScore { get; set; }
    public string LeadSource { get; set; } = string.Empty;
    public DateTime? LastTouch { get; set; }
    public string? LastTouchType { get; set; }
    public string? LastTouchNotes { get; set; }
    public Guid? SdrId { get; set; }
    public User? Sdr { get; set; }
    public PhoneType PhoneType { get; set; }
    public bool DoNotCall { get; set; }
    public string? DoNotCallReason { get; set; }
    public string? Notes { get; set; }
}
