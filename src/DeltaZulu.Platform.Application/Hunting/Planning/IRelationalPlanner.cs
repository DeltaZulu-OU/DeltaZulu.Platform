using DeltaZulu.Platform.Domain.Hunting.Planning;

using DeltaZulu.Platform.Domain.Hunting.QueryModel;

namespace DeltaZulu.Platform.Application.Hunting.Planning;
public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}