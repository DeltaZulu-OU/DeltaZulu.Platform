using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record ValidationFinding(
    ValidationSeverity Severity,
    string ArtifactType,
    string? FieldPath,
    string Message,
    string? SuggestedFix,
    bool IsBlocking);
