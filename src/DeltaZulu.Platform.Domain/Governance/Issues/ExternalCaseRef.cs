using DeltaZulu.Platform.Domain.Governance.Common;

namespace DeltaZulu.Platform.Domain.Governance.Issues;

/// <summary>Discriminates between external system categories for UI display and connector routing.</summary>
public enum ExternalSystemType
{
    Generic = 0,
    FlowIntel = 1,
    TheHive = 2,
    Itsm = 3,
    SocIncident = 4,
}

/// <summary>
/// Reference to a case or ticket managed in an external system (FlowIntel, TheHive, ITSM,
/// or similar). <see cref="SystemType"/> is a display hint; <see cref="System"/> carries
/// the raw identifier used by connectors.
/// </summary>
public sealed class ExternalCaseRef
{
    public ExternalSystemType SystemType { get; }
    public string System { get; }
    public string ExternalId { get; }
    public string? Url { get; }

    public ExternalCaseRef(string system, string externalId, string? url = null,
        ExternalSystemType systemType = ExternalSystemType.Generic)
    {
        if (string.IsNullOrWhiteSpace(system))
            throw new DomainException("external_case.system_empty", "External case system identifier must not be empty.");
        if (system.Length > 64)
            throw new DomainException("external_case.system_too_long", "External case system identifier exceeds 64 characters.");
        if (string.IsNullOrWhiteSpace(externalId))
            throw new DomainException("external_case.id_empty", "External case ID must not be empty.");
        if (externalId.Length > 200)
            throw new DomainException("external_case.id_too_long", "External case ID exceeds 200 characters.");
        if (url?.Length > 2000)
            throw new DomainException("external_case.url_too_long", "External case URL exceeds 2000 characters.");

        SystemType = systemType;
        System = system;
        ExternalId = externalId;
        Url = url;
    }
}