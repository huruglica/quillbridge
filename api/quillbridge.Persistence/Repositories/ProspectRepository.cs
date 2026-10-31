using Microsoft.EntityFrameworkCore;
using quillbridge.Application.BusinessRules;
using quillbridge.Application.Interfaces;
using quillbridge.Domain.Models;
using quillbridge.Persistence.AppDbContext;

namespace quillbridge.Persistence.Repositories;

public class ProspectRepository : IProspectRepository
{
    private readonly QuillbridgeDbContext _context;

    public ProspectRepository(QuillbridgeDbContext context)
    {
        _context = context;
    }

    public async Task<HashSet<string>> GetExistingProspectIdsAsync(IEnumerable<string> prospectIds)
    {
        var ids = prospectIds.ToList();

        var existing = await _context.Prospects
            .Where(p => ids.Contains(p.ProspectId))
            .Select(p => p.ProspectId)
            .ToListAsync();

        return existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<Prospect>> GetContactsAsync()
    {
        return _context.Prospects
            .AsNoTracking()
            .Select(p => new Prospect
            {
                ProspectId = p.ProspectId,
                FirstName = p.FirstName,
                LastName = p.LastName,
                Company = p.Company
            })
            .ToListAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Prospect> prospects)
    {
        await _context.Prospects.AddRangeAsync(prospects);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<Prospect> Items, int TotalCount)> GetCallListAsync(Guid sdrId, int skip, int take)
    {
        var query = _context.Prospects
            .AsNoTracking()
            .Where(p => p.SdrId == sdrId && !p.DoNotCall);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(ProspectPriority.Score)
            .ThenBy(p => p.ProspectId)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<Prospect> Items, int TotalCount)> GetDoNotCallListAsync(Guid sdrId, int skip, int take)
    {
        var query = _context.Prospects
            .AsNoTracking()
            .Where(p => p.SdrId == sdrId && p.DoNotCall);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(p => p.ProspectId)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task<int> DeleteAllAsync()
    {
        return _context.Prospects.ExecuteDeleteAsync();
    }

    public Task<Prospect?> GetByProspectIdAsync(string prospectId)
    {
        return _context.Prospects
            .AsNoTracking()
            .Include(p => p.Sdr)
            .FirstOrDefaultAsync(p => p.ProspectId == prospectId.Trim());
    }

    public Task<List<Prospect>> GetAllWithSdrAsync()
    {
        return _context.Prospects
            .AsNoTracking()
            .Include(p => p.Sdr)
            .ToListAsync();
    }

    public Task<User?> GetSdrByNameAsync(string fullName)
    {
        return _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.FirstName + " " + u.LastName == fullName);
    }
}
