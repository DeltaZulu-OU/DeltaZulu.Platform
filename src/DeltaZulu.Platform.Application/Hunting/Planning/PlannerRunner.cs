
using DeltaZulu.Platform.Domain.Hunting.Planning;
using DeltaZulu.Platform.Domain.Hunting.QueryModel;

namespace DeltaZulu.Platform.Application.Hunting.Planning;
internal static class PlannerRunner
{
    internal static PlannerRunResult Run(
        RelNode root,
        PlannerContext context,
        IReadOnlyList<IPlannerPass> passes)
    {
        var current = root;
        var maxIterations = Math.Max(1, context.MaxIterations);
        var ruleBudget = Math.Max(1, context.MaxRuleApplications);

        var attemptedByPass = new int[passes.Count];
        var appliedByPass = new int[passes.Count];

        var totalApplied = 0;
        var hitBudget = false;
        var iterations = 0;

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            iterations++;
            var changedAny = false;

            for (var passIndex = 0; passIndex < passes.Count; passIndex++)
            {
                var pass = passes[passIndex];
                var next = pass.Apply(current, out var changed, out var attempted, out var applied);

                attemptedByPass[passIndex] += attempted;
                appliedByPass[passIndex] += applied;
                totalApplied += applied;

                if (changed)
                {
                    current = next;
                    changedAny = true;
                }

                if (totalApplied >= ruleBudget)
                {
                    hitBudget = true;
                    break;
                }
            }

            if (hitBudget || !changedAny)
            {
                break;
            }
        }

        var passStats = new List<PlannerPassStat>(passes.Count);
        var totalAttempted = 0;

        for (var i = 0; i < passes.Count; i++)
        {
            totalAttempted += attemptedByPass[i];
            passStats.Add(new PlannerPassStat(passes[i].Name, attemptedByPass[i], appliedByPass[i]));
        }

        var stats = new PlannerRunStats(
            Iterations: iterations,
            TotalRulesAttempted: totalAttempted,
            TotalRulesApplied: totalApplied,
            HitRuleApplicationBudget: hitBudget,
            PassStats: passStats);

        return new PlannerRunResult(current, stats);
    }
}

internal sealed record PlannerRunResult(RelNode Node, PlannerRunStats Stats);