namespace Workbench.Domain.Enums;

/// <summary>
/// Issue types. <see cref="Case"/> tracks SOC investigations linked to external systems.
/// <see cref="Request"/> tracks detection content change requests.
/// </summary>
public enum IssueType
{
    Case = 0,
    Request = 1,
}
