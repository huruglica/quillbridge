namespace quillbridge.Application.Guides;

public record SuccessStory(string Key, string Customer, string Country, string Fact);

public record PushbackType(string Key, string Objection);

// the only facts an SDR may state about Quillbridge, straight from the brief
public static class SalesFacts
{
    public static readonly SuccessStory Hartwell = new("hartwell", "Hartwell Logistics", "UK", "time to process an invoice cut from 9 days to 2");
    public static readonly SuccessStory Brennor = new("brennor", "Brennor Foods", "Netherlands", "40% more invoices handled with no new hires");
    public static readonly SuccessStory Callisto = new("callisto", "Callisto Health Group", "US", "its first audit after switching found no problems in how it pays bills");

    public static readonly SuccessStory[] Stories = [Hartwell, Brennor, Callisto];

    public static readonly string[] SupportedIntegrations = ["NetSuite", "Sage Intacct", "Microsoft Dynamics 365 Business Central"];

    public static readonly PushbackType[] Pushbacks =
    [
        new("already_have_tool", "We already have a tool."),
        new("no_budget", "No budget until next year."),
        new("send_email", "Just send me an email."),
        new("switching_accounting", "We're switching our accounting software."),
        new("how_got_number", "How did you get my number?"),
        new("not_interested", "We're not interested."),
        new("integration", "Does it work with our accounting software?"),
        new("pricing", "What does it cost?"),
        new("outside_accountant", "Our outside accountant handles the invoices."),
        new("not_my_area", "That's not my area.")
    ];

    public static readonly string[] PlanBKeys = ["switchboard", "voicemail", "sendEmail", "gatekeeper", "wrongPerson"];

    // what to say for any accounting software we do not connect to, per guide language
    public static readonly Dictionary<string, string> TechnicalTeamPhrase = new()
    {
        ["en"] = "let me get our technical team to answer that",
        ["de"] = "das lasse ich gern unser technisches Team beantworten",
        ["it"] = "lascio rispondere il nostro team tecnico",
        ["es"] = "dejo que nuestro equipo técnico responda a eso",
        ["tr"] = "bunu teknik ekibimizin yanıtlamasını sağlayayım"
    };

    public static bool IsSupportedIntegration(string? system)
    {
        if (string.IsNullOrWhiteSpace(system))
        {
            return false;
        }

        return system.Contains("NetSuite", StringComparison.OrdinalIgnoreCase)
            || system.Contains("Sage Intacct", StringComparison.OrdinalIgnoreCase)
            || system.Contains("Business Central", StringComparison.OrdinalIgnoreCase);
    }
}
