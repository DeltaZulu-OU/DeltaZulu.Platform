namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record RelpConfig(
    bool UseTls,
    IReadOnlyList<RelpEndpoint> Endpoints,
    string Encoding,
    string Transport);
