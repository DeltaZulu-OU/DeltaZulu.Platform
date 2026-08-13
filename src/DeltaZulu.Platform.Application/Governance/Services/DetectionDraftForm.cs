namespace DeltaZulu.Platform.Application.Governance.Services;

public sealed record DetectionDraftForm(
    string Id,
    string Title,
    string Description,
    string Severity,
    string Confidence,
    int RiskScore,
    string TriggerType,
    int? RealtimeThreshold,
    string? ScheduleExpression,
    string? Lookback,
    int? MaxAlertsPerRun,
    string KqlQuery,
    IReadOnlyList<string> MitreAttackTechniques,
    IReadOnlyList<DetectionEntityMappingDraft> EntityMappings,
    IReadOnlyList<string> FalsePositiveNotes);

public sealed record DetectionEntityMappingDraft(string Type, string Field);
