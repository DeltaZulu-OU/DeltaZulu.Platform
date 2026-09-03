using DeltaZulu.Kql.Compilation;

namespace DeltaZulu.Platform.Domain.Analytics.Policy;

/// <summary>
/// Adapts the shared DeltaZulu.Kql compiler's immutable <see cref="KqlDiagnostic"/>
/// results into Platform's <see cref="DiagnosticBag"/>, so callers that already
/// depend on Platform's diagnostics pipeline (query history, UI markers, dashboard
/// error surfaces) keep working unchanged.
/// </summary>
public static class KqlDiagnosticAdapter
{
    public static void CopyInto(IEnumerable<KqlDiagnostic> source, DiagnosticBag target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        foreach (var diagnostic in source)
        {
            target.Add(new QueryDiagnostic(
                ToSeverity(diagnostic.Severity),
                ToPhase(diagnostic.Phase),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.Detail,
                diagnostic.TextStart,
                diagnostic.TextLength));
        }
    }

    private static DiagnosticSeverity ToSeverity(KqlDiagnosticSeverity severity) => severity switch {
        KqlDiagnosticSeverity.Error => DiagnosticSeverity.Error,
        KqlDiagnosticSeverity.Warning => DiagnosticSeverity.Warning,
        KqlDiagnosticSeverity.Info => DiagnosticSeverity.Info,
        _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown KqlDiagnosticSeverity.")
    };

    private static DiagnosticPhase ToPhase(KqlDiagnosticPhase phase) => phase switch {
        KqlDiagnosticPhase.Parse => DiagnosticPhase.Parse,
        KqlDiagnosticPhase.Policy => DiagnosticPhase.Policy,
        KqlDiagnosticPhase.Translate => DiagnosticPhase.Translate,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown KqlDiagnosticPhase.")
    };
}
