namespace DeltaZulu.Platform.Data.Proton;

/// <summary>
/// Connection options for the Proton ClickHouse-compatible HTTP interface.
/// </summary>
public sealed class ProtonHttpClientOptions
{
    /// <summary>
    /// Base URL of the Proton HTTP API, e.g. <c>http://localhost:8123</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:8123";

    public string? Username { get; set; }
    public string? Password { get; set; }
}
