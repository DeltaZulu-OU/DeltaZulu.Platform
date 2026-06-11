namespace DeltaZulu.Platform.Application.Hunting.Planning;

using DeltaZulu.Platform.Domain.Hunting.QueryModel;

internal interface IPlannerPass
{
    string Name { get; }

    RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied);
}