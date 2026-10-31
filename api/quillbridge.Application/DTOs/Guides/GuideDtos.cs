using quillbridge.Application.Guides;
using quillbridge.Domain.Enums;

namespace quillbridge.Application.DTOs.Guides;

public class GuideDto
{
    public Guid GuideId { get; set; }
    public string ProspectId { get; set; } = string.Empty;
    public string ProspectName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Role { get; set; } = string.Empty;
    public string? SdrName { get; set; }
    public GuideSource Source { get; set; }
    // set when the guide is a template, explaining why the AI was not used
    public string? FallbackReason { get; set; }
    public string Language { get; set; } = string.Empty;
    public string OpeningAngle { get; set; } = string.Empty;
    public string StoryKey { get; set; } = string.Empty;
    public List<string> Flags { get; set; } = [];
    public GuideContent Content { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
    public int HistoryCount { get; set; }
    public bool? Worked { get; set; }
    public CallOutcome? Outcome { get; set; }
    public string? PushbackHeard { get; set; }
    public string? FeedbackNote { get; set; }
}

public class GuideFeedbackRequest
{
    public bool? Worked { get; set; }
    public CallOutcome? Outcome { get; set; }
    // one of the pushback keys, e.g. "no_budget"
    public string? PushbackHeard { get; set; }
    public string? Note { get; set; }
}

public class BudgetDto
{
    public bool AiConfigured { get; set; }
    public string Model { get; set; } = string.Empty;
    public decimal SpentUsd { get; set; }
    public decimal LimitUsd { get; set; }
    public bool Exhausted { get; set; }
}

public class OutcomeRateDto
{
    public string Group { get; set; } = string.Empty;
    public int Calls { get; set; }
    public int MeetingsBooked { get; set; }
    public double MeetingRate { get; set; }
    public int MarkedWorked { get; set; }
}

public class GuideReportDto
{
    public int GuidesWithFeedback { get; set; }
    public List<KeyValuePair<string, int>> MostCommonPushbacks { get; set; } = [];
    public List<OutcomeRateDto> ByOpening { get; set; } = [];
    public List<OutcomeRateDto> ByRole { get; set; } = [];
    public List<OutcomeRateDto> ByTrigger { get; set; } = [];
}

public class BatchRequest
{
    // only prospects without an AI guide yet, highest priority first; null = all of them
    public int? Limit { get; set; }
}

public class BatchStatusDto
{
    public bool Running { get; set; }
    public int Total { get; set; }
    public int Done { get; set; }
    public int Ai { get; set; }
    public int Template { get; set; }
    public int Skipped { get; set; }
    public decimal CostUsd { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}
