using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Kql.Relational;

namespace DeltaZulu.Platform.Domain.Analytics.Compilation;

public interface IQueryCompiler
{
    long CatalogVersion { get; }

    RelNode? Compile(string queryText, DiagnosticBag diagnostics);
}