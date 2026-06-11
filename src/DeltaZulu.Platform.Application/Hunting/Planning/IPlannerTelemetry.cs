using DeltaZulu.Platform.Domain.Hunting.Planning;

namespace DeltaZulu.Platform.Application.Hunting.Planning;

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}