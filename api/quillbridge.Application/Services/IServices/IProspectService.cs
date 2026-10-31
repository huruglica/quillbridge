using quillbridge.Application.DTOs.Common;
using quillbridge.Application.DTOs.Prospects;

namespace quillbridge.Application.Services.IServices;

public interface IProspectService
{
    Task<ProspectImportResult> ImportAsync(ImportProspectsRequest request);
    Task<DeleteProspectsResult> DeleteAllAsync();
    Task<PagedResult<CallListItemDto>> GetCallListAsync(Guid sdrId, PagedQueryParameters query);
    Task<PagedResult<DoNotCallItemDto>> GetDoNotCallListAsync(Guid sdrId, PagedQueryParameters query);
}
