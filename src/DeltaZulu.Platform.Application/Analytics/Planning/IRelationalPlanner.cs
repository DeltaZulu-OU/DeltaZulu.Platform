using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Application.Analytics.Planning;

public interface IRelationalPlanner
{
    RelNode Plan(RelNode root, PlannerContext context);
}