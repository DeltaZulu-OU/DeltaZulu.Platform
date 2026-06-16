using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;

namespace DeltaZulu.Platform.Domain.Analytics.Compilation;

public interface IQueryCompiler
{
    long CatalogVersion { get; }

    RelNode? Compile(string queryText, DiagnosticBag diagnostics);
}