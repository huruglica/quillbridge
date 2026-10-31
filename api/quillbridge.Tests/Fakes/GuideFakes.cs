using quillbridge.Application.Guides;
using quillbridge.Application.Interfaces;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Tests.Fakes;

public static class TestData
{
    public static readonly User Priya = new() { Id = Guid.NewGuid(), FirstName = "Priya", LastName = "Raman", Languages = ["English"] };
    public static readonly User Jonas = new() { Id = Guid.NewGuid(), FirstName = "Jonas", LastName = "Weber", Languages = ["German", "English"] };
    public static readonly User Luca = new() { Id = Guid.NewGuid(), FirstName = "Luca", LastName = "Ferri", Languages = ["Italian", "English"] };
    public static readonly User Sam = new() { Id = Guid.NewGuid(), FirstName = "Sam", LastName = "Ortega", Languages = ["English", "Spanish"] };

    public static Prospect Prospect(Action<Prospect>? configure = null)
    {
        var prospect = new Prospect
        {
            Id = Guid.NewGuid(),
            ProspectId = "P-1001",
            FirstName = "Tanya",
            LastName = "Chen",
            Title = "Financial Controller",
            Company = "Ferncliff Precision Ltd",
            Industry = "Industrial manufacturing",
            Employees = 1165,
            Hq = "London, UK",
            AccountingSystem = "Sage Intacct",
            InvoiceProcess = "Paper invoices, scanned",
            FitTier = FitTier.A,
            Trigger = Trigger.Acquisition,
            InterestScore = 66,
            LeadSource = "Bought contact list",
            PhoneType = PhoneType.Direct,
            SdrId = Priya.Id,
            Sdr = Priya
        };
        configure?.Invoke(prospect);

        return prospect;
    }

    // a guide that follows every rule for the given input
    public static GuideContent ValidGuide(GuideInput input)
    {
        return new GuideContent
        {
            Opening = $"Hi {input.FirstName}, Priya from Quillbridge, calling about supplier invoices at {input.Company}.",
            WhyNow = "Bringing another company in usually means two invoice processes to merge.",
            DiscoveryQuestions = ["How do invoices reach your team today?", "Where do approvals get stuck?"],
            CoreMessage = "Quillbridge reads, approves, pays and records supplier invoices.",
            SuccessStory = input.Story,
            MeetingAsk = "Would you be open to 30 minutes with a senior colleague?",
            Pushbacks = input.PushbackKeys.Select(k => new GuidePushback { Key = k, Objection = "An objection.", Answer = "An honest answer." }).ToList(),
            PlanB = new GuidePlanB { Switchboard = "Ask for them.", Voicemail = "Leave a short message.", SendEmail = "Agree and ask one question.", Gatekeeper = "Be honest.", WrongPerson = "Ask who handles invoices." },
            LastTouch = input.LastTouch is null ? "" : "We spoke before.",
            KeyFacts = []
        };
    }
}

public class FakeProspectRepository : IProspectRepository
{
    public List<Prospect> Prospects { get; } = [];

    public Task<Prospect?> GetByProspectIdAsync(string prospectId) => Task.FromResult(Prospects.FirstOrDefault(p => p.ProspectId == prospectId));
    public Task<List<Prospect>> GetAllWithSdrAsync() => Task.FromResult(Prospects.ToList());
    public Task<User?> GetSdrByNameAsync(string fullName) =>
        Task.FromResult(new[] { TestData.Priya, TestData.Jonas, TestData.Luca, TestData.Sam }.FirstOrDefault(u => $"{u.FirstName} {u.LastName}" == fullName));

    public Task<HashSet<string>> GetExistingProspectIdsAsync(IEnumerable<string> prospectIds) => throw new NotImplementedException();
    public Task<List<Prospect>> GetContactsAsync() => throw new NotImplementedException();
    public Task AddRangeAsync(IEnumerable<Prospect> prospects) => throw new NotImplementedException();
    public Task<(List<Prospect> Items, int TotalCount)> GetCallListAsync(Guid sdrId, int skip, int take) => throw new NotImplementedException();
    public Task<(List<Prospect> Items, int TotalCount)> GetDoNotCallListAsync(Guid sdrId, int skip, int take) => throw new NotImplementedException();
    public Task<int> DeleteAllAsync() => throw new NotImplementedException();
}

public class FakeGuideRepository : IGuideRepository
{
    public List<Guide> Guides { get; } = [];

    public Task<decimal> GetTotalSpendAsync() => Task.FromResult(Guides.Sum(g => g.CostUsd));

    public Task<Guide?> FindAsync(Guid prospectId, string inputHash, string promptVersion, string language, GuideSource? source = null) =>
        Task.FromResult(Matching(prospectId, inputHash, promptVersion, language).Where(g => source == null || g.Source == source).MaxBy(g => g.Revision));

    public Task<int> GetMaxRevisionAsync(Guid prospectId, string inputHash, string promptVersion, string language) =>
        Task.FromResult(Matching(prospectId, inputHash, promptVersion, language).Select(g => g.Revision).DefaultIfEmpty(-1).Max());

    public Task<bool> TryAddAsync(Guide guide)
    {
        if (Matching(guide.ProspectId, guide.InputHash, guide.PromptVersion, guide.Language).Any(g => g.Revision == guide.Revision))
        {
            return Task.FromResult(false);
        }

        guide.CreatedAt = DateTime.UtcNow.AddTicks(Guides.Count);
        Guides.Add(guide);

        return Task.FromResult(true);
    }

    public Task<Guide?> GetByIdAsync(Guid guideId) => Task.FromResult(Guides.FirstOrDefault(g => g.Id == guideId));
    public Task UpdateAsync(Guide guide) => Task.CompletedTask;
    public Task<int> CountForProspectAsync(Guid prospectId) => Task.FromResult(Guides.Count(g => g.ProspectId == prospectId));
    public Task<Dictionary<Guid, Guide>> GetLatestPerProspectAsync() =>
        Task.FromResult(Guides.GroupBy(g => g.ProspectId).ToDictionary(g => g.Key, g => g.MaxBy(x => x.CreatedAt)!));
    public Task<List<GuideExample>> GetBestExamplesAsync(string role, Trigger trigger, string industry, int take) => Task.FromResult(new List<GuideExample>());
    public Task<List<GuideFeedbackRow>> GetFeedbackAsync() => Task.FromResult(new List<GuideFeedbackRow>());

    private IEnumerable<Guide> Matching(Guid prospectId, string inputHash, string promptVersion, string language) =>
        Guides.Where(g => g.ProspectId == prospectId && g.InputHash == inputHash && g.PromptVersion == promptVersion && g.Language == language);
}

public class FakeAiWriter : IAiGuideWriter
{
    // what each call returns, in order; the last one repeats
    public List<Func<GuideInput, AiGuideResult>> Responses { get; } = [];
    public int Calls { get; private set; }
    public List<IReadOnlyList<string>> ErrorsSent { get; } = [];
    public bool IsConfigured { get; set; } = true;
    public string Model => "claude-haiku-5-5";

    public Task<AiGuideResult> WriteAsync(GuideInput input, IReadOnlyList<GuideExample> examples, IReadOnlyList<string> previousErrors, CancellationToken cancellationToken = default)
    {
        ErrorsSent.Add(previousErrors.ToList());
        var response = Responses[Math.Min(Calls, Responses.Count - 1)];
        Calls++;

        return Task.FromResult(response(input));
    }

    public static AiGuideResult Valid(GuideInput input) => new(TestData.ValidGuide(input), 3000, 1500, null);
}
