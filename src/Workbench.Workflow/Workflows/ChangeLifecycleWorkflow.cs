using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;

namespace Workbench.Workflow.Workflows;

/// <summary>
/// Elsa 3.7 coded workflow modelling the change request lifecycle. Extends <see cref="WorkflowBase"/>
/// (synchronous Build override) rather than implementing <c>IWorkflow</c> directly, which
/// changed to <c>BuildAsync</c> in Elsa 3.7.
/// </summary>
public sealed class ChangeLifecycleWorkflow : WorkflowBase
{
    // Event names used as bookmark stimuli. Must match the names used in ElsaWorkflowOrchestrator.
    public const string EventContentEdited = "ContentEdited";

    public const string EventChecksCompleted = "ChecksCompleted";
    public const string EventReviewRecorded = "ReviewRecorded";
    public const string EventMergeCompleted = "MergeCompleted";
    public const string EventClosed = "Closed";

    protected override void Build(IWorkflowBuilder builder)
    {
        // Input: the change request ID and workflow profile are passed at workflow start.
        var changeIdVar = builder.WithVariable<string>("ChangeId", "").WithWorkflowStorage();
        var profileVar = builder.WithVariable<string>("WorkflowProfile", "").WithWorkflowStorage();

        builder.Name = "Change Lifecycle";
        builder.Description = "Tracks the PR-like lifecycle of a detection change request.";

        // LIMITATION (P2-6): This workflow is strictly sequential and does not model
        // re-entry loops (edit → check → review → changes requested → edit → ...).
        // The domain state machine handles loops correctly; this Elsa workflow is a
        // supplementary tracker that mirrors the first happy-path traversal only.
        // To model loops, replace the linear Sequence with a While/Loop activity
        // that re-enters the edit→check→review cycle on ChangesRequested events.

        builder.Root = new Sequence
        {
            Activities =
            {
                // 1. Log start.
                new WriteLine("Change lifecycle started."),

                // 2. Wait for content edit or close.
                new Fork
                {
                    JoinMode = ForkJoinMode.WaitAny,
                    Branches =
                    {
                        // Happy path: content edited → checks → review → merge.
                        new Sequence
                        {
                            Activities =
                            {
                                new Event(EventContentEdited) { Name = "WaitForContentEdit" },
                                new WriteLine("Content edited — running checks."),

                                new Event(EventChecksCompleted) { Name = "WaitForChecks" },
                                new WriteLine("Checks completed."),

                                new Event(EventReviewRecorded) { Name = "WaitForReview" },
                                new WriteLine("Review recorded."),

                                new Event(EventMergeCompleted) { Name = "WaitForMerge" },
                                new WriteLine("Merge requested — lifecycle complete."),
                            }
                        },

                        // Close path: can happen at any point.
                        new Sequence
                        {
                            Activities =
                            {
                                new Event(EventClosed) { Name = "WaitForClose" },
                                new WriteLine("Change closed without merge."),
                            }
                        },
                    }
                },
            }
        };
    }
}