using quillbridge.Domain.Common;
using quillbridge.Domain.Enums;

namespace quillbridge.Domain.Models;

// one generated call guide; older rows for the same prospect are kept as history
public class Guide : BaseEntity
{
    public Guid ProspectId { get; set; }
    public Prospect? Prospect { get; set; }
    // SHA-256 of every input that shapes the guide, including the language
    public string InputHash { get; set; } = string.Empty;
    // prompt version for AI guides, template version for template guides
    public string PromptVersion { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    // 0 for the first guide of a hash, +1 for each "Regenerate"
    public int Revision { get; set; }
    public GuideSource Source { get; set; }
    public string? Model { get; set; }
    public string? FallbackReason { get; set; }
    public string OpeningAngle { get; set; } = string.Empty;
    // role detected from the title when the guide was made, for examples and the report
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    // AI usage of the request that produced this row, failed attempts included
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }

    public bool? Worked { get; set; }
    public CallOutcome? Outcome { get; set; }
    public string? PushbackHeard { get; set; }
    public string? FeedbackNote { get; set; }
    public DateTime? FeedbackAt { get; set; }
}
