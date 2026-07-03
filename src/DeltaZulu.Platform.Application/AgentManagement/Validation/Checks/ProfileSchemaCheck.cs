using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Validation.Checks;

public sealed class ProfileSchemaCheck : IProfileValidationCheck
{
    public string Name => "profile-schema";
    public bool IsBlocking => true;

    public Task<ProfileValidationOutcome> RunAsync(ProfileValidationContext context, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        var v = context.Version;

        if (string.IsNullOrWhiteSpace(v.SchemaVersion))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceProfileVersion",
                "SchemaVersion", "Schema version is required.", "Set a valid schema version.", true));

        if (string.IsNullOrWhiteSpace(v.ContentHash))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceProfileVersion",
                "ContentHash", "Content hash is required.", "Compute and set the content hash.", true));

        return Task.FromResult(new ProfileValidationOutcome(Name, findings));
    }
}
