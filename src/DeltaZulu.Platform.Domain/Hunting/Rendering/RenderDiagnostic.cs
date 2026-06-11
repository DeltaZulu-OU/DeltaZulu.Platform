namespace DeltaZulu.Platform.Domain.Hunting.Rendering;

public sealed record RenderDiagnostic(
    RenderDiagnosticSeverity Severity,
    string Message);