using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Validation;

public sealed class ProfileValidationPipelineRunner(IEnumerable<IProfileValidationCheck> checks)
{
    public async Task<IReadOnlyList<ProfileValidationOutcome>> RunAllAsync(
        ProfileValidationContext context, CancellationToken ct = default)
    {
        var results = new List<ProfileValidationOutcome>();

        foreach (var check in checks)
        {
            var outcome = await check.RunAsync(context, ct);
            results.Add(outcome);
        }

        return results;
    }

    public static bool HasBlockingFailures(
        IReadOnlyList<ProfileValidationOutcome> outcomes,
        IEnumerable<IProfileValidationCheck> checkDefinitions)
    {
        var blockingChecks = checkDefinitions.Where(c => c.IsBlocking).Select(c => c.Name).ToHashSet();

        return outcomes.Any(o =>
            blockingChecks.Contains(o.CheckName) &&
            o.Findings.Any(f => f.IsBlocking));
    }
}
