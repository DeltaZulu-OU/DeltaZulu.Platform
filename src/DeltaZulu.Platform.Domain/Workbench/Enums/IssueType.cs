namespace DeltaZulu.Platform.Domain.Workbench.Enums;

/// <summary>
/// Detection-content-specific issue types. Each type carries different required intake
/// fields and drives different acceptance criteria.
/// </summary>
public enum IssueType
{
    /// <summary>SOC investigation linked to an external case system (FlowIntel, TheHive).</summary>
    Case = 0,

    /// <summary>Legacy alias — superseded by <see cref="DetectionRequest"/>.</summary>
    Request = 1,

    /// <summary>Propose a new detection rule or analytic.</summary>
    DetectionRequest = 2,

    /// <summary>Reduce noisy or incorrect detection behaviour.</summary>
    FalsePositiveReport = 3,

    /// <summary>Fix incorrect detection logic or failed execution.</summary>
    DetectionBug = 4,

    /// <summary>Track missing analytic visibility or coverage.</summary>
    CoverageGap = 5,

    /// <summary>Fix ATT&amp;CK mapping, severity, tags, references, or owner.</summary>
    MetadataIssue = 6,

    /// <summary>Track failing schema validation, syntax check, or fixture mismatch.</summary>
    TestFailure = 7,

    /// <summary>Improve detection notes or analyst guidance.</summary>
    DocumentationIssue = 8,

    /// <summary>Retire obsolete or superseded detection content.</summary>
    Deprecation = 9,
}