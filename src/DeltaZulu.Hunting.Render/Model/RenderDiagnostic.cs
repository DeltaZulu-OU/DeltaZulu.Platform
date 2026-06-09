namespace DeltaZulu.Hunting.Render.Model;

public sealed record RenderDiagnostic(
    RenderDiagnosticSeverity Severity,
    string Message);
