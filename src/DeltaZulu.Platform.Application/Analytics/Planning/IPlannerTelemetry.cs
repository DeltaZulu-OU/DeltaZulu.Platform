using DeltaZulu.Platform.Domain.Analytics.Planning;

namespace DeltaZulu.Platform.Application.Analytics.Planning;

public interface IPlannerTelemetry
{
    PlannerRunStats? LastRunStats { get; }
}