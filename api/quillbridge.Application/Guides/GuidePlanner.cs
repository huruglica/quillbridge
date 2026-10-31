using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using quillbridge.Application.BusinessRules;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Application.Guides;

// everything that shapes a guide. It is what the AI (or the template) receives, and its hash decides
// whether a stored guide is still valid. Interest score and fit tier are left out on purpose:
// they rank the call list but must not be said on a call.
public record GuideInput
{
    public string Language { get; init; } = "en";
    public string LanguageName { get; init; } = "English";
    public string SdrName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public string Employees { get; init; } = string.Empty;
    public string Hq { get; init; } = string.Empty;
    public string? Country { get; init; }
    public string? AccountingSystem { get; init; }
    // supported | not_supported | unknown
    public string Integration { get; init; } = "unknown";
    public string? SwitchingTo { get; init; }
    public bool? SwitchingToSupported { get; init; }
    public string? InvoiceProcess { get; init; }
    public string? Trigger { get; init; }
    public string? TriggerMeaning { get; init; }
    public string LeadSource { get; init; } = string.Empty;
    public string PhoneType { get; init; } = string.Empty;
    public string? LastTouch { get; init; }
    public List<string> Notes { get; init; } = [];
    public string OpeningAngle { get; init; } = "cold";
    public string StoryKey { get; init; } = string.Empty;
    public string Story { get; init; } = string.Empty;
    public List<string> PushbackKeys { get; init; } = [];
    public string? ProspectLanguageNote { get; init; }
}

public record GuidePlan(GuideInput Input, string InputHash, List<string> Flags);

