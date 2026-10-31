using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Application.Import;

public class ProspectMapResult
{
    public Prospect? Prospect { get; init; }
    public string? SkipReason { get; init; }
    public List<string> Warnings { get; init; } = [];
}

// turns one raw CSV row into a Prospect, tolerating the inconsistencies found in the source list
public static partial class ProspectCsvMapper
{
    private const string NeverContacted = "Never contacted";

    private static readonly Dictionary<string, Trigger> Triggers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EXPANSION"] = Trigger.Expansion,
        ["ACQUISITION"] = Trigger.Acquisition,
        ["AUDIT"] = Trigger.Audit,
        ["HIRING"] = Trigger.Hiring,
        ["FUNDING"] = Trigger.Funding,
        ["NEW_LEADER"] = Trigger.NewLeader,
        ["NEW_SYSTEM"] = Trigger.NewSystem
    };

    // year first (2026-09-16, 2026/9/16) or year last (10/12/2026, 12.10.2026, 12-10-2026)
    [GeneratedRegex(@"^(?<date>\d{4}[-/.]\d{1,2}[-/.]\d{1,2}|\d{1,2}[-/.]\d{1,2}[-/.]\d{4})\s+(?<rest>.+)$")]
    private static partial Regex LastTouchPattern();

    private static readonly char[] DateSeparators = ['-', '/', '.'];

    public static ProspectMapResult Map(ProspectCsvRow row, IReadOnlyDictionary<string, Guid> sdrIdsByName, DateTime today)
    {
        var warnings = new List<string>();

        var missing = new[]
            {
                ("prospect_id", row.ProspectId),
                ("first_name", row.FirstName),
                ("last_name", row.LastName),
                ("company", row.Company)
            }
            .Where(f => string.IsNullOrWhiteSpace(f.Item2))
            .Select(f => f.Item1)
            .ToList();

        if (missing.Count > 0)
        {
            return new ProspectMapResult { SkipReason = $"Missing required field(s): {string.Join(", ", missing)}." };
        }

        var employees = ParseEmployees(row.Employees);

        if (employees is null)
        {
            return new ProspectMapResult { SkipReason = $"Invalid employees value '{row.Employees}'." };
        }

        var (lastTouch, lastTouchType, lastTouchNotes) = ParseLastTouch(row.LastTouch, today, warnings);
        var (doNotCall, doNotCallReason) = ParseDoNotCall(row.DoNotCall, lastTouch, lastTouchNotes);

        var prospect = new Prospect
        {
            ProspectId = row.ProspectId.Trim(),
            FirstName = row.FirstName.Trim(),
            LastName = row.LastName.Trim(),
            Title = NullIfEmpty(row.Title),
            Company = row.Company.Trim(),
            Industry = row.Industry.Trim(),
            Employees = employees.Value,
            Hq = row.Hq.Trim(),
            AccountingSystem = NullIfEmpty(row.AccountingSystem),
            InvoiceProcess = NullIfEmpty(row.InvoiceProcess),
            FitTier = ParseFitTier(row.FitTier, warnings),
            Trigger = ParseTrigger(row.Trigger, warnings),
            InterestScore = ParseInterestScore(row.InterestScore, warnings),
            LeadSource = row.LeadSource.Trim(),
            LastTouch = lastTouch,
            LastTouchType = lastTouchType,
            LastTouchNotes = lastTouchNotes,
            SdrId = ResolveSdr(row.AssignedSdr, sdrIdsByName, warnings),
            PhoneType = ParsePhoneType(row.PhoneType, warnings),
            DoNotCall = doNotCall,
            DoNotCallReason = doNotCallReason,
            Notes = NullIfEmpty(row.Notes)
        };

        return new ProspectMapResult { Prospect = prospect, Warnings = warnings };
    }

    // handles "1,165", "~4850" and "5,000+" (the lower bound is kept)
    public static int? ParseEmployees(string value)
    {
        var cleaned = value.Replace(",", "").Replace("~", "").Replace("+", "").Trim();

        return int.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out var employees) ? employees : null;
    }

    // existing customers have no tier
    private static FitTier ParseFitTier(string value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FitTier.None;
        }

        if (Enum.TryParse<FitTier>(value.Trim(), true, out var tier) && tier != FitTier.None)
        {
            return tier;
        }

        warnings.Add($"Unknown fit tier '{value}', imported as None.");

        return FitTier.None;
    }

    private static Trigger ParseTrigger(string value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Trigger.None;
        }

        if (Triggers.TryGetValue(value.Trim(), out var trigger))
        {
            return trigger;
        }

        warnings.Add($"Unknown trigger '{value}', imported as None.");

        return Trigger.None;
    }

    private static int? ParseInterestScore(string value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var score))
        {
            return score;
        }

        warnings.Add($"Invalid interest score '{value}', imported without a score.");

        return null;
    }

    private static (DateTime? Date, string? Type, string? Notes) ParseLastTouch(string value, DateTime today, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals(NeverContacted, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, null);
        }

        var match = LastTouchPattern().Match(value.Trim());

        if (!match.Success)
        {
            warnings.Add($"Last touch '{value}' has no date, kept as text only.");

            return (null, value.Trim(), null);
        }

        var rawDate = match.Groups["date"].Value;
        var date = ParseDate(rawDate, warnings);

        if (date > today)
        {
            warnings.Add($"Last touch date {rawDate} is in the future.");
        }

        // "Call - left voicemail" => type "Call", notes "left voicemail"
        var rest = match.Groups["rest"].Value;
        var separator = rest.IndexOf(" - ", StringComparison.Ordinal);

        return separator < 0
            ? (date, rest.Trim(), null)
            : (date, rest[..separator].Trim(), NullIfEmpty(rest[(separator + 3)..]));
    }

    // accepts any day/month order the list uses; a day past the end of the month
    // (2026-09-31) is a typo, so it is moved to the month's last day instead of being dropped
    public static DateTime? ParseDate(string value, List<string> warnings)
    {
        var parts = value.Split(DateSeparators).Select(int.Parse).ToArray();

        int year, month, day;

        if (parts[0] > 999)
        {
            (year, month, day) = (parts[0], parts[1], parts[2]);
        }
        else
        {
            year = parts[2];
            // month first (US style) unless the first number cannot be a month
            (month, day) = parts[0] > 12 ? (parts[1], parts[0]) : (parts[0], parts[1]);
        }

        if (month is < 1 or > 12 && day is >= 1 and <= 12)
        {
            (month, day) = (day, month);
        }

        if (year < 1 || month is < 1 or > 12 || day is < 1 or > 31)
        {
            warnings.Add($"Last touch date '{value}' is not a real date, imported without a date.");

            return null;
        }

        var daysInMonth = DateTime.DaysInMonth(year, month);

        if (day > daysInMonth)
        {
            warnings.Add($"Last touch date '{value}' does not exist, imported as the last day of the month.");
            day = daysInMonth;
        }

        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static Guid? ResolveSdr(string name, IReadOnlyDictionary<string, Guid> sdrIdsByName, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            warnings.Add("No SDR assigned.");

            return null;
        }

        if (sdrIdsByName.TryGetValue(name.Trim(), out var sdrId))
        {
            return sdrId;
        }

        warnings.Add($"SDR '{name}' is not a user in the system, prospect left unassigned.");

        return null;
    }

    private static PhoneType ParsePhoneType(string value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PhoneType.Unknown;
        }

        if (Enum.TryParse<PhoneType>(value.Trim(), true, out var phoneType))
        {
            return phoneType;
        }

        warnings.Add($"Unknown phone type '{value}', imported as Unknown.");

        return PhoneType.Unknown;
    }

    // besides the explicit flag, a prospect who asked us to stop calling or whose
    // number reaches a private home must not be called either
    private static (bool DoNotCall, string? Reason) ParseDoNotCall(string value, DateTime? lastTouch, string? lastTouchNotes)
    {
        var reasons = new List<string>();

        if (value.Trim().Equals("Y", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("Marked do-not-call in the source list.");
        }

        if (lastTouchNotes?.Contains("asked us to stop calling", StringComparison.OrdinalIgnoreCase) == true)
        {
            var on = lastTouch.HasValue ? $" on {lastTouch:yyyy-MM-dd}" : string.Empty;
            reasons.Add($"Asked us to stop calling{on}.");
        }

        if (lastTouchNotes?.Contains("wrong number", StringComparison.OrdinalIgnoreCase) == true)
        {
            reasons.Add("Number on file is wrong and reaches a private home.");
        }

        return reasons.Count > 0 ? (true, string.Join(" ", reasons)) : (false, null);
    }

    // the same person at the same company, ignoring case, accents, punctuation and legal
    // suffixes, so "Auental Pflegedienste GmbH & Co. KG" matches "AUENTAL PFLEGEDIENSTE & Co. KG"
    public static string ContactKey(string firstName, string lastName, string company)
    {
        var companyWords = Normalize(company)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !LegalSuffixes.Contains(w));

        return $"{Normalize(firstName)}|{Normalize(lastName)}|{string.Join(' ', companyWords)}";
    }

    private static readonly HashSet<string> LegalSuffixes =
    [
        "ltd", "llc", "inc", "plc", "co", "gmbh", "kg", "ag", "bv", "nv", "sa", "sas", "sl",
        "spa", "srl", "ab", "as", "a/s", "oy", "lda"
    ];

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            // dots are dropped so "S.r.l." and "B.V." become "srl" and "bv"
            if (c == '.')
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) || c == '/' ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
