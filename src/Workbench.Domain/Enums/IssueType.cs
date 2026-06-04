namespace Workbench.Domain.Enums;

/// <summary>
/// Issue taxonomy. <see cref="Case"/> indicates detection content work triggered by an
/// external case investigation (FlowIntel, TheHive, etc.); the case itself is managed
/// in the external system and linked via <see cref="Issues.ExternalCaseRef"/>.
/// </summary>
public enum IssueType
{
    NewDetection = 0,
    Tuning = 1,
    Bug = 2,
    TestGap = 3,
    Research = 4,
    Documentation = 5,
    Maintenance = 6,
    Case = 7,
}