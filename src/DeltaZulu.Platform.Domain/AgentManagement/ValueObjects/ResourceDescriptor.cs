namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record ResourceDescriptor(
    string Platform,
    string Family,
    string? Service,
    string? Channel,
    string? Session,
    string? Provider,
    IReadOnlyList<string>? RecordTypes);
