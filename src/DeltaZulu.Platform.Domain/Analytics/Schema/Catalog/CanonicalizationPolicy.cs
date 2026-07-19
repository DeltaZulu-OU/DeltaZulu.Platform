namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;

/// <summary>
/// The closed set of canonical forms a parser applies at parse time. One policy per
/// logical shape — this is not a menu of caller-selectable formatting options, it is
/// the single canonical form that shape is always normalized into (e.g. MAC addresses
/// are always lowercase colon-separated, never a mix of forms across sources).
/// </summary>
public enum CanonicalizationPolicy
{
    /// <summary>No canonicalization; the parser's raw extraction is used as-is.</summary>
    None,

    /// <summary>Timestamps are converted to UTC.</summary>
    Utc,

    /// <summary>MAC addresses are lowercased with colon separators (e.g. <c>aa:bb:cc:dd:ee:ff</c>).</summary>
    MacLowerColon,

    /// <summary>IPv6 addresses are stored in RFC 5952 compressed form.</summary>
    Ipv6Compressed
}
