namespace DeltaZulu.Hunting.Core.Translation;

using Catalog;
using Kusto.Language.Syntax;
using Policy;

/// <summary>Enforces the approved, unqualified KQL table-reference boundary.</summary>
internal sealed class KustoTableReferencePolicy
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly DiagnosticBag _diagnostics;

    public KustoTableReferencePolicy(ApprovedViewCatalog catalog, DiagnosticBag diagnostics)
    {
        _catalog = catalog;
        _diagnostics = diagnostics;
    }

    public bool TryValidateTablePathQualifiers(IReadOnlyList<string> parts, out string tableName)
    {
        tableName = parts[^1];
        if (parts.Count == 1) { return true; }
        _diagnostics.AddError(DiagnosticPhase.Policy, BuildQualifiedTablePathRejectedMessage(parts));
        return false;
    }

    public void ValidateQualifiedApprovedTableReferences(SyntaxNode root)
    {
        foreach (var path in root.GetDescendants<PathExpression>())
        {
            var parts = KustoSyntaxHelpers.GetPathParts(path);
            if (parts.Count <= 1 || !_catalog.IsApproved(parts[^1])) { continue; }
            _diagnostics.AddError(DiagnosticPhase.Policy, BuildQualifiedTablePathRejectedMessage(parts));
        }
    }

    private static string BuildQualifiedTablePathRejectedMessage(IReadOnlyList<string> parts)
        => $"Table path '{string.Join('.', parts)}' is not allowed. Use the unqualified table name '{parts[^1]}'.";
}
