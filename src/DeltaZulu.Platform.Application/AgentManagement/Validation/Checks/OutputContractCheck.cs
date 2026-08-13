using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Validation.Checks;

public sealed class OutputContractCheck : IProfileValidationCheck
{
    public string Name => "output-contract";
    public bool IsBlocking => true;

    public Task<ProfileValidationOutcome> RunAsync(ProfileValidationContext context, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        var oc = context.Version.OutputContract;

        if (!string.Equals(oc.Format, "ndjson", StringComparison.OrdinalIgnoreCase))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "OutputContract",
                "Format", "Output format must be 'ndjson'.",
                "Set format to 'ndjson'.", true));

        if (!oc.PreserveOriginalFieldNames)
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "OutputContract",
                "PreserveOriginalFieldNames", "PreserveOriginalFieldNames must be true.",
                "Set preserveOriginalFieldNames to true.", true));

        return Task.FromResult(new ProfileValidationOutcome(Name, findings));
    }
}
