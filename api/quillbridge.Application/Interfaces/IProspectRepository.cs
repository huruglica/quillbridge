using quillbridge.Domain.Models;

namespace quillbridge.Application.Interfaces;

public interface IProspectRepository
{
    Task<HashSet<string>> GetExistingProspectIdsAsync(IEnumerable<string> prospectIds);
    // only ProspectId, FirstName, LastName and Company are loaded
    Task<List<Prospect>> GetContactsAsync();
    Task AddRangeAsync(IEnumerable<Prospect> prospects);
    // highest priority first, do-not-call prospects excluded
    Task<(List<Prospect> Items, int TotalCount)> GetCallListAsync(Guid sdrId, int skip, int take);
    // ordered by prospect ID
    Task<(List<Prospect> Items, int TotalCount)> GetDoNotCallListAsync(Guid sdrId, int skip, int take);
    Task<int> DeleteAllAsync();
    // with the assigned SDR loaded
    Task<Prospect?> GetByProspectIdAsync(string prospectId);
    Task<List<Prospect>> GetAllWithSdrAsync();
    Task<User?> GetSdrByNameAsync(string fullName);
}
