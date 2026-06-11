using DeltaZulu.Platform.Application.Governance.Services;
using DeltaZulu.Platform.Application.Governance.Workflow.Workflows;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.Governance.Workflow;

public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Elsa-backed workflow orchestrator. Adds Elsa 3.x services, registers
    /// the <see cref="ChangeLifecycleWorkflow"/>, and binds
    /// <see cref="IWorkflowOrchestrator"/> to <see cref="ElsaWorkflowOrchestrator"/>.
    /// </summary>
    public static IServiceCollection AddGovernanceElsaWorkflows(this IServiceCollection services)
    {
        services.AddElsa(elsa => {
            elsa.UseWorkflowRuntime(runtime => {
                runtime.AddWorkflow<ChangeLifecycleWorkflow>();
            });
            elsa.UseWorkflowManagement();
        });

        services.AddScoped<IWorkflowOrchestrator, ElsaWorkflowOrchestrator>();

        return services;
    }

    /// <summary>
    /// Registers the domain-driven orchestrator (no Elsa dependency). Use this when Elsa
    /// is not desired. The domain aggregate state machine handles all lifecycle transitions.
    /// </summary>
    public static IServiceCollection AddGovernanceDomainOrchestrator(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowOrchestrator, DomainDrivenOrchestrator>();
        return services;
    }
}