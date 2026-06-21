using DeltaZulu.Blazor.Interop;

namespace DeltaZulu.Platform.Web.Analytics.Dashboards;

/// <summary>
/// Dashboard-specific file transfer operations. Delegates the browser file I/O to the
/// generic <see cref="FileOperationsService"/> and adds the domain conventions (MIME type,
/// file extension filter) so callers don't repeat those details.
/// </summary>
public sealed class DashboardTransferInterop(FileOperationsService fileOps)
{
    private const string JsonMimeType = "application/json;charset=utf-8";
    private const string JsonAcceptFilter = "application/json,.json";

    public ValueTask DownloadAsync(string fileName, string json) =>
        fileOps.DownloadAsync(fileName, json, JsonMimeType);

    public ValueTask<string?> PickAsync(CancellationToken cancellationToken = default) =>
        fileOps.PickAsync(JsonAcceptFilter, cancellationToken);
}
