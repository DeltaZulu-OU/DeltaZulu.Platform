using DeltaZulu.Kql.Relational;

namespace DeltaZulu.Platform.Application.Analytics.Planning;

internal interface IPlannerPass
{
    string Name { get; }

    RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied);
}