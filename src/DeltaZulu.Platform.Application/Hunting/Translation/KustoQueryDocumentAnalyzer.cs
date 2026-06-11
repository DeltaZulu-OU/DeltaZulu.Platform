
using DeltaZulu.Platform.Application.Hunting.Catalog;
using DeltaZulu.Platform.Domain.Hunting.Policy;
using Kusto.Language;
using Kusto.Language.Syntax;

namespace DeltaZulu.Platform.Application.Hunting.Translation;
/// <summary>Parses KQL, imports diagnostics, and validates document-level policy boundaries.</summary>
internal sealed class KustoQueryDocumentAnalyzer
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly DiagnosticBag _diagnostics;

    public KustoQueryDocumentAnalyzer(ApprovedViewCatalog catalog, DiagnosticBag diagnostics)
    {
        _catalog = catalog;
        _diagnostics = diagnostics;
        TableReferencePolicy = new KustoTableReferencePolicy(catalog, diagnostics);
    }

    public KustoTableReferencePolicy TableReferencePolicy { get; }

    public KustoQueryDocument? Analyze(string kql)
    {
        if (string.IsNullOrWhiteSpace(kql))
        {
            _diagnostics.AddError(DiagnosticPhase.Parse, "KQL input is empty.");
            return null;
        }

        if (KustoManagementCommandGuard.ContainsExecutableCommandText(kql))
        {
            AddManagementCommandDiagnostic();
            return null;
        }

        var code = KustoCode.ParseAndAnalyze(kql, _catalog.BuildGlobalState());
        var hasParseErrors = false;
        foreach (var diagnostic in code.GetDiagnostics())
        {
            if (diagnostic.Severity != Kusto.Language.DiagnosticSeverity.Error
                || IsKnownOptionalExtractArgDiagnostic(diagnostic.Message))
            {
                continue;
            }

            hasParseErrors = true;
            _diagnostics.AddError(
                DiagnosticPhase.Parse,
                diagnostic.Message,
                diagnostic.Code,
                diagnostic.Start,
                diagnostic.Length);
        }

        if (KustoManagementCommandGuard.ContainsExecutableCommand(code.Syntax))
        {
            AddManagementCommandDiagnostic();
            return null;
        }

        TableReferencePolicy.ValidateQualifiedApprovedTableReferences(code.Syntax);
        var statements = code.Syntax.GetDescendants<Statement>()
            .Where(statement => IsTopLevel(statement, code.Syntax))
            .ToList();
        return new KustoQueryDocument(statements, hasParseErrors);
    }

    private void AddManagementCommandDiagnostic() => _diagnostics.AddError(
        DiagnosticPhase.Policy,
        "Management commands are not allowed. Submit only a single query expression, optionally preceded by let bindings.");

    private static bool IsKnownOptionalExtractArgDiagnostic(string? message) =>
        !string.IsNullOrWhiteSpace(message)
        && message.Contains("function 'extract' expects 4 arguments", StringComparison.OrdinalIgnoreCase);

    private static bool IsTopLevel(SyntaxNode node, SyntaxNode root)
    {
        var parent = node.Parent;
        while (parent is not null && parent != root)
        {
            if (parent is Statement) { return false; }
            parent = parent.Parent;
        }
        return true;
    }
}

internal sealed record KustoQueryDocument(IReadOnlyList<Statement> Statements, bool HasParseErrors);