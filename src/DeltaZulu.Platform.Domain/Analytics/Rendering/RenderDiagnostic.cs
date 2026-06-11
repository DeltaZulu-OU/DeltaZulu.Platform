namespace DeltaZulu.Platform.Domain.Analytics.Rendering;

public sealed record RenderDiagnostic(
    RenderDiagnosticSeverity Severity,
    string Message);