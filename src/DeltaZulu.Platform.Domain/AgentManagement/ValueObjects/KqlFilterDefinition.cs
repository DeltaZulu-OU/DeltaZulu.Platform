namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record KqlFilterDefinition(
    string Language,
    string Query);
