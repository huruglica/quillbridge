using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using quillbridge.Application.BusinessRules;
using quillbridge.Application.CustomExceptions;
using quillbridge.Application.DTOs.Guides;
using quillbridge.Application.Guides;
using quillbridge.Application.Interfaces;
using quillbridge.Application.Services.IServices;
using quillbridge.Domain.Enums;

namespace quillbridge.Application.Services;

// generates guides in the background with limited parallelism; the budget limit in GuideService applies to
// every call, so once it is reached the rest of the batch gets template guides
public class GuideBatchService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ClaudeOptions _options;
    private readonly ILogger<GuideBatchService> _logger;
    private readonly object _lock = new();
    private BatchStatusDto _status = new();

    public GuideBatchService(IServiceScopeFactory scopeFactory, IOptions<ClaudeOptions> options, ILogger<GuideBatchService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public BatchStatusDto Status
    {
        get
        {
            lock (_lock)
            {
                return new BatchStatusDto
                {
                    Running = _status.Running, Total = _status.Total, Done = _status.Done, Ai = _status.Ai,
                    Template = _status.Template, Skipped = _status.Skipped, CostUsd = _status.CostUsd,
                    StartedAt = _status.StartedAt, FinishedAt = _status.FinishedAt
                };
            }
        }
    }

    public async Task<BatchStatusDto> StartAsync(BatchRequest request)
    {
        List<string> prospectIds;

        // eligible prospects that have no AI guide yet, highest call priority first
        using (var scope = _scopeFactory.CreateScope())
        {
            var prospects = await scope.ServiceProvider.GetRequiredService<IProspectRepository>().GetAllWithSdrAsync();
            var latest = await scope.ServiceProvider.GetRequiredService<IGuideRepository>().GetLatestPerProspectAsync();

            prospectIds = prospects
                .Where(p => ProspectEligibility.Check(p).Allowed)
                .Where(p => !latest.TryGetValue(p.Id, out var guide) || guide.Source != GuideSource.Ai)
                .OrderByDescending(ProspectPriority.Calculate)
                .ThenBy(p => p.ProspectId)
                .Take(request.Limit ?? int.MaxValue)
                .Select(p => p.ProspectId)
                .ToList();
        }

        lock (_lock)
        {
            if (_status.Running)
            {
                throw new QBBadRequestException("A batch is already running.");
            }

            _status = new BatchStatusDto { Running = true, Total = prospectIds.Count, StartedAt = DateTime.UtcNow };
        }

        _ = Task.Run(() => RunAsync(prospectIds));

        return Status;
    }

    private async Task RunAsync(List<string> prospectIds)
    {
        var parallel = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.BatchParallelism) };

        await Parallel.ForEachAsync(prospectIds, parallel, async (prospectId, _) =>
        {
            using var scope = _scopeFactory.CreateScope();
            var guides = scope.ServiceProvider.GetRequiredService<IGuideService>();

            try
            {
                var guide = await guides.GetGuideAsync(prospectId);

                lock (_lock)
                {
                    _status.Done++;
                    _status.CostUsd += guide.CostUsd;
                    if (guide.Source == GuideSource.Ai)
                    {
                        _status.Ai++;
                    }
                    else
                    {
                        _status.Template++;
                    }
                }
            }
            catch (Exception ex)
            {
                // not eligible (do not call, out of size range...) or an unexpected error; the batch goes on
                _logger.LogInformation("Batch skipped {ProspectId}: {Message}", prospectId, ex.Message);

                lock (_lock)
                {
                    _status.Done++;
                    _status.Skipped++;
                }
            }
        });

        lock (_lock)
        {
            _status.Running = false;
            _status.FinishedAt = DateTime.UtcNow;
        }
    }
}
