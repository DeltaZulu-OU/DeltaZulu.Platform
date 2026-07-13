using DeltaZulu.Platform.Domain.AgentManagement.Configs;
using DeltaZulu.Platform.Domain.AgentManagement.Enums;
using DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

namespace DeltaZulu.Platform.Application.AgentManagement.Validation;

/// <summary>
/// Pre-rollout validation of daemon config versions: buffer limits, retry policy,
/// TLS consistency, and diagnostics settings. Creation-time invariants
/// (forward-requires-RELP, file-requires-path) already live on the aggregate;
/// these checks gate the Draft -> Validated transition.
/// </summary>
public static class DaemonConfigValidator
{
    public static IReadOnlyList<ValidationFinding> Validate(DaemonConfigVersion version)
    {
        var findings = new List<ValidationFinding>();
        ValidateBuffer(version.Buffer, findings);
        ValidateTls(version.Tls, version.Relp, findings);
        ValidateDiagnostics(version.Diagnostics, findings);
        return findings;
    }

    public static bool HasBlockingFailures(IReadOnlyList<ValidationFinding> findings) =>
        findings.Any(f => f.IsBlocking);

    private static void ValidateBuffer(BufferConfig buffer, List<ValidationFinding> findings)
    {
        if (string.IsNullOrWhiteSpace(buffer.Path))
            findings.Add(Blocking("Buffer", "Path", "Buffer path is required."));

        if (buffer.MaxDiskBytes < 1)
            findings.Add(Blocking("Buffer", "MaxDiskBytes", "Disk buffer limit must be positive."));

        if (buffer.MaxMemoryBytes < 1)
            findings.Add(Blocking("Buffer", "MaxMemoryBytes", "Memory buffer limit must be positive."));

        if (buffer.MaxMemoryBytes > buffer.MaxDiskBytes)
            findings.Add(Blocking("Buffer", "MaxMemoryBytes",
                "Memory buffer limit must not exceed the disk buffer limit."));

        if (buffer.MaxChunkRecords < 1)
            findings.Add(Blocking("Buffer", "MaxChunkRecords", "Chunk record limit must be positive."));

        if (buffer.MaxChunkBytes < 1 || buffer.MaxChunkBytes > buffer.MaxMemoryBytes)
            findings.Add(Blocking("Buffer", "MaxChunkBytes",
                "Chunk byte limit must be positive and must not exceed the memory buffer limit."));

        if (buffer.MaxChunkAgeSeconds < 1)
            findings.Add(Blocking("Buffer", "MaxChunkAgeSeconds", "Chunk age limit must be positive."));

        if (buffer.MaxRetryAttempts < 0)
            findings.Add(Blocking("Buffer", "MaxRetryAttempts", "Retry attempts must not be negative."));

        if (buffer.RetryBaseDelaySeconds < 1)
            findings.Add(Blocking("Buffer", "RetryBaseDelaySeconds", "Retry base delay must be positive."));

        if (buffer.RetryMaxDelaySeconds < buffer.RetryBaseDelaySeconds)
            findings.Add(Blocking("Buffer", "RetryMaxDelaySeconds",
                "Retry maximum delay must be at least the base delay."));
    }

    private static void ValidateTls(TlsConfig tls, RelpConfig relp, List<ValidationFinding> findings)
    {
        if (tls.UseTls && tls.ValidationMode == TlsValidationMode.Thumbprint
            && (tls.Thumbprints is null || tls.Thumbprints.Count == 0))
        {
            findings.Add(Blocking("Tls", "Thumbprints",
                "Thumbprint validation mode requires at least one pinned thumbprint."));
        }

        if (tls.UseClientCertificate
            && (string.IsNullOrWhiteSpace(tls.CertificatePath) || string.IsNullOrWhiteSpace(tls.KeyPath)))
        {
            findings.Add(Blocking("Tls", "CertificatePath",
                "Client certificate authentication requires certificate and key paths."));
        }

        if (relp.UseTls && !tls.UseTls)
            findings.Add(Blocking("Tls", "UseTls",
                "RELP requests TLS but the TLS configuration is disabled."));

        if (tls.UseTls && tls.ValidationMode == TlsValidationMode.None)
            findings.Add(new ValidationFinding(ValidationSeverity.Warning, "Tls", "ValidationMode",
                "TLS certificate validation is disabled; forwarding is exposed to interception.",
                "Use System or Thumbprint validation.", false));
    }

    private static void ValidateDiagnostics(DiagnosticsConfig diagnostics, List<ValidationFinding> findings)
    {
        if (diagnostics.Enabled && diagnostics.IntervalSeconds < 1)
            findings.Add(Blocking("Diagnostics", "IntervalSeconds",
                "Diagnostics interval must be positive when diagnostics are enabled."));
    }

    private static ValidationFinding Blocking(string artifactType, string fieldPath, string message) =>
        new(ValidationSeverity.Error, artifactType, fieldPath, message, null, true);
}
