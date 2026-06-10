namespace DeltaZulu.Hunting.Core.Planning;

using DeltaZulu.Hunting.Core.QueryModel;

public sealed record NoOpRelationalPlanner(PlannerRunStats? LastRunStats = null) : IRelationalPlanner, IPlannerTelemetry
{
    public PlannerRunStats? LastRunStats { get; private set; } = LastRunStats;

    public RelNode Plan(RelNode root, PlannerContext context)
    {
        LastRunStats = context.Enabled
            ? new PlannerRunStats(1, 0, 0, false, [])
            : null;
        return root;
    }
}