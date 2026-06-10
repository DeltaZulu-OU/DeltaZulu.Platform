namespace DeltaZulu.Hunting.Core.Planning;

using DeltaZulu.Hunting.Core.QueryModel;

internal interface IPlannerPass
{
    string Name { get; }

    RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied);
}