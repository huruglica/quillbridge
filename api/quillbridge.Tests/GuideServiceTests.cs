using Microsoft.Extensions.Options;
using quillbridge.Application.CustomExceptions;
using quillbridge.Application.Guides;
using quillbridge.Application.Interfaces;
using quillbridge.Application.Services;
using quillbridge.Domain.Enums;
using quillbridge.Tests.Fakes;

namespace quillbridge.Tests;

public class GuideServiceTests
{
    private readonly FakeProspectRepository _prospects = new();
    private readonly FakeGuideRepository _guides = new();
    private readonly FakeAiWriter _ai = new();
    private readonly ClaudeOptions _options = new() { BudgetUsd = 5m };

    public GuideServiceTests()
    {
        _prospects.Prospects.Add(TestData.Prospect());
    }

    private GuideService Service() => new(_prospects, _guides, _ai, new BudgetGuard(), Options.Create(_options));

    [Fact]
    public async Task ValidAiGuide_IsStoredAndReusedWithoutAnotherCall()
    {
        _ai.Responses.Add(FakeAiWriter.Valid);

        var first = await Service().GetGuideAsync("P-1001");
        var second = await Service().GetGuideAsync("P-1001");

        Assert.Equal(GuideSource.Ai, first.Source);
        Assert.Equal(first.GuideId, second.GuideId);
        Assert.Equal(1, _ai.Calls);
        Assert.True(first.CostUsd > 0);
    }

    [Fact]
    public async Task ChangedProspectData_InvalidatesTheStoredGuide()
    {
        _ai.Responses.Add(FakeAiWriter.Valid);
        var first = await Service().GetGuideAsync("P-1001");

        _prospects.Prospects[0].Title = "CFO";
        var second = await Service().GetGuideAsync("P-1001");

        Assert.NotEqual(first.GuideId, second.GuideId);
        Assert.Equal(2, _ai.Calls);
        Assert.Equal(2, second.HistoryCount);
    }

    [Fact]
    public async Task NewPromptVersion_InvalidatesTheStoredGuide()
    {
        _ai.Responses.Add(FakeAiWriter.Valid);
        await Service().GetGuideAsync("P-1001");
        _guides.Guides.ForEach(g => g.PromptVersion = "dcg-v0");

        await Service().GetGuideAsync("P-1001");

        Assert.Equal(2, _ai.Calls);
    }

    [Fact]
    public async Task NoApiKey_GivesTemplateWithoutErrors()
    {
        _ai.IsConfigured = false;

        var guide = await Service().GetGuideAsync("P-1001");

        Assert.Equal(GuideSource.Template, guide.Source);
        Assert.Contains("ANTHROPIC_API_KEY", guide.FallbackReason);
        Assert.Equal(0, _ai.Calls);
        Assert.NotEmpty(guide.Content.Opening);
    }

    [Fact]
    public async Task Template_IsNotAFinalCacheHit_AiIsTriedAgainNextTime()
    {
        _ai.IsConfigured = false;
        await Service().GetGuideAsync("P-1001");

        _ai.IsConfigured = true;
        _ai.Responses.Add(FakeAiWriter.Valid);
        var guide = await Service().GetGuideAsync("P-1001");

        Assert.Equal(GuideSource.Ai, guide.Source);
        Assert.Equal(1, _ai.Calls);
    }

    [Fact]
    public async Task RepeatedTemplateRequestsWithoutAi_DoNotPileUpRows()
    {
        _ai.IsConfigured = false;

        await Service().GetGuideAsync("P-1001");
        await Service().GetGuideAsync("P-1001");

        Assert.Single(_guides.Guides);
    }

    [Fact]
    public async Task InvalidAiGuide_IsRetriedOnceWithTheErrors_ThenFallsBackToTemplate()
    {
        _ai.Responses.Add(input =>
        {
            var guide = TestData.ValidGuide(input);
            guide.CoreMessage = "Only €5 per user per month.";

            return new AiGuideResult(guide, 3000, 1500, null);
        });

        var result = await Service().GetGuideAsync("P-1001");

        Assert.Equal(2, _ai.Calls);
        Assert.Empty(_ai.ErrorsSent[0]);
        Assert.NotEmpty(_ai.ErrorsSent[1]);
        Assert.Equal(GuideSource.Template, result.Source);
        Assert.Contains("validation", result.FallbackReason);
        // both failed calls were paid for and are recorded
        Assert.Equal(_options.Cost(6000, 3000), result.CostUsd);
    }

    [Fact]
    public async Task InvalidThenValid_UsesTheRetry()
    {
        _ai.Responses.Add(input => new AiGuideResult(new GuideContent(), 100, 100, null));
        _ai.Responses.Add(FakeAiWriter.Valid);

        var result = await Service().GetGuideAsync("P-1001");

        Assert.Equal(GuideSource.Ai, result.Source);
        Assert.Equal(2, _ai.Calls);
    }

    [Fact]
    public async Task UseAiFalse_ReturnsTemplateWithoutCallingTheAi()
    {
        _ai.Responses.Add(FakeAiWriter.Valid);
        await Service().GetGuideAsync("P-1001");

        var result = await Service().GetGuideAsync("P-1001", useAi: false);

        Assert.Equal(GuideSource.Template, result.Source);
        Assert.Contains("useAi=false", result.FallbackReason);
        Assert.Equal(0, result.CostUsd);
        Assert.Equal(1, _ai.Calls);
    }

    [Fact]
    public async Task AiError_FallsBackToTemplate()
    {
        _ai.Responses.Add(_ => new AiGuideResult(null, 0, 0, "Claude API timed out or could not be reached."));

        var result = await Service().GetGuideAsync("P-1001");

        Assert.Equal(GuideSource.Template, result.Source);
        Assert.Contains("timed out", result.FallbackReason);
    }

    [Fact]
    public async Task BudgetUsedUp_StopsCallingTheAi()
    {
        _options.BudgetUsd = 0.001m;
        _ai.Responses.Add(FakeAiWriter.Valid);

        var result = await Service().GetGuideAsync("P-1001");

        Assert.Equal(GuideSource.Template, result.Source);
        Assert.Contains("budget", result.FallbackReason);
        Assert.Equal(0, _ai.Calls);
    }

    [Fact]
    public async Task Regenerate_KeepsTheOldGuideAsHistory()
    {
        _ai.Responses.Add(FakeAiWriter.Valid);
        var first = await Service().GetGuideAsync("P-1001");

        var second = await Service().GetGuideAsync("P-1001", regenerate: true);

        Assert.NotEqual(first.GuideId, second.GuideId);
        Assert.Equal(2, _guides.Guides.Count);
        Assert.Equal([0, 1], _guides.Guides.Select(g => g.Revision).Order());
        Assert.Equal(second.GuideId, (await Service().GetGuideAsync("P-1001")).GuideId);
    }

    [Fact]
    public async Task DoNotCall_GetsNoGuideAndNoAiCall()
    {
        _prospects.Prospects[0].DoNotCall = true;
        _prospects.Prospects[0].DoNotCallReason = "Marked do-not-call in the source list.";
        _ai.Responses.Add(FakeAiWriter.Valid);

        await Assert.ThrowsAsync<QBBadRequestException>(() => Service().GetGuideAsync("P-1001"));
        Assert.Equal(0, _ai.Calls);
        Assert.Empty(_guides.Guides);
    }
}
