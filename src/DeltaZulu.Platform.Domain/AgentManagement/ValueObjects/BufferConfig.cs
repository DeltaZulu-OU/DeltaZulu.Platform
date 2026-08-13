using DeltaZulu.Platform.Domain.AgentManagement.Enums;

namespace DeltaZulu.Platform.Domain.AgentManagement.ValueObjects;

public sealed record BufferConfig(
    string Path,
    long MaxDiskBytes,
    long MaxMemoryBytes,
    int MaxChunkRecords,
    long MaxChunkBytes,
    int MaxChunkAgeSeconds,
    BufferFullPolicy FullPolicy,
    RetryExhaustedPolicy RetryExhaustedPolicy,
    int MaxRetryAttempts,
    int RetryBaseDelaySeconds,
    int RetryMaxDelaySeconds)
{
    public static BufferConfig DefaultEndpoint() => new(
        Path: "%ProgramData%\\DeltaZulu\\buffer",
        MaxDiskBytes: 1_073_741_824,
        MaxMemoryBytes: 134_217_728,
        MaxChunkRecords: 10_000,
        MaxChunkBytes: 10_485_760,
        MaxChunkAgeSeconds: 60,
        FullPolicy: BufferFullPolicy.Block,
        RetryExhaustedPolicy: RetryExhaustedPolicy.DeadLetter,
        MaxRetryAttempts: 5,
        RetryBaseDelaySeconds: 2,
        RetryMaxDelaySeconds: 300);
}
