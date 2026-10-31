using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using quillbridge.Application.BusinessRules;
using quillbridge.Application.CustomExceptions;
using quillbridge.Application.DTOs.Common;
using quillbridge.Application.DTOs.Prospects;
using quillbridge.Application.Import;
using quillbridge.Application.Interfaces;
using quillbridge.Application.Services.IServices;
using quillbridge.Application.Wrappers;
using quillbridge.Domain.Constants;
using quillbridge.Domain.Models;

namespace quillbridge.Application.Services;

public class ProspectService : IProspectService
{
    private readonly IProspectRepository _prospectRepository;
    private readonly UserManager<User> _userManager;
    private readonly TimeProvider _timeProvider;
    private readonly IValidator<ImportProspectsRequest> _importValidator;

    public ProspectService(
        IProspectRepository prospectRepository,
        UserManager<User> userManager,
        TimeProvider timeProvider,
        IValidator<ImportProspectsRequest> importValidator)
    {
        _prospectRepository = prospectRepository;
        _userManager = userManager;
        _timeProvider = timeProvider;
        _importValidator = importValidator;
    }

    public async Task<ProspectImportResult> ImportAsync(ImportProspectsRequest request)
    {
        var validation = await _importValidator.ValidateAsync(request);

        if (!validation.IsValid)
        {
            var errors = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));

