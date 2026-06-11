
using DeltaZulu.Platform.Domain.Hunting.QueryModel;

namespace DeltaZulu.Platform.Application.Hunting.Planning;
internal interface IPlannerPass
{
    string Name { get; }

    RelNode Apply(RelNode node, out bool changed, out int attempted, out int applied);
}