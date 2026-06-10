namespace DeltaZulu.Hunting.Core.Planning;

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}