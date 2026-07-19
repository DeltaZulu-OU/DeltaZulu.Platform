using System.Reflection;

namespace DeltaZulu.Platform.Domain.Analytics.Schema.Catalog;

/// <summary>
/// Built-in type contract catalogs, embedded as JSON resources so they are the single
/// artifact every projection generates from — not re-encoded as C# object initializers
/// the way <c>BronzeSourceTables</c> is (ADR-2).
/// </summary>
public static class SourceFieldCatalogLibrary
{
    private static readonly Lazy<SourceFieldCatalog> LazyCefFirewall = new(() => Load("cef_firewall.catalog.json"));

    /// <summary>A CEF-heavy source deliberately chosen to exercise promotion (custom cs*/cn* extensions lifted to typed columns).</summary>
    public static SourceFieldCatalog CefFirewall => LazyCefFirewall.Value;

    private static SourceFieldCatalog Load(string fileName)
    {
        var assembly = typeof(SourceFieldCatalogLibrary).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(n => n.EndsWith(fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Embedded catalog resource '{fileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded catalog resource '{fileName}' could not be opened.");
        using var reader = new StreamReader(stream);
        return SourceFieldCatalog.LoadFromJson(reader.ReadToEnd());
    }
}
