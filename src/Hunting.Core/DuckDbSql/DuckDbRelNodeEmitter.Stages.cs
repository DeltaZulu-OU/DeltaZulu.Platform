namespace Hunting.Core.DuckDbSql;

using QueryModel;

internal sealed partial class DuckDbRelNodeEmitter
{
    internal string StageFrom(RelNode input)
    {
        var (source, cols) = EmitNode(input);
        // Pass-through elimination: when the input already produced a standalone
        // CTE stage and no column projection needs to be applied, reuse that
        // stage directly instead of emitting a redundant `SELECT * FROM stage`
        // wrapper. Base-table scans (`golden.X`) are never CTE references, so the
        // scan still gets its own stage.
        if (cols is null && _context.Stages.IsStageReference(source))
        {
            return source;
        }

        var stage = _context.Stages.NextStage();
        var sql = $"SELECT {cols ?? "*"} FROM {source}";
        _context.Stages.AddStage(stage, sql);
        return stage;
    }
}