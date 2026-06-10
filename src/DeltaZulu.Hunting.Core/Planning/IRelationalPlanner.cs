namespace DeltaZulu.Hunting.Core.Planning;

using DeltaZulu.Hunting.Core.QueryModel;

public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}