public static partial class GuidePlanner
{
    private static readonly Dictionary<string, string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["English"] = "en", ["German"] = "de", ["Italian"] = "it", ["Spanish"] = "es", ["Turkish"] = "tr",
        ["French"] = "fr", ["Dutch"] = "nl", ["Portuguese"] = "pt"
    };

    // how prospects state a language preference in the notes
    private static readonly (string Pattern, string Language)[] PreferencePatterns =
    [
        ("français|french", "French"), ("español|spanish", "Spanish"), ("deutsch|german", "German"),
        ("italiano|italian", "Italian"), ("nederlands|dutch", "Dutch"), ("português|portuguese", "Portuguese"),
        ("türkçe|turkish", "Turkish"), ("in english", "English")
    ];

    private static readonly Dictionary<Trigger, string> TriggerMeanings = new()
    {
        [Trigger.NewSystem] = "switching accounting software",
        [Trigger.NewLeader] = "new head of finance",
        [Trigger.Funding] = "raised money from investors",
        [Trigger.Hiring] = "hiring invoice staff",
        [Trigger.Audit] = "facing an outside check of their accounts",
        [Trigger.Acquisition] = "bought or merged with another company",
        [Trigger.Expansion] = "opening new sites or entering new countries"
    };

    private static readonly string[] InvoiceTools = ["Tipalti", "Basware", "Coupa", "Bill.com", "AvidXchange"];

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z""ÀÉÖÜ])")]
    private static partial Regex SentenceBreak();

    [GeneratedRegex(@"Switching to (?<target>[^,.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SwitchingPattern();

    [GeneratedRegex(@"replied|attended|chatted|call back|accepted|clicked|opened|filled in|spoke|check-in", RegexOptions.IgnoreCase)]
    private static partial Regex EngagedPattern();

    public static GuidePlan Plan(Prospect prospect, User? sdr, GuideEligibility eligibility)
    {
        var flags = new List<string>(eligibility.Flags);
        var notes = CleanNotes(prospect.Notes, flags);
        var (language, languageNote) = ChooseLanguage(sdr, prospect.Notes);
        var switching = SwitchingTarget(prospect.Notes);
        var hasSystem = !string.IsNullOrWhiteSpace(prospect.AccountingSystem);

        if (languageNote is not null)
        {
            flags.Add(languageNote);
        }

        var input = new GuideInput
        {
            Language = LanguageCodes.GetValueOrDefault(language, "en"),
            LanguageName = language,
            SdrName = sdr is null ? "your SDR" : $"{sdr.FirstName} {sdr.LastName}",
            FirstName = prospect.FirstName,
            LastName = prospect.LastName,
            Title = prospect.Title,
            Role = eligibility.Role.ToString(),
            Company = DisplayCompany(prospect.Company),
            Industry = prospect.Industry,
            Employees = prospect.Employees.ToString("N0", CultureInfo.InvariantCulture),
            Hq = prospect.Hq,
            Country = eligibility.Country,
            AccountingSystem = prospect.AccountingSystem,
            Integration = !hasSystem ? "unknown" : SalesFacts.IsSupportedIntegration(prospect.AccountingSystem) ? "supported" : "not_supported",
            SwitchingTo = switching,
            SwitchingToSupported = switching is null ? null : SalesFacts.IsSupportedIntegration(switching),
            InvoiceProcess = prospect.InvoiceProcess,
            Trigger = prospect.Trigger == Trigger.None ? null : prospect.Trigger.ToString(),
            TriggerMeaning = TriggerMeanings.GetValueOrDefault(prospect.Trigger),
            LeadSource = prospect.LeadSource,
            PhoneType = prospect.PhoneType.ToString(),
            LastTouch = LastTouchText(prospect),
            Notes = notes,
            OpeningAngle = OpeningAngle(prospect, notes),
            StoryKey = ChooseStory(prospect, eligibility.Country).Key,
            Story = StoryText(ChooseStory(prospect, eligibility.Country)),
            PushbackKeys = ChoosePushbacks(prospect, eligibility.Role, notes, switching),
            ProspectLanguageNote = languageNote
        };

        return new GuidePlan(input, Hash(input), flags);
    }

    public static string Hash(GuideInput input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input)));

        return Convert.ToHexString(bytes);
    }

    public static string StoryText(SuccessStory story)
    {
        return $"{story.Customer} ({story.Country}): {story.Fact}.";
    }

    // the guide uses the SDR's language; a prospect's stated preference wins only if the SDR speaks it
    public static (string Language, string? Note) ChooseLanguage(User? sdr, string? notes)
    {
        var spoken = sdr?.Languages is { Count: > 0 } languages ? languages : ["English"];
        var preferred = notes is null
            ? null
            : PreferencePatterns.FirstOrDefault(p => Regex.IsMatch(notes, p.Pattern, RegexOptions.IgnoreCase)).Language;

        if (preferred is null || spoken.Contains(preferred, StringComparer.OrdinalIgnoreCase))
        {
            return (preferred ?? spoken[0], null);
        }

        var sdrName = sdr is null ? "The SDR" : sdr.FirstName;

        return (spoken[0], $"Prospect prefers {preferred}, which {sdrName} does not speak: offer {string.Join(" or ", spoken)}, or ask a colleague who speaks {preferred} to call.");
    }

    // "Sales ops" sentences are internal instructions (connectors, promotions) that may contradict
    // what SDRs are allowed to say, so they never reach the guide
    public static List<string> CleanNotes(string? notes, List<string> flags)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return [];
        }

        var kept = new List<string>();

        foreach (var sentence in SentenceBreak().Split(notes.ReplaceLineEndings(" ")).Select(s => s.Trim()).Where(s => s.Length > 0))
        {
            if (sentence.StartsWith("Sales ops", StringComparison.OrdinalIgnoreCase))
            {
                flags.Add($"Ignored internal note (not approved for calls): \"{sentence}\"");
                continue;
            }

            kept.Add(sentence);
        }

        return kept;
    }

    public static string? SwitchingTarget(string? notes)
    {
        var match = notes is null ? Match.Empty : SwitchingPattern().Match(notes);

        if (!match.Success)
        {
            return null;
        }

        var target = match.Groups["target"].Value.Trim();

        return target.StartsWith("new ", StringComparison.OrdinalIgnoreCase) ? null : target;
    }

    public static SuccessStory ChooseStory(Prospect prospect, string? country)
    {
        // a link to one of the reference customers beats everything else
        var linked = SalesFacts.Stories.FirstOrDefault(s =>
            prospect.Notes?.Contains(s.Customer.Split(' ')[0], StringComparison.OrdinalIgnoreCase) == true);

        if (linked is not null)
        {
            return linked;
        }

        if (prospect.Trigger == Trigger.Audit)
        {
            return SalesFacts.Callisto;
        }

        switch (country)
        {
            case "UK":
                return SalesFacts.Hartwell;
            case "NL":
                return SalesFacts.Brennor;
            case "US":
                return SalesFacts.Callisto;
        }

        // elsewhere: growth stories fit growth triggers, speed fits the rest
        return prospect.Trigger is Trigger.Hiring or Trigger.Expansion or Trigger.Acquisition or Trigger.Funding
            ? SalesFacts.Brennor
            : SalesFacts.Hartwell;
    }

    public static string OpeningAngle(Prospect prospect, List<string> notes)
    {
        var lastTouch = $"{prospect.LastTouchType} {prospect.LastTouchNotes}";

        if (notes.Any(n => n.StartsWith("Referred by", StringComparison.OrdinalIgnoreCase))
            || prospect.LeadSource.StartsWith("Referral", StringComparison.OrdinalIgnoreCase))
        {
            return "referral";
        }

        if (prospect.LastTouchType is not null && EngagedPattern().IsMatch(lastTouch))
        {
            return "follow_up";
        }

        if (prospect.Trigger != Trigger.None)
        {
            return "trigger";
        }

        if (notes.Any(n => n.StartsWith("Met at", StringComparison.OrdinalIgnoreCase)))
        {
            return "event";
        }

        return prospect.LastTouchType is not null ? "follow_up" : "cold";
    }

    // the 3-4 pushbacks this person is most likely to raise, scored from their data
    public static List<string> ChoosePushbacks(Prospect prospect, ProspectRole role, List<string> notes, string? switchingTo)
    {
        var allNotes = string.Join(" ", notes);
        var lastTouch = $"{prospect.LastTouchType} {prospect.LastTouchNotes}";
        var usesTool = InvoiceTools.Any(t => prospect.InvoiceProcess?.Contains(t, StringComparison.OrdinalIgnoreCase) == true);
        var isLeader = role == ProspectRole.FinanceLeader;
        var bought = prospect.LeadSource.Contains("Bought", StringComparison.OrdinalIgnoreCase);

        var scores = new Dictionary<string, double>
        {
            ["already_have_tool"] = usesTool ? 4 : 1,
            ["no_budget"] = (isLeader ? 2.5 : 1.5) + (allNotes.Contains("next year", StringComparison.OrdinalIgnoreCase) ? 2 : 0),
            ["send_email"] = (prospect.LastTouchType == "Email" ? 3 : 1.5) + (prospect.PhoneType == PhoneType.Switchboard ? 0.5 : 0),
            ["switching_accounting"] = prospect.Trigger == Trigger.NewSystem || switchingTo is not null ? 5 : 0,
            ["how_got_number"] = (bought && prospect.LastTouch is null ? 3 : bought ? 2 : 0.5) + (prospect.PhoneType == PhoneType.Mobile ? 1 : 0),
            ["not_interested"] = lastTouch.Contains("not interested", StringComparison.OrdinalIgnoreCase) ? 4.5 : 0,
            ["integration"] = (SalesFacts.IsSupportedIntegration(prospect.AccountingSystem) ? 0 : 2.2) + (role == ProspectRole.It ? 1.5 : 0),
            ["pricing"] = Regex.IsMatch(allNotes, "pric", RegexOptions.IgnoreCase) ? 4 : 0,
            ["outside_accountant"] = prospect.InvoiceProcess?.Contains("outside accountant", StringComparison.OrdinalIgnoreCase) == true ? 3.5 : 0,
            ["not_my_area"] = role is ProspectRole.It or ProspectRole.NonBuyer or ProspectRole.Other ? 2.8 : 0
        };

        var ranked = scores.Where(s => s.Value > 0).OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).Select(s => s.Key);
        var take = scores.Count(s => s.Value >= 2) >= 4 ? 4 : 3;

        return ranked.Take(take).ToList();
    }

    private static string? LastTouchText(Prospect prospect)
    {
        if (prospect.LastTouchType is null)
        {
            return null;
        }

        var date = prospect.LastTouch?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var what = prospect.LastTouchNotes is null ? prospect.LastTouchType : $"{prospect.LastTouchType} - {prospect.LastTouchNotes}";

        return date is null ? what : $"{date}: {what}";
    }

    // "TIDEWATER COMPONENTS GROUP" -> "Tidewater Components Group", "Tamarack Medical Group Group" -> "... Group"
    public static string DisplayCompany(string company)
    {
        var name = Regex.Replace(company.Trim(), @"\b(\w+) \1\b", "$1", RegexOptions.IgnoreCase);

        if (name.Any(char.IsLetter) && name.Where(char.IsLetter).All(char.IsUpper))
        {
            var words = name.ToLowerInvariant().Split(' ');
            name = string.Join(' ', words.Select(w => w.Length <= 4 && w.Contains('.') ? w.ToUpperInvariant() : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(w)));
        }

        return name;
    }
}
