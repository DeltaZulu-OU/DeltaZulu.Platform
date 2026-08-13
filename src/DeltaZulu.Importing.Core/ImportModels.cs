using System.Collections.ObjectModel;

namespace DeltaZulu.Importing.Core;

public enum ImportMode { Migration, Onboarding, DemoSeed }
public enum ImportFormat { JsonLines, PlainText, JsonArray, Csv }
public enum ImportDiagnosticSeverity { Information, Warning, Error }
public enum ImportJobStatus { Created, Completed, CompletedWithDiagnostics, Failed }

public sealed record ImportJobRequest(
    string JobId,
    ImportMode Mode,
    string SourcePath,
    string ParserTarget,
    string PreprocessorVersion,
    int MaxChunkBytes = 1024 * 1024,
    bool ScheduleSilverNormalization = true);

public sealed record ImportDiagnostic(
    ImportDiagnosticSeverity Severity,
    string Code,
    string Message,
    string? SourceObjectId = null,
    int? ChunkIndex = null,
    long? RecordNumber = null);

public sealed record ImportSourceObject(
    string SourceObjectId,
    string Path,
    string RelativePath,
    long SizeBytes,
    string Sha256,
    ImportFormat Format,
    string Status,
    IReadOnlyList<ImportDiagnostic> Diagnostics);

public sealed record ImportChunk(
    string ChunkId,
    string SourceObjectId,
    int Index,
    long StartByteInclusive,
    long EndByteExclusive,
    long StartRecordInclusive,
    long EndRecordInclusive,
    string Sha256,
    string Payload,
    string Status);

public sealed record BronzeImportRow(
    string ImportJobId,
    string SourceObjectId,
    string ChunkId,
    int ChunkIndex,
    long SourceRecordNumber,
    string ParserTarget,
    string PreprocessorVersion,
    string SourcePath,
    string SourceSha256,
    string ChunkSha256,
    string RawLog,
    string RawText);

public sealed record ImportSummaryMetrics(
    int SourceFileCount,
    int ChunkCount,
    int BronzeRowCount,
    int FailedFileCount,
    int MalformedRecordCount,
    bool SilverNormalizationScheduled);

public sealed class ImportManifest
{
    public ImportManifest(string jobId, ImportMode mode, string sourcePath, string parserTarget, string preprocessorVersion)
    {
        JobId = jobId;
        Mode = mode;
        SourcePath = sourcePath;
        ParserTarget = parserTarget;
        PreprocessorVersion = preprocessorVersion;
    }

    public string JobId { get; }
    public ImportMode Mode { get; }
    public string SourcePath { get; }
    public string ParserTarget { get; }
    public string PreprocessorVersion { get; }
    public ImportJobStatus Status { get; internal set; } = ImportJobStatus.Created;
    public Collection<ImportSourceObject> SourceObjects { get; } = [];
    public Collection<ImportChunk> Chunks { get; } = [];
    public Collection<ImportDiagnostic> Diagnostics { get; } = [];
    public ImportSummaryMetrics Summary { get; internal set; } = new(0, 0, 0, 0, 0, false);
}

public sealed record ImportResult(ImportManifest Manifest, IReadOnlyList<BronzeImportRow> BronzeRows);
