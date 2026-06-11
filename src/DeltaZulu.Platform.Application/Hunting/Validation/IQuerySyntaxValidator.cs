namespace DeltaZulu.Platform.Domain.Hunting.Validation;

/// <summary>
/// Host-agnostic query syntax validation seam for consumers such as Workbench.
/// Implementations must parse and translate KQL only; they must not execute queries,
/// open DuckDB connections, or depend on web/runtime composition.
/// </summary>
public interface IQuerySyntaxValidator
{
    QuerySyntaxValidationResult Validate(string queryText);
}