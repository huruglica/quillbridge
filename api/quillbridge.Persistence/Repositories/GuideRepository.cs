using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using quillbridge.Application.Guides;
using quillbridge.Application.Interfaces;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;
using quillbridge.Persistence.AppDbContext;

namespace quillbridge.Persistence.Repositories;

public class GuideRepository : IGuideRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly QuillbridgeDbContext _context;

    public GuideRepository(QuillbridgeDbContext context)
    {
        _context = context;
    }

    public async Task<decimal> GetTotalSpendAsync()
    {
        return await _context.Guides.SumAsync(g => (decimal?)g.CostUsd) ?? 0;
    }

    public Task<Guide?> FindAsync(Guid prospectId, string inputHash, string promptVersion, string language, GuideSource? source = null)
    {
        return _context.Guides
            .AsNoTracking()
            .Where(g => g.ProspectId == prospectId && g.InputHash == inputHash && g.PromptVersion == promptVersion && g.Language == language)
            .Where(g => source == null || g.Source == source)
            .OrderByDescending(g => g.Revision)
            .FirstOrDefaultAsync();
    }

    public async Task<int> GetMaxRevisionAsync(Guid prospectId, string inputHash, string promptVersion, string language)
    {
        return await _context.Guides
            .Where(g => g.ProspectId == prospectId && g.InputHash == inputHash && g.PromptVersion == promptVersion && g.Language == language)
            .MaxAsync(g => (int?)g.Revision) ?? -1;
    }

    public async Task<bool> TryAddAsync(Guide guide)
    {
        _context.Guides.Add(guide);

        try
        {
            await _context.SaveChangesAsync();

            return true;
        }
        // 2601/2627: unique index violation, another request stored this key first
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            _context.Entry(guide).State = EntityState.Detached;

            return false;
        }
    }

    public Task<Guide?> GetByIdAsync(Guid guideId)
    {
        return _context.Guides
            .Include(g => g.Prospect)
            .ThenInclude(p => p!.Sdr)
            .FirstOrDefaultAsync(g => g.Id == guideId);
    }

    public Task UpdateAsync(Guide guide)
    {
        return _context.SaveChangesAsync();
    }

    public Task<int> CountForProspectAsync(Guid prospectId)
    {
        return _context.Guides.CountAsync(g => g.ProspectId == prospectId);
    }

    public async Task<Dictionary<Guid, Guide>> GetLatestPerProspectAsync()
    {
        var latest = await _context.Guides
            .AsNoTracking()
            .GroupBy(g => g.ProspectId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
            .ToListAsync();

        return latest.ToDictionary(g => g.ProspectId);
    }

    public async Task<List<GuideExample>> GetBestExamplesAsync(string role, Trigger trigger, string industry, int take)
    {
        var candidates = await _context.Guides
            .AsNoTracking()
            .Where(g => g.Source == GuideSource.Ai && g.Role == role && (g.Outcome == CallOutcome.MeetingBooked || g.Worked == true))
            .Select(g => new { g.Content, g.Outcome, g.FeedbackAt, g.Prospect!.Trigger, g.Prospect.Industry })
            .ToListAsync();

        // same trigger counts most, then same industry, then a booked meeting over a "worked" mark
        return candidates
            .OrderByDescending(c => (c.Trigger == trigger ? 4 : 0) + (c.Industry == industry ? 2 : 0) + (c.Outcome == CallOutcome.MeetingBooked ? 1 : 0))
            .ThenByDescending(c => c.FeedbackAt)
            .Take(take)
            .Select(c =>
            {
                var content = JsonSerializer.Deserialize<GuideContent>(c.Content, Json)!;

                return new GuideExample(role, c.Trigger == Trigger.None ? null : c.Trigger.ToString(), c.Industry, content.Opening, content.WhyNow, content.MeetingAsk);
            })
            .ToList();
    }

    public Task<List<GuideFeedbackRow>> GetFeedbackAsync()
    {
        return _context.Guides
            .AsNoTracking()
            .Where(g => g.FeedbackAt != null)
            .Select(g => new GuideFeedbackRow(g.Role, g.Prospect!.Trigger, g.OpeningAngle, g.Worked, g.Outcome, g.PushbackHeard))
            .ToListAsync();
    }
}
