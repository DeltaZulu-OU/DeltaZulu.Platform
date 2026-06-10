using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;

namespace DeltaZulu.Workbench.Workflow.Workflows;

/// <summary>
/// Elsa 3.7 coded workflow modelling the change request lifecycle. Mirrors the GitHub PR
/// workflow pattern: Draft → Checks → Review → (loop back on ChangesRequested) → Merge →
/// Published → Closed.
/// </summary>
public sealed class ChangeLifecycleWorkflow : WorkflowBase
{
    public const string EventContentEdited = "ContentEdited";
    public const string EventChecksCompleted = "ChecksCompleted";
    public const string EventReviewRecorded = "ReviewRecorded";
    public const string EventMergeCompleted = "MergeCompleted";
    public const string EventPublished = "ChangePublished";
    public const string EventClosed = "Closed";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithVariable<string>("ChangeId", "").WithWorkflowStorage();
        builder.WithVariable<string>("WorkflowProfile", "").WithWorkflowStorage();

        builder.Name = "Change Lifecycle";
        builder.Description = "Tracks the PR-like lifecycle of a detection change request.";

        // Implementation cycle: ContentEdited → ChecksCompleted → Review → (loop or merge)
        var implementationCycle = new Sequence
        {
            Activities =
            {
                new Event(EventContentEdited) { Name = "WaitForContentEdit" },
                new WriteLine("Content edited — running checks."),

                new Event(EventChecksCompleted) { Name = "WaitForChecks" },
                new WriteLine("Checks completed."),

                new Event(EventReviewRecorded) { Name = "WaitForReview" },
                new WriteLine("Review recorded."),

                // After review: either merge (approved) or loop (changes requested → re-edit)
                new Fork
                {
                    JoinMode = ForkJoinMode.WaitAny,
                    Branches =
                    {
                        // Direct merge path
                        new Event(EventMergeCompleted) { Name = "WaitForMerge" },

                        // ChangesRequested path: one additional edit → checks → review → merge cycle
                        new Sequence
                        {
                            Activities =
                            {
                                new Event(EventContentEdited) { Name = "WaitForReEdit" },
                                new WriteLine("Content re-edited after review — re-running checks."),

                                new Event(EventChecksCompleted) { Name = "WaitForChecks2" },
                                new WriteLine("Checks completed (cycle 2)."),

                                new Event(EventReviewRecorded) { Name = "WaitForReview2" },
                                new WriteLine("Review recorded (cycle 2)."),

                                new Event(EventMergeCompleted) { Name = "WaitForMerge2" },
                            }
                        },
                    }
                },
            }
        };

        // Post-merge phase: optional Published event, then done
        var postMergePhase = new Fork
        {
            JoinMode = ForkJoinMode.WaitAny,
            Branches =
            {
                new Event(EventPublished) { Name = "WaitForPublished" },
                new WriteLine("Merge complete — lifecycle ends without explicit publication."),
            }
        };

        builder.Root = new Sequence
        {
            Activities =
            {
                new WriteLine("Change lifecycle started."),

                // Outer fork: happy path vs close-at-any-point
                new Fork
                {
                    JoinMode = ForkJoinMode.WaitAny,
                    Branches =
                    {
                        new Sequence
                        {
                            Activities =
                            {
                                implementationCycle,
                                new WriteLine("Change merged."),
                                postMergePhase,
                                new WriteLine("Change lifecycle complete."),
                            }
                        },

                        // Close path: can fire at any point before merge
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