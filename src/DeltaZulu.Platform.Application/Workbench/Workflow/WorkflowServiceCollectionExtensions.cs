using DeltaZulu.Platform.Application.Workbench.Services;
using DeltaZulu.Platform.Application.Workbench.Workflow.Workflows;
using DeltaZulu.Platform.Domain.Workbench.Contracts;
using Elsa.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace DeltaZulu.Platform.Application.Workbench.Workflow;

public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Elsa-backed workflow orchestrator. Adds Elsa 3.x services, registers
    /// the <see cref="ChangeLifecycleWorkflow"/>, and binds
    /// <see cref="IWorkflowOrchestrator"/> to <see cref="ElsaWorkflowOrchestrator"/>.
    /// </summary>
    public static IServiceCollection AddWorkbenchElsaWorkflows(this IServiceCollection services)
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
    public static IServiceCollection AddWorkbenchDomainOrchestrator(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowOrchestrator, DomainDrivenOrchestrator>();
        return services;
    }
}