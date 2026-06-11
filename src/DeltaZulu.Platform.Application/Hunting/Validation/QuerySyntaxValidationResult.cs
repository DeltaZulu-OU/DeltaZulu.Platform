
using DeltaZulu.Platform.Domain.Hunting.Policy;

namespace DeltaZulu.Platform.Application.Hunting.Validation;
public sealed record QuerySyntaxValidationResult(
    bool IsValid,
    IReadOnlyList<QueryDiagnostic> Diagnostics)
{
    public static QuerySyntaxValidationResult FromDiagnostics(IReadOnlyList<QueryDiagnostic> diagnostics) =>
        new(!diagnostics.Any(diagnostic => diagnostic.IsError), diagnostics);
}