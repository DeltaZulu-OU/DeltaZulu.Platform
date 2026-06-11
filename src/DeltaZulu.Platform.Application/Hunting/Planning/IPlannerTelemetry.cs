namespace DeltaZulu.Platform.Domain.Hunting.Planning;

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}