using quillbridge.Application.BusinessRules;
using quillbridge.Domain.Enums;
using quillbridge.Domain.Models;

namespace quillbridge.Tests;

public class ProspectPriorityTests
{
    [Theory]
    [InlineData(Trigger.NewSystem, FitTier.A, 66, 7 + 3 + 6.6)]
    [InlineData(Trigger.Expansion, FitTier.C, 40, 1 + 1 + 4.0)]
    [InlineData(Trigger.Audit, FitTier.B, 50, 3 + 2 + 5.0)]
    [InlineData(Trigger.None, FitTier.None, 10, 1.0)]
    [InlineData(Trigger.NewLeader, FitTier.A, null, 6 + 3.0)]
    public void Calculate_AddsTriggerTierAndInterestPoints(Trigger trigger, FitTier tier, int? interest, double expected)
    {
        var prospect = new Prospect { Trigger = trigger, FitTier = tier, InterestScore = interest };

        Assert.Equal(expected, ProspectPriority.Calculate(prospect), 10);
    }

    [Fact]
    public void TriggerPoints_FollowTheOrderInTheBrief()
    {
        Trigger[] briefOrder =
        [
            Trigger.NewSystem, Trigger.NewLeader, Trigger.Funding, Trigger.Hiring,
            Trigger.Audit, Trigger.Acquisition, Trigger.Expansion, Trigger.None
        ];

        var points = briefOrder.Select(t => (int)t).ToList();

        Assert.Equal(points.OrderByDescending(p => p), points);
    }
}
