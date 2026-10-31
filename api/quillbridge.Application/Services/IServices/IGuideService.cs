using quillbridge.Application.DTOs.Guides;

namespace quillbridge.Application.Services.IServices;

public interface IGuideService
{
    Task<GuideDto> GetGuideAsync(string prospectId, bool regenerate = false, bool useAi = true);
    Task<GuideDto> SaveFeedbackAsync(Guid guideId, GuideFeedbackRequest request);
    Task<GuideReportDto> GetReportAsync();
    Task<BudgetDto> GetBudgetAsync();
    Task<string> ExportMarkdownAsync();
}
