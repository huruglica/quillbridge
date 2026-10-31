using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using quillbridge.Application.BusinessRules;
using quillbridge.Application.CustomExceptions;
using quillbridge.Application.DTOs.Guides;
using quillbridge.Application.Guides;
using quillbridge.Application.Interfaces;
using quillbridge.Application.Services.IServices;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Application.Services;

// every guide request goes: stored AI guide -> AI -> rule-based template
public class GuideService : IGuideService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IProspectRepository _prospectRepository;
    private readonly IGuideRepository _guideRepository;
    private readonly IAiGuideWriter _aiWriter;
    private readonly BudgetGuard _budget;
    private readonly ClaudeOptions _options;

    public GuideService(
        IProspectRepository prospectRepository,
        IGuideRepository guideRepository,
        IAiGuideWriter aiWriter,
        BudgetGuard budget,
        IOptions<ClaudeOptions> options)
    {
        _prospectRepository = prospectRepository;
        _guideRepository = guideRepository;
        _aiWriter = aiWriter;
        _budget = budget;
        _options = options.Value;
    }

    public async Task<GuideDto> GetGuideAsync(string prospectId, bool regenerate = false, bool useAi = true)
    {
        var prospect = await _prospectRepository.GetByProspectIdAsync(prospectId)
            ?? throw new QBNotFoundException($"Prospect {prospectId} was not found.");

        var eligibility = ProspectEligibility.Check(prospect);

        if (!eligibility.Allowed)
        {
            throw new QBBadRequestException($"No guide for {prospectId}. {eligibility.SkipReason}");
        }

        var sdr = prospect.Sdr ?? (eligibility.RegionSdrName is null ? null : await _prospectRepository.GetSdrByNameAsync(eligibility.RegionSdrName));
        var plan = GuidePlanner.Plan(prospect, sdr, eligibility);
        var input = plan.Input;

        if (!regenerate && useAi)
        {
            var cached = await _guideRepository.FindAsync(prospect.Id, plan.InputHash, GuidePrompt.Version, input.Language, GuideSource.Ai);

            if (cached is not null)
            {
                return await ToDtoAsync(cached, prospect, sdr, plan);
            }
        }

        var (content, usage, fallbackReason) = await TryAiAsync(input, eligibility.Role, useAi);
        var cost = _options.Cost(usage.Input, usage.Output);

        if (content is not null)
        {
            var guide = await StoreAsync(NewGuide(prospect, plan, eligibility, GuidePrompt.Version, input.Language, GuideSource.Ai, content, usage, cost, null), regenerate);

            return await ToDtoAsync(guide, prospect, sdr, plan);
        }

        // a stored template is reused only if this request spent nothing; it is never a final answer,
        // so the next request tries the AI again
        var templateLanguage = TemplateGuideWriter.LanguageFor(input);
        var existingTemplate = await _guideRepository.FindAsync(prospect.Id, plan.InputHash, TemplateGuideWriter.Version, templateLanguage, GuideSource.Template);

        if (existingTemplate is not null && cost == 0)
        {
            existingTemplate.FallbackReason = fallbackReason;

            return await ToDtoAsync(existingTemplate, prospect, sdr, plan);
        }

        var template = NewGuide(prospect, plan, eligibility, TemplateGuideWriter.Version, templateLanguage, GuideSource.Template,
            TemplateGuideWriter.Write(input), usage, cost, fallbackReason);

        return await ToDtoAsync(await StoreAsync(template, regenerate: true), prospect, sdr, plan);
    }

    private async Task<(GuideContent? Content, (long Input, long Output) Usage, string Reason)> TryAiAsync(GuideInput input, ProspectRole role, bool useAi)
    {
        if (!useAi)
        {
            return (null, (0, 0), "AI was turned off for this request (useAi=false).");
        }

        if (!_aiWriter.IsConfigured)
        {
            return (null, (0, 0), "AI is off: no Claude API key is set (Claude:ApiKey or ANTHROPIC_API_KEY).");
        }

        await EnsureBudgetLoadedAsync();

        var examples = await _guideRepository.GetBestExamplesAsync(role.ToString(), Enum.TryParse<Trigger>(input.Trigger, out var trigger) ? trigger : Trigger.None, input.Industry, 2);
        var errors = new List<string>();
        long inputTokens = 0, outputTokens = 0;

        // one attempt plus one retry that is told what was wrong
        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (!_budget.TryReserve(_options.MaxCostPerCall, _options.BudgetUsd))
            {
                return (null, (inputTokens, outputTokens), $"AI budget of ${_options.BudgetUsd} is used up (${_budget.Spent:0.####} spent).");
            }

            var result = await _aiWriter.WriteAsync(input, examples, errors);

            inputTokens += result.InputTokens;
            outputTokens += result.OutputTokens;
            _budget.Settle(_options.MaxCostPerCall, _options.Cost(result.InputTokens, result.OutputTokens));

            if (result.Content is null)
            {
                return (null, (inputTokens, outputTokens), $"AI failed: {result.Error}");
            }

            errors = GuideValidator.Validate(result.Content, input);

            if (errors.Count == 0)
            {
                return (result.Content, (inputTokens, outputTokens), string.Empty);
            }
        }

        return (null, (inputTokens, outputTokens), $"AI guide failed validation twice: {string.Join(" ", errors.Take(3))}");
    }

    private async Task EnsureBudgetLoadedAsync()
    {
        if (!_budget.IsLoaded)
        {
            _budget.Load(await _guideRepository.GetTotalSpendAsync());
        }
    }

    // each new guide takes the next revision of its key. If a parallel request stored that revision first,
    // a normal request uses theirs (no duplicate); only "Regenerate" tries the next revision.
    private async Task<Guide> StoreAsync(Guide guide, bool regenerate)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            guide.Revision = await _guideRepository.GetMaxRevisionAsync(guide.ProspectId, guide.InputHash, guide.PromptVersion, guide.Language) + 1;

            if (await _guideRepository.TryAddAsync(guide))
            {
                return guide;
            }

            if (!regenerate)
            {
                break;
            }
        }

        return await _guideRepository.FindAsync(guide.ProspectId, guide.InputHash, guide.PromptVersion, guide.Language) ?? guide;
    }

    private Guide NewGuide(Prospect prospect, GuidePlan plan, GuideEligibility eligibility, string version, string language,
        GuideSource source, GuideContent content, (long Input, long Output) usage, decimal cost, string? fallbackReason)
    {
        return new Guide
        {
            Id = Guid.NewGuid(),
            ProspectId = prospect.Id,
            InputHash = plan.InputHash,
            PromptVersion = version,
            Language = language,
            Source = source,
            Model = usage.Input > 0 ? _aiWriter.Model : null,
            FallbackReason = fallbackReason,
            OpeningAngle = plan.Input.OpeningAngle,
            Role = eligibility.Role.ToString(),
            Content = JsonSerializer.Serialize(content, Json),
            InputTokens = (int)usage.Input,
            OutputTokens = (int)usage.Output,
            CostUsd = cost
        };
    }

    public async Task<GuideDto> SaveFeedbackAsync(Guid guideId, GuideFeedbackRequest request)
    {
        var guide = await _guideRepository.GetByIdAsync(guideId)
            ?? throw new QBNotFoundException("Guide not found.");

        if (request.PushbackHeard is not null && SalesFacts.Pushbacks.All(p => p.Key != request.PushbackHeard))
        {
            throw new QBBadRequestException($"Unknown pushback '{request.PushbackHeard}'.");
        }

        guide.Worked = request.Worked;
        guide.Outcome = request.Outcome;
        guide.PushbackHeard = request.PushbackHeard;
        guide.FeedbackNote = request.Note?.Trim();
        guide.FeedbackAt = DateTime.UtcNow;

        await _guideRepository.UpdateAsync(guide);

        var prospect = guide.Prospect!;

        return await GetGuideByRowAsync(guide, prospect);
    }

    private async Task<GuideDto> GetGuideByRowAsync(Guide guide, Prospect prospect)
    {
        var eligibility = ProspectEligibility.Check(prospect);
        var sdr = prospect.Sdr ?? (eligibility.RegionSdrName is null ? null : await _prospectRepository.GetSdrByNameAsync(eligibility.RegionSdrName));

        return await ToDtoAsync(guide, prospect, sdr, GuidePlanner.Plan(prospect, sdr, eligibility));
    }

    public async Task<GuideReportDto> GetReportAsync()
    {
        var rows = await _guideRepository.GetFeedbackAsync();

        return new GuideReportDto
        {
            GuidesWithFeedback = rows.Count,
            MostCommonPushbacks = rows
                .Where(r => r.PushbackHeard is not null)
                .GroupBy(r => r.PushbackHeard!)
                .Select(g => new KeyValuePair<string, int>(g.Key, g.Count()))
                .OrderByDescending(p => p.Value)
                .ToList(),
            ByOpening = Rates(rows, r => r.OpeningAngle),
            ByRole = Rates(rows, r => r.Role),
            ByTrigger = Rates(rows, r => r.Trigger == Trigger.None ? "No trigger" : r.Trigger.ToString())
        };
    }

    private static List<OutcomeRateDto> Rates(List<GuideFeedbackRow> rows, Func<GuideFeedbackRow, string> group)
    {
        return rows
            .GroupBy(group)
            .Select(g => new OutcomeRateDto
            {
                Group = g.Key,
                Calls = g.Count(r => r.Outcome is not null),
                MeetingsBooked = g.Count(r => r.Outcome == CallOutcome.MeetingBooked),
                MeetingRate = g.Count(r => r.Outcome is not null) == 0
                    ? 0
                    : Math.Round((double)g.Count(r => r.Outcome == CallOutcome.MeetingBooked) / g.Count(r => r.Outcome is not null), 3),
                MarkedWorked = g.Count(r => r.Worked == true)
            })
            .OrderByDescending(r => r.MeetingRate)
            .ThenByDescending(r => r.Calls)
            .ToList();
    }

    public async Task<BudgetDto> GetBudgetAsync()
    {
        await EnsureBudgetLoadedAsync();

        return new BudgetDto
        {
            AiConfigured = _aiWriter.IsConfigured,
            Model = _aiWriter.Model,
            SpentUsd = Math.Round(_budget.Spent, 4),
            LimitUsd = _options.BudgetUsd,
            Exhausted = _budget.Spent + _options.MaxCostPerCall > _options.BudgetUsd
        };
    }

    // the latest guide of every prospect, as Markdown, for handing over or printing
    public async Task<string> ExportMarkdownAsync()
    {
        var latest = await _guideRepository.GetLatestPerProspectAsync();
        var prospects = await _prospectRepository.GetAllWithSdrAsync();
        var markdown = new StringBuilder("# Quillbridge call guides\n\n");

        foreach (var prospect in prospects.Where(p => latest.ContainsKey(p.Id)).OrderBy(p => p.ProspectId))
        {
            var guide = latest[prospect.Id];
            var content = JsonSerializer.Deserialize<GuideContent>(guide.Content, Json)!;

            markdown.AppendLine($"## {prospect.ProspectId} {prospect.FirstName} {prospect.LastName}, {prospect.Title} at {prospect.Company}");
            markdown.AppendLine($"_{guide.Source} guide, {guide.Language}, {guide.CreatedAt:yyyy-MM-dd HH:mm} UTC_\n");
            markdown.AppendLine($"**Opening:** {content.Opening}\n");
            markdown.AppendLine($"**Why now:** {content.WhyNow}\n");
            markdown.AppendLine("**Questions:**");
            content.DiscoveryQuestions.ForEach(q => markdown.AppendLine($"- {q}"));
            markdown.AppendLine($"\n**Message:** {content.CoreMessage}\n");
            markdown.AppendLine($"**Story:** {content.SuccessStory}\n");
            markdown.AppendLine($"**Ask:** {content.MeetingAsk}\n");
            markdown.AppendLine("**Pushbacks:**");
            content.Pushbacks.ForEach(p => markdown.AppendLine($"- \"{p.Objection}\": {p.Answer}"));
            markdown.AppendLine("\n**Plan B:**");
            markdown.AppendLine($"- Switchboard: {content.PlanB.Switchboard}");
            markdown.AppendLine($"- Voicemail: {content.PlanB.Voicemail}");
            markdown.AppendLine($"- \"Send an email\": {content.PlanB.SendEmail}");
            markdown.AppendLine($"- Gatekeeper: {content.PlanB.Gatekeeper}");
            markdown.AppendLine($"- Wrong person: {content.PlanB.WrongPerson}");

            if (content.LastTouch.Length > 0)
            {
                markdown.AppendLine($"\n**Last touch:** {content.LastTouch}");
            }

            content.KeyFacts.ForEach(f => markdown.AppendLine($"\n- {f}"));
            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private async Task<GuideDto> ToDtoAsync(Guide guide, Prospect prospect, User? sdr, GuidePlan plan)
    {
        return new GuideDto
        {
            GuideId = guide.Id,
            ProspectId = prospect.ProspectId,
            ProspectName = $"{prospect.FirstName} {prospect.LastName}",
            Company = plan.Input.Company,
            Title = prospect.Title,
            Role = guide.Role,
            SdrName = sdr is null ? null : $"{sdr.FirstName} {sdr.LastName}",
            Source = guide.Source,
            FallbackReason = guide.FallbackReason,
            Language = guide.Language,
            OpeningAngle = guide.OpeningAngle,
            StoryKey = plan.Input.StoryKey,
            Flags = plan.Flags,
            Content = JsonSerializer.Deserialize<GuideContent>(guide.Content, Json)!,
            CreatedAt = guide.CreatedAt,
            InputTokens = guide.InputTokens,
            OutputTokens = guide.OutputTokens,
            CostUsd = guide.CostUsd,
            HistoryCount = await _guideRepository.CountForProspectAsync(prospect.Id),
            Worked = guide.Worked,
            Outcome = guide.Outcome,
            PushbackHeard = guide.PushbackHeard,
            FeedbackNote = guide.FeedbackNote
        };
    }
}
