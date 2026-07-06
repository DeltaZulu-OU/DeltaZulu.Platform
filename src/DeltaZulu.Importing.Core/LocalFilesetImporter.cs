using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DeltaZulu.Importing.Core;

public sealed class LocalFilesetImporter
{
    public ImportResult Import(ImportJobRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.JobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ParserTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PreprocessorVersion);
        if (request.MaxChunkBytes <= 0) throw new ArgumentOutOfRangeException(nameof(request));

        var manifest = new ImportManifest(request.JobId.Trim(), request.Mode, request.SourcePath, request.ParserTarget.Trim(), request.PreprocessorVersion.Trim());
        var rows = new List<BronzeImportRow>();

        foreach (var path in EnumerateFiles(request.SourcePath))
        {
            ProcessFile(request, manifest, rows, path);
        }

        var failedFiles = manifest.SourceObjects.Count(item => item.Status == "failed");
        var malformed = manifest.Diagnostics.Count(item => item.Code == "malformed-record");
        manifest.Summary = new ImportSummaryMetrics(manifest.SourceObjects.Count, manifest.Chunks.Count, rows.Count, failedFiles, malformed, request.ScheduleSilverNormalization);
        manifest.Status = failedFiles > 0 || malformed > 0 ? ImportJobStatus.CompletedWithDiagnostics : ImportJobStatus.Completed;
        if (request.ScheduleSilverNormalization)
        {
            manifest.Diagnostics.Add(new ImportDiagnostic(ImportDiagnosticSeverity.Information, "silver-normalization-scheduled", "Silver normalization was scheduled for imported Bronze rows."));
        }

        return new ImportResult(manifest, rows);
    }

    private static IEnumerable<string> EnumerateFiles(string sourcePath)
    {
        if (File.Exists(sourcePath)) return [Path.GetFullPath(sourcePath)];
        if (Directory.Exists(sourcePath)) return Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal);
        throw new DirectoryNotFoundException($"Import source path '{sourcePath}' does not exist.");
    }

    private static void ProcessFile(ImportJobRequest request, ImportManifest manifest, List<BronzeImportRow> rows, string path)
    {
        var format = DetectFormat(path);
        var sourceHash = Sha256(File.ReadAllBytes(path));
        var relative = File.Exists(request.SourcePath) ? Path.GetFileName(path) : Path.GetRelativePath(request.SourcePath, path);
        var sourceId = StableId(request.JobId, relative, sourceHash);
        var diagnostics = new List<ImportDiagnostic>();
        var records = ReadRecords(path, format, sourceId, diagnostics).ToArray();
        var source = new ImportSourceObject(sourceId, path, relative, new FileInfo(path).Length, sourceHash, format, diagnostics.Count == 0 ? "completed" : "completed-with-diagnostics", diagnostics);
        manifest.SourceObjects.Add(source);
        foreach (var diagnostic in diagnostics) manifest.Diagnostics.Add(diagnostic);

        var chunkRecords = new List<(long Number, string RawLog, string RawText)>();
        var chunkBytes = 0;
        var chunkIndex = 0;
        foreach (var record in records)
        {
            var bytes = Encoding.UTF8.GetByteCount(record.RawText) + 1;
            if (chunkRecords.Count > 0 && chunkBytes + bytes > request.MaxChunkBytes)
            {
                WriteChunk(request, manifest, rows, source, chunkRecords, chunkIndex++);
                chunkRecords.Clear();
                chunkBytes = 0;
            }
            chunkRecords.Add(record);
            chunkBytes += bytes;
        }
        if (chunkRecords.Count > 0) WriteChunk(request, manifest, rows, source, chunkRecords, chunkIndex);
    }

    private static IEnumerable<(long Number, string RawLog, string RawText)> ReadRecords(string path, ImportFormat format, string sourceId, List<ImportDiagnostic> diagnostics)
    {
        if (format is ImportFormat.PlainText or ImportFormat.JsonLines or ImportFormat.Csv)
        {
            long number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (format == ImportFormat.JsonLines && !IsJson(line)) diagnostics.Add(new ImportDiagnostic(ImportDiagnosticSeverity.Error, "malformed-record", "JSONL record is not valid JSON.", sourceId, RecordNumber: number));
                yield return (number, format == ImportFormat.PlainText ? JsonSerializer.Serialize(new { message = line }) : line, line);
            }
            yield break;
        }

        if (format == ImportFormat.JsonArray)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var number = 0L;
            foreach (var element in doc.RootElement.EnumerateArray()) yield return (++number, element.GetRawText(), element.GetRawText());
        }
    }

    private static void WriteChunk(ImportJobRequest request, ImportManifest manifest, List<BronzeImportRow> rows, ImportSourceObject source, IReadOnlyList<(long Number, string RawLog, string RawText)> records, int index)
    {
        var payload = string.Join('\n', records.Select(item => item.RawText));
        var hash = Sha256(Encoding.UTF8.GetBytes(payload));
        var chunkId = StableId(request.JobId, source.SourceObjectId, index.ToString(System.Globalization.CultureInfo.InvariantCulture), hash);
        var chunk = new ImportChunk(chunkId, source.SourceObjectId, index, 0, Encoding.UTF8.GetByteCount(payload), records[0].Number, records[^1].Number, hash, payload, "prepared");
        manifest.Chunks.Add(chunk);
        rows.AddRange(records.Select(item => new BronzeImportRow(request.JobId, source.SourceObjectId, chunkId, index, item.Number, request.ParserTarget, request.PreprocessorVersion, source.RelativePath, source.Sha256, hash, item.RawLog, item.RawText)));
    }

    private static ImportFormat DetectFormat(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jsonl" or ".ndjson" => ImportFormat.JsonLines,
        ".json" => ImportFormat.JsonArray,
        ".csv" => ImportFormat.Csv,
        _ => ImportFormat.PlainText
    };

    private static bool IsJson(string value)
    {
        try { using var _ = JsonDocument.Parse(value); return true; }
        catch (JsonException) { return false; }
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string StableId(params string[] values) => Sha256(Encoding.UTF8.GetBytes(string.Join('|', values)))[..16];
}
