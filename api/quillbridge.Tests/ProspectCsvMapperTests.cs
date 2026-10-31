using quillbridge.Application.Import;
using quillbridge.Domain.Enums;

namespace quillbridge.Tests;

public class ProspectCsvMapperTests
{
    private static readonly Guid PriyaId = Guid.NewGuid();
    private static readonly Dictionary<string, Guid> Sdrs = new(StringComparer.OrdinalIgnoreCase) { ["Priya Raman"] = PriyaId };
    private static readonly DateTime Today = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

    private static ProspectCsvRow Row(Action<ProspectCsvRow>? configure = null)
    {
        var row = new ProspectCsvRow
        {
            ProspectId = "P-1001",
            FirstName = "Tanya",
            LastName = "Chen",
            Company = "Ferncliff Precision Ltd",
            Industry = "Industrial manufacturing",
            Employees = "1,165",
            Hq = "London, UK",
            FitTier = "A",
            Trigger = "ACQUISITION",
            InterestScore = "66",
            LeadSource = "Bought contact list",
            LastTouch = "Never contacted",
            AssignedSdr = "Priya Raman",
            PhoneType = "Direct",
            DoNotCall = "N"
        };
        configure?.Invoke(row);

        return row;
    }

    [Theory]
    [InlineData("1,165", 1165)]
    [InlineData("~4850", 4850)]
    [InlineData("5,000+", 5000)]
    [InlineData("10387", 10387)]
    public void ParseEmployees_HandlesSourceFormats(string value, int expected)
    {
        Assert.Equal(expected, ProspectCsvMapper.ParseEmployees(value));
    }

    [Fact]
    public void Map_NeverContacted_HasNoLastTouch()
    {
        var prospect = ProspectCsvMapper.Map(Row(), Sdrs, Today).Prospect!;

        Assert.Null(prospect.LastTouch);
        Assert.Null(prospect.LastTouchType);
        Assert.Equal(PriyaId, prospect.SdrId);
        Assert.Equal(Trigger.Acquisition, prospect.Trigger);
        Assert.Equal(FitTier.A, prospect.FitTier);
    }

    [Theory]
    [InlineData("2026-09-16 Call - not interested", 2026, 9, 16, "Call", "not interested")]
    [InlineData("10/02/2026 Call - no answer", 2026, 10, 2, "Call", "no answer")]
    [InlineData("2026-10-12 Filled in website form", 2026, 10, 12, "Filled in website form", null)]
    public void Map_ParsesLastTouch(string value, int y, int m, int d, string type, string? notes)
    {
        var prospect = ProspectCsvMapper.Map(Row(r => r.LastTouch = value), Sdrs, Today).Prospect!;

        Assert.Equal(new DateTime(y, m, d), prospect.LastTouch);
        Assert.Equal(type, prospect.LastTouchType);
        Assert.Equal(notes, prospect.LastTouchNotes);
    }

    [Theory]
    [InlineData("2026-09-16", 2026, 9, 16)]
    [InlineData("2026/9/5", 2026, 9, 5)]
    [InlineData("10/12/2026", 2026, 10, 12)]
    [InlineData("25/10/2026", 2026, 10, 25)]
    [InlineData("14.10.2026", 2026, 10, 14)]
    [InlineData("2026-09-31", 2026, 9, 30)]
    [InlineData("2026-06-31", 2026, 6, 30)]
    public void ParseDate_AcceptsEveryFormatInTheList(string value, int y, int m, int d)
    {
        Assert.Equal(new DateTime(y, m, d), ProspectCsvMapper.ParseDate(value, []));
    }

    [Fact]
    public void Map_DayPastMonthEnd_IsMovedToLastDayAndWarns()
    {
        var result = ProspectCsvMapper.Map(Row(r => r.LastTouch = "2026-09-31 Webinar - signed up, did not attend"), Sdrs, Today);

        Assert.Equal(new DateTime(2026, 9, 30), result.Prospect!.LastTouch);
        Assert.Equal("Webinar", result.Prospect.LastTouchType);
        Assert.Contains(result.Warnings, w => w.Contains("2026-09-31"));
    }

    [Theory]
    [InlineData("Aoife", "Ryan", "LARKSPUR TRANSPORT GROUP", "Larkspur Transport Group")]
    [InlineData("Florian", "Steiner", "Auental Pflegedienste & Co. KG", "Auental Pflegedienste GmbH & Co. KG")]
    [InlineData("Francesca", "Lombardi", "RIVA ALTA INGROSSO S.P.A.", "Riva Alta Ingrosso S.p.A.")]
    public void ContactKey_MatchesTheSamePersonAndCompany(string first, string last, string companyA, string companyB)
    {
        Assert.Equal(ProspectCsvMapper.ContactKey(first, last, companyA), ProspectCsvMapper.ContactKey(first, last, companyB));
    }

    [Fact]
    public void ContactKey_KeepsDifferentCompaniesApart()
    {
        Assert.NotEqual(
            ProspectCsvMapper.ContactKey("Paolo", "Greco", "Torre Nera Negozi S.r.l."),
            ProspectCsvMapper.ContactKey("Paolo", "Greco", "Torre Nera Sanità S.r.l."));
    }

    [Fact]
    public void Map_AskedToStopCalling_SetsDoNotCall()
    {
        var prospect = ProspectCsvMapper.Map(Row(r => r.LastTouch = "2026-09-01 Call - asked us to stop calling"), Sdrs, Today).Prospect!;

        Assert.True(prospect.DoNotCall);
        Assert.Contains("stop calling", prospect.DoNotCallReason);
    }

    [Fact]
    public void Map_UnknownTriggerAndSdr_ImportsWithWarnings()
    {
        var result = ProspectCsvMapper.Map(Row(r => { r.Trigger = "RVW"; r.AssignedSdr = "Tom K."; }), Sdrs, Today);

        Assert.Equal(Trigger.None, result.Prospect!.Trigger);
        Assert.Null(result.Prospect.SdrId);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Map_ExistingCustomer_HasNoTierOrScore()
    {
        var prospect = ProspectCsvMapper.Map(Row(r => { r.FitTier = ""; r.Trigger = ""; r.InterestScore = ""; r.InvoiceProcess = "Quillbridge"; }), Sdrs, Today).Prospect!;

        Assert.Equal(FitTier.None, prospect.FitTier);
        Assert.Null(prospect.InterestScore);
        Assert.Equal(Trigger.None, prospect.Trigger);
    }

    [Fact]
    public void Map_MissingRequiredField_IsSkipped()
    {
        var result = ProspectCsvMapper.Map(Row(r => r.Company = " "), Sdrs, Today);

        Assert.Null(result.Prospect);
        Assert.Contains("company", result.SkipReason);
    }
}
