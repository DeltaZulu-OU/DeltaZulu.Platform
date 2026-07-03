using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Domain.AgentManagement.Contracts;

public sealed record ProfileValidationContext(
    Profiles.ResourceProfileVersion Version);

public sealed record ProfileValidationOutcome(
    string CheckName,
    IReadOnlyList<ValidationFinding> Findings);

public interface IProfileValidationCheck
{
    string Name { get; }

    bool IsBlocking { get; }

    Task<ProfileValidationOutcome> RunAsync(ProfileValidationContext context, CancellationToken ct = default);
}
