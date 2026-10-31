using CsvHelper.Configuration.Attributes;

namespace quillbridge.Application.Import;

public class ProspectCsvRow
{
    [Name("prospect_id")] public string ProspectId { get; set; } = string.Empty;
    [Name("first_name")] public string FirstName { get; set; } = string.Empty;
    [Name("last_name")] public string LastName { get; set; } = string.Empty;
    [Name("title")] public string Title { get; set; } = string.Empty;
    [Name("company")] public string Company { get; set; } = string.Empty;
    [Name("industry")] public string Industry { get; set; } = string.Empty;
    [Name("employees")] public string Employees { get; set; } = string.Empty;
    [Name("hq")] public string Hq { get; set; } = string.Empty;
    [Name("accounting_system")] public string AccountingSystem { get; set; } = string.Empty;
    [Name("invoice_process")] public string InvoiceProcess { get; set; } = string.Empty;
    [Name("fit_tier")] public string FitTier { get; set; } = string.Empty;
    [Name("trigger")] public string Trigger { get; set; } = string.Empty;
    [Name("interest_score")] public string InterestScore { get; set; } = string.Empty;
    [Name("lead_source")] public string LeadSource { get; set; } = string.Empty;
    [Name("last_touch")] public string LastTouch { get; set; } = string.Empty;
    [Name("assigned_sdr")] public string AssignedSdr { get; set; } = string.Empty;
    [Name("phone_type")] public string PhoneType { get; set; } = string.Empty;
    [Name("do_not_call")] public string DoNotCall { get; set; } = string.Empty;
    [Name("notes")] public string Notes { get; set; } = string.Empty;
}
