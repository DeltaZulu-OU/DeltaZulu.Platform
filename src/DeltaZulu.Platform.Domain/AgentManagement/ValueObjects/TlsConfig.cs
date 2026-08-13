using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record TlsConfig(
    bool UseTls,
    TlsValidationMode ValidationMode,
    IReadOnlyList<string>? Thumbprints,
    bool UseClientCertificate,
    string? CertificatePath,
    string? KeyPath,
    string? CaPath);
