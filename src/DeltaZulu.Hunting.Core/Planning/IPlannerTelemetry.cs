namespace DeltaZulu.Hunting.Core.Planning;

using DeltaZulu.Hunting.Core.QueryModel;

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}
