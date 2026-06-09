namespace DeltaZulu.Workbench.Domain.Enums;

/// <summary>
/// Kind of draft content stored against a <see cref="Changes.ChangeRequest"/>. Drives
/// validation routing in the check pipeline and binary/text discrimination in the
/// canonical writer.
/// </summary>
public enum DraftContentType
{
    /// <summary>Detection metadata YAML (parsed against detection schema by the check pipeline).</summary>
    DetectionMetadata = 0,

    /// <summary>Hunting/detection query (KQL, SPL, YARA, Sigma — routed to the appropriate parser check).</summary>
    HuntingQuery = 1,

    /// <summary>Test definition YAML (expected results for automated testing).</summary>
    TestDefinition = 2,

    /// <summary>Test fixture data (NDJSON, CSV — fed to the detection during test execution).</summary>
    Fixture = 3,

    /// <summary>
    /// Investigation note in Markdown with optional YAML frontmatter. Used for disconnected
    /// case notes in air-gapped environments. Cross-document links use standard relative paths
    /// within the Git tree; the UI viewer rewrites them to Blazor routes at render time.
    /// </summary>
    InvestigationNote = 4,

    /// <summary>
    /// Static asset (image, diagram, pcap excerpt, PDF) accompanying a note or detection.
    /// Content is base64-encoded in the draft and passed through to the content store as-is
    /// with <c>IsBinary = true</c>. The content store implementation is responsible for
    /// decoding to binary on disk. The check pipeline skips text-based validation for this type.
    /// </summary>
    StaticAsset = 5,
}
