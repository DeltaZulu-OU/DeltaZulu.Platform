using DeltaZulu.Platform.Domain.AgentManagement.Contracts;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Validation.Checks;

public sealed class ResourceDescriptorCheck : IProfileValidationCheck
{
    public string Name => "resource-descriptor";
    public bool IsBlocking => true;

    public Task<ProfileValidationOutcome> RunAsync(ProfileValidationContext context, CancellationToken ct = default)
    {
        var findings = new List<ValidationFinding>();
        var rd = context.Version.ResourceDescriptor;

        if (string.IsNullOrWhiteSpace(rd.Platform))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceDescriptor",
                "Platform", "Platform is required.", null, true));

        if (string.IsNullOrWhiteSpace(rd.Family))
            findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceDescriptor",
                "Family", "Family is required.", null, true));

        if (rd.Family is "EventLog" or "Evtx")
        {
            if (string.IsNullOrWhiteSpace(rd.Channel))
                findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceDescriptor",
                    "Channel", $"Channel is required for {rd.Family} family.", null, true));
        }

        if (rd.Family is "Etw")
        {
            if (string.IsNullOrWhiteSpace(rd.Session))
                findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceDescriptor",
                    "Session", "Session is required for ETW family.", null, true));
            if (string.IsNullOrWhiteSpace(rd.Provider))
                findings.Add(new ValidationFinding(ValidationSeverity.Error, "ResourceDescriptor",
                    "Provider", "Provider is required for ETW family.", null, true));
        }

        return Task.FromResult(new ProfileValidationOutcome(Name, findings));
    }
}
