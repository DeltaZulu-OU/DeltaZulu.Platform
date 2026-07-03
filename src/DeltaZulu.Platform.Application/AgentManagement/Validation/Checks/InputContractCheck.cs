using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Validation.Checks;

public sealed class InputContractCheck : IProfileValidationCheck
{
    public string Name => "input-contract";
    public bool IsBlocking => true;

    public Task<ProfileValidationOutcome> RunAsync(ProfileValidationContext context, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        var ic = context.Version.InputContract;

        if (string.IsNullOrWhiteSpace(ic.Table))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "InputContract",
                "Table", "Input table name is required.", null, true));

        if (string.IsNullOrWhiteSpace(ic.Schema))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "InputContract",
                "Schema", "Input schema is required.", null, true));

        return Task.FromResult(new ProfileValidationOutcome(Name, findings));
    }
}
