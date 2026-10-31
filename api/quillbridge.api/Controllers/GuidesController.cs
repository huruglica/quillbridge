using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using quillbridge.api.Controllers.BaseController;
using quillbridge.Application.DTOs.Guides;
using quillbridge.Application.Services;
using quillbridge.Application.Services.IServices;
using quillbridge.Domain.Constants;

namespace quillbridge.api.Controllers;

[Authorize(Roles = Roles.Sdr)]
public class GuidesController : BaseApiController
{
    private readonly IGuideService _guideService;
    private readonly GuideBatchService _batchService;

    public GuidesController(IGuideService guideService, GuideBatchService batchService)
    {
        _guideService = guideService;
        _batchService = batchService;
    }

    // the latest valid guide: stored AI guide, else a new AI guide, else a template.
    // useAi=false skips the AI and returns the template, for testing
    [HttpGet("prospect/{prospectId}")]
    public async Task<IActionResult> GetGuide(string prospectId, [FromQuery] bool useAi = true)
    {
        var response = await _guideService.GetGuideAsync(prospectId, useAi: useAi);

        return ReturnToBaseResponse(response);
    }

    [HttpPost("prospect/{prospectId}/regenerate")]
    public async Task<IActionResult> Regenerate(string prospectId, [FromQuery] bool useAi = true)
    {
        var response = await _guideService.GetGuideAsync(prospectId, regenerate: true, useAi: useAi);

        return ReturnToBaseResponse(response);
    }

    [HttpPut("{guideId:guid}/feedback")]
    public async Task<IActionResult> SaveFeedback(Guid guideId, [FromBody] GuideFeedbackRequest request)
    {
        var response = await _guideService.SaveFeedbackAsync(guideId, request);

        return ReturnToBaseResponse(response);
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetReport()
    {
        var response = await _guideService.GetReportAsync();

        return ReturnToBaseResponse(response);
    }

    [HttpGet("budget")]
    public async Task<IActionResult> GetBudget()
    {
        var response = await _guideService.GetBudgetAsync();

        return ReturnToBaseResponse(response);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> StartBatch([FromBody] BatchRequest request)
    {
        var response = await _batchService.StartAsync(request);

        return ReturnToBaseResponse(response);
    }

    [HttpGet("batch")]
    public IActionResult GetBatchStatus()
    {
        return ReturnToBaseResponse(_batchService.Status);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var markdown = await _guideService.ExportMarkdownAsync();

        return File(Encoding.UTF8.GetBytes(markdown), "text/markdown", "quillbridge-guides.md");
    }
}
