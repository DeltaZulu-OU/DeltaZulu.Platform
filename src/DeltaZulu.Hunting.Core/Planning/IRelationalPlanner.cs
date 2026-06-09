namespace Hunting.Core.Planning;

using Hunting.Core.QueryModel;

public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}
