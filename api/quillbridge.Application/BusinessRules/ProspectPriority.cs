using System.Linq.Expressions;
using quillbridge.Domain.Models;

namespace quillbridge.Application.BusinessRules;

// call list order: trigger points (NEW_SYSTEM 7 ... EXPANSION 1, none 0) + fit tier
// points (A 3, B 2, C 1, none 0) + interest score / 10 (missing counts as 0); the enum values are those points.
// the interest score is divided so it weighs about as much as trigger and tier, instead of drowning them out
public static class ProspectPriority
{
    // kept as an expression so the database can sort by it
    public static readonly Expression<Func<Prospect, double>> Score =
        p => (int)p.Trigger + (int)p.FitTier + (p.InterestScore ?? 0) / 10.0;

    private static readonly Func<Prospect, double> CompiledScore = Score.Compile();

    public static double Calculate(Prospect prospect)
    {
        return CompiledScore(prospect);
    }
}
