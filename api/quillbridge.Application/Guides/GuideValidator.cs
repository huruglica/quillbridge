using System.Text.Json;
using System.Text.RegularExpressions;

namespace quillbridge.Application.Guides;

// checks a guide against the fixed structure and the hard rules: no prices or offers, no customers or
// numbers that are not in our facts or the prospect's data, and no claimed integration we do not have
public static partial class GuideValidator
{
    private const int MaxFieldLength = 420;

    // keeps accents readable so "é" does not add stray digits to the allowed numbers
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // accounting software we do NOT connect to; any mention needs the "technical team" answer next to it
    private static readonly string[] UnsupportedSystems =
    [
        "SAP", "S/4HANA", "Business One", "Oracle Fusion", "Oracle E-Business", "JD Edwards", "Xero", "QuickBooks",
        "Sage 50", "Sage 100", "Sage 200", "Sage 300", "Sage X3", "DATEV", "Exact Online", "Fortnox", "Visma", "Odoo",
        "Acumatica", "Zucchetti", "TeamSystem", "e-conomic", "Tripletex", "Twinfield", "AFAS", "Lexware", "BMD",
        "Primavera", "PHC", "A3 ERP", "Uniconta", "F&O", "Finance and Operations", "Finance & Operations", "Dynamics AX",
        "Dynamics NAV", "Workday", "Infor", "Epicor", "MYOB", "FreshBooks", "Holded", "Navision"
    ];

    // commercial offers SDRs cannot make (e.g. an expired "free pilot" promo in the notes)
    [GeneratedRegex(@"\bpilot|free trial|discount|rabatt|kostenlos|gratis|sconto|prova gratuita|descuento|prueba gratuita|indirim|ücretsiz", RegexOptions.IgnoreCase)]
    private static partial Regex OfferPattern();

    // any amount of money, a per-licence fee, or "it costs about N"
    [GeneratedRegex(@"[$€£¥]\s?\d|\d\s?(?:[$€£]|usd|eur|gbp|chf|sek|nok|dkk|dollars?|euros?|pounds?|franken|k\b)|\bper (?:user|seat|licen[cs]e)\b|\b(?:costs?|priced at|pricing starts at|kostet|costa|cuesta)\s+(?:only |just |around |about |from |nur |solo |circa |unos )?\S*\d", RegexOptions.IgnoreCase)]
    private static partial Regex PricePattern();

    // "1,500" is one number; "16.09.2026" is three
    [GeneratedRegex(@"\d+(?:[.,\s]\d{3}(?!\d))*")]
    private static partial Regex NumberPattern();

