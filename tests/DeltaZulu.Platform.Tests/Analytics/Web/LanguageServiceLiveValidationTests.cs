using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.Schema;
using DeltaZulu.Platform.Web.Analytics.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace DeltaZulu.Platform.Tests.Analytics.Web;

[TestClass]
public sealed class LanguageServiceLiveValidationTests
{
    [TestMethod]
    public void ValidateEditorQuery_BlankQuery_ReturnsNoMarkers()
    {
        var service = CreateService();

        var markers = service.ValidateEditorQuery("   ");

        Assert.IsEmpty(markers);
    }

    [TestMethod]
    public void ValidateEditorQuery_ValidQuery_ReturnsNoMarkers()
    {
        var service = CreateService();

        var markers = service.ValidateEditorQuery("ProcessEvent | take 1");

        Assert.IsEmpty(markers, string.Join("\n", markers.Select(marker => marker.Message)));
    }

    [TestMethod]
    public void ValidateEditorQuery_UnsupportedOperator_ReturnsErrorMarkerWithSpan()
    {
        var service = CreateService();

        var markers = service.ValidateEditorQuery("ProcessEvent | mv-expand Tags");

        Assert.IsNotEmpty(markers);
        var marker = markers[0];
        Assert.AreEqual(DiagnosticSeverity.Error.ToString(), marker.Severity);
        Assert.IsTrue(marker.TextStart.HasValue, "Live validation markers should preserve diagnostic start offsets.");
        Assert.IsTrue(marker.TextLength.HasValue, "Live validation markers should preserve diagnostic text lengths.");
        Assert.IsTrue(marker.TextLength > 0, "Live validation marker spans should not be empty.");
    }

    [TestMethod]
    public void EditorDiagnosticMarker_FromDiagnostic_PreservesMessageSeverityAndSpan()
    {
        var diagnostic = new QueryDiagnostic(
            DiagnosticSeverity.Warning,
            DiagnosticPhase.Translate,
            "KQLTEST",
            "Example warning",
            TextStart: 7,
            TextLength: 3);

        var marker = LanguageService.EditorDiagnosticMarker.FromDiagnostic(diagnostic);

        Assert.AreEqual("Example warning", marker.Message);
        Assert.AreEqual("Warning", marker.Severity);
        Assert.AreEqual(7, marker.TextStart);
        Assert.AreEqual(3, marker.TextLength);
    }

    private static LanguageService CreateService()
    {
        var catalog = new ApprovedViewCatalog();
        catalog.RegisterAll(SchemaConventions.CanonicalViews);

        return new LanguageService(
            new NoopJSRuntime(),
            NullLogger<LanguageService>.Instance,
            catalog);
    }

    private sealed class NoopJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => throw new NotSupportedException("JS interop is not used by live-validation unit tests.");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
            => throw new NotSupportedException("JS interop is not used by live-validation unit tests.");
    }
}
