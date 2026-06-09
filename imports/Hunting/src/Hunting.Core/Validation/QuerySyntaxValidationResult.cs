namespace Hunting.Core.Validation;

using Hunting.Core.Policy;

public sealed record QuerySyntaxValidationResult(
    bool IsValid,
    IReadOnlyList<QueryDiagnostic> Diagnostics)
{
    public static QuerySyntaxValidationResult FromDiagnostics(IReadOnlyList<QueryDiagnostic> diagnostics) =>
        new(!diagnostics.Any(diagnostic => diagnostic.IsError), diagnostics);
}