    // generic claims of broad compatibility are also integration claims
    [GeneratedRegex(@"\b(?:any|every|all) (?:erp|accounting (?:system|software)|systems?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AnySystemClaimPattern();

    public static List<string> Validate(GuideContent guide, GuideInput input)
    {
        var errors = new List<string>();

        CheckStructure(guide, input, errors);

        var techPhrase = SalesFacts.TechnicalTeamPhrase.GetValueOrDefault(input.Language, SalesFacts.TechnicalTeamPhrase["en"]);
        var allowedNumbers = AllowedNumbers(input);
        var inputText = JsonSerializer.Serialize(input, Json);
        var unsupportedSystems = UnsupportedSystems
            .Concat(new[] { input.AccountingSystem, input.SwitchingTo }.Where(s => s is not null && !SalesFacts.IsSupportedIntegration(s))!)
            .ToList();

        foreach (var (field, text) in Fields(guide))
        {
            if (text.Length > MaxFieldLength)
            {
                errors.Add($"{field} is {text.Length} characters; keep it under {MaxFieldLength} so the guide fits one screen.");
            }

            if (PricePattern().IsMatch(text))
            {
                errors.Add($"{field} mentions a price or cost per unit; SDRs never give prices.");
            }

            if (OfferPattern().IsMatch(text))
            {
                errors.Add($"{field} offers a pilot, trial or discount; SDRs cannot make offers.");
            }

            if (AnySystemClaimPattern().IsMatch(text))
            {
                errors.Add($"{field} claims compatibility with any system; only {string.Join(", ", SalesFacts.SupportedIntegrations)} are supported.");
            }

            foreach (var number in NumberPattern().Matches(text).Select(m => Normalize(m.Value)))
            {
                if (!allowedNumbers.Contains(number))
                {
                    errors.Add($"{field} contains the number '{number}', which is not in the prospect data or our approved facts.");
                }
            }

            foreach (var story in SalesFacts.Stories.Where(s => s.Key != input.StoryKey))
            {
                var name = story.Customer.Split(' ')[0];

                if (text.Contains(name, StringComparison.OrdinalIgnoreCase) && !inputText.Contains(name, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"{field} mentions {story.Customer}; this guide's success story is {input.StoryKey}.");
                }
            }

            var unsupported = unsupportedSystems.FirstOrDefault(s => Regex.IsMatch(text, $@"(?<![\w-]){Regex.Escape(s)}(?![\w-])", RegexOptions.IgnoreCase));

            // key facts report what the prospect said ("asked whether we work with SAP"), not what we claim
            if (unsupported is not null && !field.StartsWith("keyFacts") && !text.Contains(techPhrase, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{field} mentions {unsupported} without \"{techPhrase}\"; we only connect to {string.Join(", ", SalesFacts.SupportedIntegrations)}.");
            }
        }

        return errors;
    }

    private static void CheckStructure(GuideContent guide, GuideInput input, List<string> errors)
    {
        foreach (var (field, text) in Fields(guide))
        {
            if (string.IsNullOrWhiteSpace(text) && field != "lastTouch" && !field.StartsWith("keyFacts"))
            {
                errors.Add($"{field} is empty.");
            }
        }

        if (guide.DiscoveryQuestions.Count != 2)
        {
            errors.Add($"Expected exactly 2 discovery questions, got {guide.DiscoveryQuestions.Count}.");
        }

        var keys = guide.Pushbacks.Select(p => p.Key).ToList();

        if (!keys.Order().SequenceEqual(input.PushbackKeys.Order()))
        {
            errors.Add($"Pushbacks must be exactly [{string.Join(", ", input.PushbackKeys)}], got [{string.Join(", ", keys)}].");
        }

        if (guide.KeyFacts.Count > 2)
        {
            errors.Add($"At most 2 key facts, got {guide.KeyFacts.Count}.");
        }

        if (input.LastTouch is not null && string.IsNullOrWhiteSpace(guide.LastTouch))
        {
            errors.Add("lastTouch is empty but we contacted this prospect before.");
        }

        if (input.LastTouch is null && !string.IsNullOrWhiteSpace(guide.LastTouch))
        {
            errors.Add("lastTouch must be empty: we never contacted this prospect.");
        }
    }

    private static IEnumerable<(string Field, string Text)> Fields(GuideContent guide)
    {
        yield return ("opening", guide.Opening);
        yield return ("whyNow", guide.WhyNow);

        for (var i = 0; i < guide.DiscoveryQuestions.Count; i++)
        {
            yield return ($"discoveryQuestions[{i}]", guide.DiscoveryQuestions[i]);
        }

        yield return ("coreMessage", guide.CoreMessage);
        yield return ("successStory", guide.SuccessStory);
        yield return ("meetingAsk", guide.MeetingAsk);

        foreach (var pushback in guide.Pushbacks)
        {
            yield return ($"pushbacks.{pushback.Key}.objection", pushback.Objection);
            yield return ($"pushbacks.{pushback.Key}.answer", pushback.Answer);
        }

        yield return ("planB.switchboard", guide.PlanB.Switchboard);
        yield return ("planB.voicemail", guide.PlanB.Voicemail);
        yield return ("planB.sendEmail", guide.PlanB.SendEmail);
        yield return ("planB.gatekeeper", guide.PlanB.Gatekeeper);
        yield return ("planB.wrongPerson", guide.PlanB.WrongPerson);
        yield return ("lastTouch", guide.LastTouch);

        for (var i = 0; i < guide.KeyFacts.Count; i++)
        {
            yield return ($"keyFacts[{i}]", guide.KeyFacts[i]);
        }
    }

    // numbers the guide may use: anything in the prospect data or our facts, plus the 30-minute meeting,
    // and small counting words ("2 questions", "1 example")
    private static HashSet<string> AllowedNumbers(GuideInput input)
    {
        var allowed = NumberPattern().Matches(JsonSerializer.Serialize(input, Json))
            .Select(m => Normalize(m.Value))
            .ToHashSet();

        allowed.UnionWith(["0", "1", "2", "3", "4", "5", "30", "365"]);

        return allowed;
    }

    // "1,500" "1.500" and "1 500" all mean 1500; "09" is 9
    private static string Normalize(string number)
    {
        var digits = new string(number.Where(char.IsDigit).ToArray()).TrimStart('0');

        return digits.Length > 0 ? digits : "0";
    }
}
