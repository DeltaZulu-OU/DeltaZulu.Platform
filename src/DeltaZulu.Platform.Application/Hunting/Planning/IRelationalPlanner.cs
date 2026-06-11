using DeltaZulu.Platform.Domain.Hunting.Planning;

namespace DeltaZulu.Platform.Application.Hunting.Planning;

using DeltaZulu.Platform.Domain.Hunting.QueryModel;

public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}