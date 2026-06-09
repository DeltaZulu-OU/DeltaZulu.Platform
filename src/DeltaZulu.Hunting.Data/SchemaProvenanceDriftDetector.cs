namespace Hunting.Data;

using Hunting.Core.DuckDbSql;
using Hunting.Core.Schema;

/// <summary>
/// Compares expected schema-object fingerprints with recorded provenance rows.
/// This detector reports drift only; it does not classify migration safety or block apply.
/// </summary>
public sealed class SchemaProvenanceDriftDetector
{
    private readonly SchemaProvenanceRecorder _recorder;
    private readonly SchemaEmitter _schemaEmitter;

    public SchemaProvenanceDriftDetector(
        SchemaProvenanceRecorder recorder,
        SchemaEmitter? schemaEmitter = null)
    {
        _recorder = recorder;
        _schemaEmitter = schemaEmitter ?? new SchemaEmitter();
    }

    public IReadOnlyList<SchemaProvenanceDrift> DetectDrift(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<InternalTableDef> internalTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews)
    {
        ArgumentNullException.ThrowIfNull(rawTables);
        ArgumentNullException.ThrowIfNull(internalTables);
        ArgumentNullException.ThrowIfNull(parserViews);
        ArgumentNullException.ThrowIfNull(canonicalViews);

        var expected = BuildExpectedFingerprints(rawTables, internalTables, parserViews, canonicalViews)
            .ToDictionary(static fingerprint => fingerprint.ObjectName, StringComparer.OrdinalIgnoreCase);

        var recorded = _recorder.ReadRecordedProvenance()
            .ToDictionary(static row => row.ObjectName, StringComparer.OrdinalIgnoreCase);

        var names = expected.Keys
            .Concat(recorded.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase);

        var drift = new List<SchemaProvenanceDrift>();

        foreach (var name in names)
        {
            var hasExpected = expected.TryGetValue(name, out var expectedFingerprint);
            var hasRecorded = recorded.TryGetValue(name, out var recordedRow);

            if (hasExpected && !hasRecorded)
            {
                drift.Add(new SchemaProvenanceDrift(
                    name,
                    expectedFingerprint!.ObjectKind,
                    SchemaProvenanceDriftStatus.NewObject,
                    ExpectedHash: expectedFingerprint.SchemaHash,
                    RecordedHash: null,
                    Message: $"Schema object {name} is expected but has no recorded provenance row."));
                continue;
            }

            if (!hasExpected && hasRecorded)
            {
                drift.Add(new SchemaProvenanceDrift(
                    name,
                    recordedRow!.ObjectKind,
                    SchemaProvenanceDriftStatus.MissingObject,
                    ExpectedHash: null,
                    RecordedHash: recordedRow.SchemaHash,
                    Message: $"Schema object {name} is recorded but is no longer expected by the active catalog."));
                continue;
            }

            if (expectedFingerprint!.SchemaHash.Equals(recordedRow!.SchemaHash, StringComparison.OrdinalIgnoreCase))
            {
                drift.Add(new SchemaProvenanceDrift(
                    name,
                    expectedFingerprint.ObjectKind,
                    SchemaProvenanceDriftStatus.Unchanged,
                    ExpectedHash: expectedFingerprint.SchemaHash,
                    RecordedHash: recordedRow.SchemaHash,
                    Message: $"Schema object {name} is unchanged."));
                continue;
            }

            drift.Add(new SchemaProvenanceDrift(
                name,
                expectedFingerprint.ObjectKind,
                SchemaProvenanceDriftStatus.ChangedObject,
                ExpectedHash: expectedFingerprint.SchemaHash,
                RecordedHash: recordedRow.SchemaHash,
                Message: $"Schema object {name} has a provenance hash mismatch."));
        }

        return drift;
    }

    private IReadOnlyList<SchemaObjectFingerprint> BuildExpectedFingerprints(
        IEnumerable<RawTableDef> rawTables,
        IEnumerable<InternalTableDef> internalTables,
        IEnumerable<ParserViewDef> parserViews,
        IEnumerable<CanonicalViewDef> canonicalViews)
    {
        var fingerprints = new List<SchemaObjectFingerprint>();

        foreach (var table in rawTables)
        {
            fingerprints.Add(SchemaFingerprint.FromRawTable(table));
        }

        foreach (var table in internalTables)
        {
            fingerprints.Add(SchemaFingerprint.FromInternalTable(table));
        }

        foreach (var view in parserViews)
        {
            fingerprints.Add(SchemaFingerprint.FromParserView(view, _schemaEmitter.EmitParserView(view)));
        }

        foreach (var view in canonicalViews)
        {
            fingerprints.Add(SchemaFingerprint.FromCanonicalView(view, _schemaEmitter.EmitCanonicalView(view)));
        }

        return fingerprints;
    }
}

public sealed record SchemaProvenanceDrift(
    string ObjectName,
    string ObjectKind,
    SchemaProvenanceDriftStatus Status,
    string? ExpectedHash,
    string? RecordedHash,
    string Message);

public enum SchemaProvenanceDriftStatus
{
    Unchanged,
    NewObject,
    ChangedObject,
    MissingObject
}
