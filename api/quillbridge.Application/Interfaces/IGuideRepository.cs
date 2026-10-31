using quillbridge.Application.Guides;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Application.Interfaces;

public record GuideFeedbackRow(string Role, Trigger Trigger, string OpeningAngle, bool? Worked, CallOutcome? Outcome, string? PushbackHeard);

public interface IGuideRepository
{
    Task<decimal> GetTotalSpendAsync();
    // highest revision for this exact key, optionally of one source only
    Task<Guide?> FindAsync(Guid prospectId, string inputHash, string promptVersion, string language, GuideSource? source = null);
    Task<int> GetMaxRevisionAsync(Guid prospectId, string inputHash, string promptVersion, string language);
    // false when another request stored the same key first (unique index)
    Task<bool> TryAddAsync(Guide guide);
    Task<Guide?> GetByIdAsync(Guid guideId);
    Task UpdateAsync(Guide guide);
    Task<int> CountForProspectAsync(Guid prospectId);
    // the most recent guide per prospect
    Task<Dictionary<Guid, Guide>> GetLatestPerProspectAsync();
    // AI guides marked as having booked a meeting or worked, closest match first
    Task<List<GuideExample>> GetBestExamplesAsync(string role, Trigger trigger, string industry, int take);
    Task<List<GuideFeedbackRow>> GetFeedbackAsync();
}
