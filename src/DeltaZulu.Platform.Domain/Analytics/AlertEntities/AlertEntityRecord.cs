namespace DeltaZulu.Platform.Domain.Analytics.AlertEntities;

public sealed record AlertEntityRecord(
    string Id,
    string AlertId,
    string EntityType,
    string EntityValue,
    string Role,
    double SpecificityWeight,
    double CriticalityWeight,
    bool IsHighFanout,
    DateTime CreatedAtUtc);