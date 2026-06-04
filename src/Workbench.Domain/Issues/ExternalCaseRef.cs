using Workbench.Domain.Common;

namespace Workbench.Domain.Issues;

/// <summary>
/// Reference to a case managed in an external system (FlowIntel, TheHive, or similar).
/// </summary>
public sealed class ExternalCaseRef
{
    public string System { get; }
    public string ExternalId { get; }
    public string? Url { get; }

    public ExternalCaseRef(string system, string externalId, string? url = null)
    {
        if (string.IsNullOrWhiteSpace(system))
            throw new DomainException("external_case.system_empty", "External case system identifier must not be empty.");
        if (system.Length > 64)
            throw new DomainException("external_case.system_too_long", "External case system identifier exceeds 64 characters.");
        if (string.IsNullOrWhiteSpace(externalId))
            throw new DomainException("external_case.id_empty", "External case ID must not be empty.");
        if (externalId.Length > 200)
            throw new DomainException("external_case.id_too_long", "External case ID exceeds 200 characters.");
        if (url is not null && url.Length > 2000)
            throw new DomainException("external_case.url_too_long", "External case URL exceeds 2000 characters.");

        System = system;
        ExternalId = externalId;
        Url = url;
    }
}