            throw new QBBadRequestException(errors, ErrorCodes.ValidationFailed);
        }

        List<(int RowNumber, ProspectCsvRow Row)> rows;

        await using (var csv = request.File!.OpenReadStream())
        {
            rows = ReadRows(csv);
        }

        var result = new ProspectImportResult { TotalRows = rows.Count };

        var sdrIdsByName = await GetSdrIdsByNameAsync();
        var existingIds = await _prospectRepository.GetExistingProspectIdsAsync(
            rows.Select(r => r.Row.ProspectId.Trim()).Where(id => id.Length > 0));
        var today = _timeProvider.GetUtcNow().UtcDateTime.Date;

        var existingContacts = (await _prospectRepository.GetContactsAsync())
            .GroupBy(c => ProspectCsvMapper.ContactKey(c.FirstName, c.LastName, c.Company))
            .ToDictionary(g => g.Key, g => g.First().ProspectId);

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenContacts = new Dictionary<string, Prospect>();
        var prospects = new List<Prospect>();

        foreach (var (rowNumber, row) in rows)
        {
            var prospectId = row.ProspectId.Trim();

            if (existingIds.Contains(prospectId))
            {
                result.SkippedRows.Add(Issue(rowNumber, prospectId, "A prospect with this ID already exists."));
                continue;
            }

            if (prospectId.Length > 0 && !seenIds.Add(prospectId))
            {
                result.SkippedRows.Add(Issue(rowNumber, prospectId, "Duplicate prospect ID in the file, only the first row was imported."));
                continue;
            }

            var mapped = ProspectCsvMapper.Map(row, sdrIdsByName, today);

            if (mapped.Prospect is null)
            {
                result.SkippedRows.Add(Issue(rowNumber, prospectId, mapped.SkipReason!));
                continue;
            }

            var contactKey = ProspectCsvMapper.ContactKey(mapped.Prospect.FirstName, mapped.Prospect.LastName, mapped.Prospect.Company);

            if (existingContacts.TryGetValue(contactKey, out var existingProspectId))
            {
                result.SkippedRows.Add(Issue(rowNumber, prospectId, $"Same person and company as {existingProspectId}, which is already imported."));
                continue;
            }

            if (seenContacts.TryGetValue(contactKey, out var kept))
            {
                result.SkippedRows.Add(Issue(rowNumber, prospectId, $"Same person and company as {kept.ProspectId}, only the first row was imported."));
                CarryOverDoNotCall(kept, mapped.Prospect);
                continue;
            }

            seenContacts[contactKey] = mapped.Prospect;
            result.Warnings.AddRange(mapped.Warnings.Select(w => Issue(rowNumber, prospectId, w)));
            prospects.Add(mapped.Prospect);
        }

        if (prospects.Count > 0)
        {
            await _prospectRepository.AddRangeAsync(prospects);
        }

        result.Imported = prospects.Count;

        return result;
    }

    public async Task<DeleteProspectsResult> DeleteAllAsync()
    {
        var deleted = await _prospectRepository.DeleteAllAsync();

        return new DeleteProspectsResult { Deleted = deleted };
    }

    public async Task<PagedResult<CallListItemDto>> GetCallListAsync(Guid sdrId, PagedQueryParameters query)
    {
        var (items, totalCount) = await _prospectRepository.GetCallListAsync(sdrId, query.Skip, query.PageSize);

        return new PagedResult<CallListItemDto>
        {
            Items = items.Select(ToCallListItem).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<DoNotCallItemDto>> GetDoNotCallListAsync(Guid sdrId, PagedQueryParameters query)
    {
        var (items, totalCount) = await _prospectRepository.GetDoNotCallListAsync(sdrId, query.Skip, query.PageSize);

        return new PagedResult<DoNotCallItemDto>
        {
            Items = items.Select(ToDoNotCallItem).ToList(),
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    private static List<(int RowNumber, ProspectCsvRow Row)> ReadRows(Stream csv)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant()
        };

        try
        {
            using var reader = new StreamReader(csv);
            using var csvReader = new CsvReader(reader, config);

            if (!csvReader.Read() || !csvReader.ReadHeader())
            {
                throw new QBBadRequestException("The CSV file is empty.");
            }

            csvReader.ValidateHeader<ProspectCsvRow>();

            var rows = new List<(int, ProspectCsvRow)>();

            while (csvReader.Read())
            {
                rows.Add((csvReader.Parser.Row, csvReader.GetRecord<ProspectCsvRow>()));
            }

            return rows;
        }
        catch (HeaderValidationException ex)
        {
            var missing = ex.InvalidHeaders.SelectMany(h => h.Names);

            throw new QBBadRequestException($"The CSV file is missing column(s): {string.Join(", ", missing)}.");
        }
        catch (CsvHelperException ex)
        {
            throw new QBBadRequestException($"The CSV file could not be read near row {ex.Context?.Parser?.Row}.");
        }
    }

    private async Task<Dictionary<string, Guid>> GetSdrIdsByNameAsync()
    {
        var sdrs = await _userManager.GetUsersInRoleAsync(Roles.Sdr);

        return sdrs
            .GroupBy(u => $"{u.FirstName} {u.LastName}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    // a dropped duplicate may be the row that says "do not call"; that must not be lost
    private static void CarryOverDoNotCall(Prospect kept, Prospect duplicate)
    {
        if (!duplicate.DoNotCall || kept.DoNotCall)
        {
            return;
        }

        kept.DoNotCall = true;
        kept.DoNotCallReason = $"{duplicate.DoNotCallReason} (from duplicate record {duplicate.ProspectId})";
    }

    private static CallListItemDto ToCallListItem(Prospect prospect)
    {
        return new CallListItemDto
        {
            ProspectId = prospect.ProspectId,
            FirstName = prospect.FirstName,
            LastName = prospect.LastName,
            Title = prospect.Title,
            Company = prospect.Company,
            Industry = prospect.Industry,
            Employees = prospect.Employees,
            Hq = prospect.Hq,
            AccountingSystem = prospect.AccountingSystem,
            FitTier = prospect.FitTier,
            Trigger = prospect.Trigger,
            InterestScore = prospect.InterestScore,
            PhoneType = prospect.PhoneType,
            LastTouch = prospect.LastTouch,
            LastTouchType = prospect.LastTouchType,
            PriorityScore = ProspectPriority.Calculate(prospect)
        };
    }

    private static DoNotCallItemDto ToDoNotCallItem(Prospect prospect)
    {
        return new DoNotCallItemDto
        {
            ProspectId = prospect.ProspectId,
            FirstName = prospect.FirstName,
            LastName = prospect.LastName,
            Title = prospect.Title,
            Company = prospect.Company,
            Hq = prospect.Hq,
            DoNotCallReason = prospect.DoNotCallReason
        };
    }

    private static ProspectImportIssue Issue(int row, string? prospectId, string message)
    {
        return new ProspectImportIssue
        {
            Row = row,
            ProspectId = string.IsNullOrEmpty(prospectId) ? null : prospectId,
            Message = message
        };
    }
}
