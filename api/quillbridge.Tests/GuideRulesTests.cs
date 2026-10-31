using quillbridge.Application.BusinessRules;
using quillbridge.Application.Guides;
using quillbridge.Domain.Enums;
using quillbridge.Tests.Fakes;

namespace quillbridge.Tests;

public class GuideRulesTests
{
    [Theory]
    [InlineData("CFO", ProspectRole.FinanceLeader)]
    [InlineData("Vice President, Finance", ProspectRole.FinanceLeader)]
    [InlineData("Financieel Directeur", ProspectRole.FinanceLeader)]
    [InlineData("Kaufmännische Leitung", ProspectRole.FinanceLeader)]
    [InlineData("Responsabile Amministrazione e Finanza", ProspectRole.FinanceLeader)]
    [InlineData("DAF", ProspectRole.FinanceLeader)]
    [InlineData("Head of Finance Operations", ProspectRole.FinanceOperations)]
    [InlineData("Controllerin", ProspectRole.FinanceOperations)]
    [InlineData("AP Team Lead", ProspectRole.FinanceOperations)]
    [InlineData("Accounts Payable Supervisor", ProspectRole.FinanceOperations)]
    [InlineData("Leiterin Kreditorenbuchhaltung", ProspectRole.FinanceOperations)]
    [InlineData("Responsable de Cuentas a Pagar", ProspectRole.FinanceOperations)]
    [InlineData("Jefa de Contabilidad", ProspectRole.FinanceOperations)]
    [InlineData("Finance", ProspectRole.FinanceOperations)]
    [InlineData("IT Director", ProspectRole.It)]
    [InlineData("IT-Leiterin", ProspectRole.It)]
    [InlineData("Responsabile IT", ProspectRole.It)]
    [InlineData("Diretora de TI", ProspectRole.It)]
    [InlineData("DSI", ProspectRole.It)]
    [InlineData("Office Manager", ProspectRole.NonBuyer)]
    [InlineData("Sales Director", ProspectRole.NonBuyer)]
    [InlineData("Operations Coordinator", ProspectRole.NonBuyer)]
    [InlineData("", ProspectRole.NonBuyer)]
    [InlineData(null, ProspectRole.NonBuyer)]
    [InlineData("Chief Executive Officer", ProspectRole.Other)]
    public void RoleDetector_PlacesTitlesInEveryLanguage(string? title, ProspectRole expected)
    {
        Assert.Equal(expected, RoleDetector.Detect(title));
    }

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(5000, true)]
    [InlineData(5001, false)]
    public void Eligibility_EnforcesCompanySize(int employees, bool allowed)
    {
        var result = ProspectEligibility.Check(TestData.Prospect(p => p.Employees = employees));

        Assert.Equal(allowed, result.Allowed);
    }

    [Fact]
    public void Eligibility_NeverAllowsDoNotCall()
    {
        var result = ProspectEligibility.Check(TestData.Prospect(p => { p.DoNotCall = true; p.DoNotCallReason = "Marked do-not-call in the source list."; }));

        Assert.False(result.Allowed);
        Assert.StartsWith("Do not call", result.SkipReason);
    }

    [Fact]
    public void Eligibility_BlocksPrivateHomeNumbersCustomersAndCompetitors()
    {
        Assert.False(ProspectEligibility.Check(TestData.Prospect(p => p.Notes = "The number we have reached someone's home.")).Allowed);
        Assert.False(ProspectEligibility.Check(TestData.Prospect(p => p.InvoiceProcess = "Quillbridge")).Allowed);
        Assert.False(ProspectEligibility.Check(TestData.Prospect(p => p.Industry = "Software (invoice automation)")).Allowed);
    }

    [Fact]
    public void Eligibility_UnassignedProspectGoesToTheRegionSdr()
    {
        var result = ProspectEligibility.Check(TestData.Prospect(p => { p.SdrId = null; p.Sdr = null; p.Hq = "Zurich, CH"; }));

        Assert.True(result.Allowed);
        Assert.Equal("Jonas Weber", result.RegionSdrName);
    }

    [Fact]
    public void Eligibility_UnassignedProspectWithUnknownRegionIsSkipped()
    {
        var result = ProspectEligibility.Check(TestData.Prospect(p => { p.SdrId = null; p.Sdr = null; p.Hq = "Tokyo"; p.Company = "Kaito KK"; }));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Eligibility_FlagsNonBuyersAndUnconfirmedSize()
    {
        var result = ProspectEligibility.Check(TestData.Prospect(p => { p.Title = "Office Manager"; p.Employees = 5000; }));

        Assert.True(result.Allowed);
        Assert.Equal(ProspectRole.NonBuyer, result.Role);
        Assert.Equal(2, result.Flags.Count);
    }

    [Theory]
    [InlineData("Leeds", "Coldspring Contracting Group", "UK")]
    [InlineData("Groningen", "Oosterwijk Bouw B.V.", "NL")]
    [InlineData("Birmingham, United Kingdom", "Whitlock Advisory Group", "UK")]
    [InlineData("Boston, USA", "Northgate Components Group", "US")]
    [InlineData("Nowhere", "Riva Alta Ingrosso S.p.A.", "IT")]
    public void Geography_ResolvesCountryWithoutCountryCode(string hq, string company, string expected)
    {
        Assert.Equal(expected, Geography.ResolveCountry(hq, company, new Dictionary<string, string>()));
    }

    [Fact]
    public void Planner_SameInput_SameHash()
    {
        var prospect = TestData.Prospect();

        Assert.Equal(Plan(prospect).InputHash, Plan(TestData.Prospect(p => p.Id = prospect.Id)).InputHash);
    }

    [Theory]
    [InlineData("title")]
    [InlineData("notes")]
    [InlineData("lastTouch")]
    [InlineData("system")]
    [InlineData("trigger")]
    [InlineData("sdr")]
    public void Planner_AnyGuideFieldChange_ChangesHash(string field)
    {
        var before = Plan(TestData.Prospect()).InputHash;
        var after = Plan(TestData.Prospect(p =>
        {
            switch (field)
            {
                case "title": p.Title = "CFO"; break;
                case "notes": p.Notes = "Annual audit starts in November."; break;
                case "lastTouch": p.LastTouch = new DateTime(2026, 9, 1); p.LastTouchType = "Call"; p.LastTouchNotes = "no answer"; break;
                case "system": p.AccountingSystem = "Xero"; break;
                case "trigger": p.Trigger = Trigger.Audit; break;
                case "sdr": p.Sdr = TestData.Jonas; p.SdrId = TestData.Jonas.Id; break;
            }
        })).InputHash;

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Planner_InterestScoreDoesNotChangeTheGuide()
    {
        Assert.Equal(Plan(TestData.Prospect()).InputHash, Plan(TestData.Prospect(p => p.InterestScore = 12)).InputHash);
    }

    [Theory]
    [InlineData("London, UK", Trigger.None, null, "hartwell")]
    [InlineData("Utrecht, NL", Trigger.None, null, "brennor")]
    [InlineData("Miami, US", Trigger.None, null, "callisto")]
    [InlineData("London, UK", Trigger.Audit, null, "callisto")]
    [InlineData("Munich, DE", Trigger.Hiring, null, "brennor")]
    [InlineData("Munich, DE", Trigger.NewLeader, null, "hartwell")]
    [InlineData("Miami, US", Trigger.None, "Referred by a manager at Brennor Foods.", "brennor")]
    public void Planner_MatchesSuccessStory(string hq, Trigger trigger, string? notes, string expected)
    {
        var plan = Plan(TestData.Prospect(p => { p.Hq = hq; p.Trigger = trigger; p.Notes = notes; }));

        Assert.Equal(expected, plan.Input.StoryKey);
    }

    [Fact]
    public void Planner_UsesSdrLanguage_AndProspectPreferenceOnlyIfSdrSpeaksIt()
    {
        Assert.Equal("de", Plan(TestData.Prospect(p => p.Sdr = TestData.Jonas)).Input.Language);
        Assert.Equal("it", Plan(TestData.Prospect(p => p.Sdr = TestData.Luca)).Input.Language);
        Assert.Equal("en", Plan(TestData.Prospect(p => p.Sdr = TestData.Sam)).Input.Language);
        Assert.Equal("es", Plan(TestData.Prospect(p => { p.Sdr = TestData.Sam; p.Notes = "Prefiere hablar en español."; })).Input.Language);

        var french = Plan(TestData.Prospect(p => { p.Sdr = TestData.Luca; p.Notes = "Préfère échanger en français."; }));
        Assert.Equal("it", french.Input.Language);
        Assert.Contains(french.Flags, f => f.Contains("French"));
    }

    [Fact]
    public void Planner_DropsSalesOpsInstructionsFromNotes()
    {
        var plan = Plan(TestData.Prospect(p => p.Notes = "Raised new investment in June 2026. Sales ops: the SAP connector went live in September, fine to confirm on calls."));

        Assert.Equal(["Raised new investment in June 2026."], plan.Input.Notes);
        Assert.Contains(plan.Flags, f => f.Contains("SAP connector"));
    }

    [Fact]
    public void Planner_PicksPushbacksFromThePersonsSituation()
    {
        var switching = Plan(TestData.Prospect(p => { p.Trigger = Trigger.NewSystem; p.InvoiceProcess = "Coupa"; }));
        Assert.Contains("switching_accounting", switching.Input.PushbackKeys);
        Assert.Contains("already_have_tool", switching.Input.PushbackKeys);

        var it = Plan(TestData.Prospect(p => { p.Title = "IT Manager"; p.AccountingSystem = "SAP Business One"; }));
        Assert.Contains("integration", it.Input.PushbackKeys);
        Assert.InRange(it.Input.PushbackKeys.Count, 3, 4);
    }

    [Fact]
    public void Planner_DifferentPeopleGetDifferentGuides()
    {
        var cfo = TemplateGuideWriter.Write(Plan(TestData.Prospect(p => { p.Title = "CFO"; p.Trigger = Trigger.Funding; })).Input);
        var it = TemplateGuideWriter.Write(Plan(TestData.Prospect(p => { p.Title = "IT Manager"; p.Trigger = Trigger.NewSystem; })).Input);

        Assert.NotEqual(cfo.WhyNow, it.WhyNow);
        Assert.NotEqual(cfo.DiscoveryQuestions, it.DiscoveryQuestions);
    }

    [Theory]
    [InlineData("Priya", "Financial Controller", "Sage Intacct", Trigger.Acquisition, null)]
    [InlineData("Jonas", "IT-Leiter", "SAP Business One", Trigger.NewSystem, "Switching to SAP Business One, going live in April 2027.")]
    [InlineData("Luca", "Office Manager", null, Trigger.None, "Asked during the webinar: \"Do you work with SAP S/4HANA?\"")]
    [InlineData("Sam", "CFO", "Xero", Trigger.Audit, "Form comment: \"Please send pricing.\"")]
    public void Template_AlwaysPassesTheValidator(string sdr, string title, string? system, Trigger trigger, string? notes)
    {
        var user = new[] { TestData.Priya, TestData.Jonas, TestData.Luca, TestData.Sam }.Single(u => u.FirstName == sdr);
        var plan = Plan(TestData.Prospect(p =>
        {
            p.Sdr = user; p.Title = title; p.AccountingSystem = system; p.Trigger = trigger; p.Notes = notes;
            p.LastTouch = new DateTime(2026, 9, 16); p.LastTouchType = "Call"; p.LastTouchNotes = "not interested";
        }));

        Assert.Equal("", string.Join(" | ", GuideValidator.Validate(TemplateGuideWriter.Write(plan.Input), plan.Input)));
    }

    [Fact]
    public void Validator_AcceptsAGuideThatFollowsTheRules()
    {
        var input = Plan(TestData.Prospect()).Input;

        Assert.Empty(GuideValidator.Validate(TestData.ValidGuide(input), input));
    }

    [Theory]
    [InlineData("It costs about €2 per user.", "price")]
    [InlineData("Plans start at $500 a year.", "price")]
    [InlineData("We can offer you a free pilot.", "offers")]
    [InlineData("We integrate with SAP too.", "SAP")]
    [InlineData("Callisto Health Group had a clean audit.", "Callisto")]
    [InlineData("Acme Corp saved 73% of their time.", "73")]
    [InlineData("We work with any ERP.", "any system")]
    public void Validator_RejectsBrokenRules(string coreMessage, string expectedInError)
    {
        var input = Plan(TestData.Prospect()).Input;
        var guide = TestData.ValidGuide(input);
        guide.CoreMessage = coreMessage;

        Assert.Contains(GuideValidator.Validate(guide, input), e => e.Contains(expectedInError, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_AllowsUnsupportedSystemOnlyWithTechnicalTeamAnswer()
    {
        var input = Plan(TestData.Prospect(p => p.AccountingSystem = "Xero")).Input;
        var guide = TestData.ValidGuide(input);

        guide.CoreMessage = "On Xero: let me get our technical team to answer that.";
        Assert.Empty(GuideValidator.Validate(guide, input));

        guide.CoreMessage = "It connects to Xero.";
        Assert.NotEmpty(GuideValidator.Validate(guide, input));
    }

    [Fact]
    public void Validator_ChecksStructure()
    {
        var input = Plan(TestData.Prospect()).Input;
        var guide = TestData.ValidGuide(input);
        guide.DiscoveryQuestions = ["Only one?"];
        guide.Pushbacks.RemoveAt(0);
        guide.PlanB.Voicemail = "";

        var errors = GuideValidator.Validate(guide, input);

        Assert.Contains(errors, e => e.Contains("2 discovery questions"));
        Assert.Contains(errors, e => e.Contains("Pushbacks"));
        Assert.Contains(errors, e => e.Contains("planB.voicemail"));
    }

    [Fact]
    public void Validator_AcceptsNumbersFromTheProspectData()
    {
        var input = Plan(TestData.Prospect(p => p.Notes = "Form comment: \"We get about 1,500 invoices a month.\"")).Input;
        var guide = TestData.ValidGuide(input);
        guide.WhyNow = "Mit etwa 1.500 Rechnungen im Monat seit dem 16.09.";

        Assert.DoesNotContain(GuideValidator.Validate(guide, input), e => e.Contains("1500"));
    }

    private static GuidePlan Plan(Domain.Models.Prospect prospect)
    {
        return GuidePlanner.Plan(prospect, prospect.Sdr, ProspectEligibility.Check(prospect));
    }
}
