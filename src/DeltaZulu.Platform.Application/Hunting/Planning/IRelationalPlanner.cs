namespace DeltaZulu.Platform.Domain.Hunting.Planning;

using DeltaZulu.Platform.Domain.Hunting.QueryModel;

public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}