using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Application.BusinessRules;

public record GuideEligibility(string? SkipReason, ProspectRole Role, string? Country, string? RegionSdrName, List<string> Flags)
{
    public bool Allowed => SkipReason is null;
}

// decides, when a guide is requested, whether the prospect may get one and what the SDR should know.
// Agreed with sales: 5,000 employees ("5,000+" in the file) is in range but flagged, a prospect without a
// team SDR gets the guide of the SDR covering the HQ region, and non-buyer titles get a referral guide.
public static class ProspectEligibility
{
    public const int MinEmployees = 200;
    public const int MaxEmployees = 5000;

    public static GuideEligibility Check(Prospect prospect)
    {
        var flags = new List<string>();
        var role = RoleDetector.Detect(prospect.Title);
        var country = Geography.ResolveCountry(prospect.Hq, prospect.Company, new Dictionary<string, string>());
        var regionSdr = prospect.SdrId is null ? Geography.SdrForRegion(country, prospect.Hq) : null;

        if (role == ProspectRole.NonBuyer)
        {
            flags.Add(string.IsNullOrWhiteSpace(prospect.Title)
                ? "No job title: the call is about finding the right finance contact."
                : $"'{prospect.Title}' is not a buyer role: the call is about finding the right finance contact.");
        }

        if (prospect.Employees == MaxEmployees)
        {
            flags.Add("Company size is listed as 5,000 (may be '5,000+'): confirm how many people work there.");
        }

        if (regionSdr is not null)
        {
            flags.Add($"No team SDR assigned: guide written for {regionSdr}, who covers this head office region.");
        }

        return new GuideEligibility(SkipReason(prospect, regionSdr), role, country, regionSdr, flags);
    }

    private static string? SkipReason(Prospect prospect, string? regionSdr)
    {
        if (prospect.DoNotCall)
        {
            return $"Do not call: {prospect.DoNotCallReason}";
        }

        if (prospect.Notes?.Contains("someone's home", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Do not call: the number on file reaches a private home.";
        }

        if (string.Equals(prospect.InvoiceProcess, "Quillbridge", StringComparison.OrdinalIgnoreCase)
            || prospect.LeadSource.Contains("customer list", StringComparison.OrdinalIgnoreCase))
        {
            return "Existing Quillbridge customer: their account manager owns the relationship.";
        }

        if (prospect.Industry.Contains("invoice automation", StringComparison.OrdinalIgnoreCase))
        {
            return "Competitor: the company sells invoice automation software.";
        }

        if (prospect.Employees is < MinEmployees or > MaxEmployees)
        {
            return $"Company size {prospect.Employees:N0} is outside the 200-5,000 employees Quillbridge is built for.";
        }

        if (prospect.SdrId is null && regionSdr is null)
        {
            return $"No assigned SDR, and no SDR covers the head office region '{prospect.Hq}'.";
        }

        return null;
    }
}
