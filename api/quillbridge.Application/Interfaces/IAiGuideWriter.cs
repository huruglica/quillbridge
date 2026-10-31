using quillbridge.Application.Guides;

namespace quillbridge.Application.Interfaces;

public record AiGuideResult(GuideContent? Content, long InputTokens, long OutputTokens, string? Error);

public interface IAiGuideWriter
{
    // false when neither Claude:ApiKey nor ANTHROPIC_API_KEY is set; the app then runs on templates only
    bool IsConfigured { get; }
    string Model { get; }
    Task<AiGuideResult> WriteAsync(GuideInput input, IReadOnlyList<GuideExample> examples, IReadOnlyList<string> previousErrors, CancellationToken cancellationToken = default);
}
