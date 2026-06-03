namespace Hunting.Core.Planning;

using Hunting.Core.QueryModel;

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}
