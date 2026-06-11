namespace DeltaZulu.Platform.Data.Hunting;

using System.Globalization;
using System.Text.Json;
using DeltaZulu.Platform.Application.Hunting.Planning;
using DeltaZulu.Platform.Data.DuckDb;
using DeltaZulu.Platform.Data.DuckDb.Sql;
using DeltaZulu.Platform.Domain.Hunting.Planning;
using DeltaZulu.Platform.Domain.Hunting.Policy;
using DuckDB.NET.Data;

public sealed partial class QueryRuntime
{
    public QueryResult ExecuteDataOnly(string kql) => ExecuteDataOnly(kql, maxRows: null);

    public QueryResult ExecuteDataOnly(string kql, int? maxRows)
    {
        var diagnostics = new DiagnosticBag();
        var debugTrace = _developerMode ? new List<string>() : null;
        debugTrace?.Add($"Runtime start (data-only): plannerMaxIterations={_plannerMaxIterations}, timeoutSeconds={_timeoutSeconds}");

        var cacheKey = new CompileCacheKey(kql, _catalog.CatalogVersion, _plannerMaxIterations, _defaultLimit, _policyEpoch, _compilerEpoch);
        if (_compileCacheCapacity > 0 && _compileCache.TryGetValue(cacheKey, out var cacheHit))
        {
            debugTrace?.Add($"Compile cache hit: catalogVersion={cacheKey.CatalogVersion}");
            return ExecuteSql(
                cacheHit.Sql,
                maxRows,
                diagnostics,
                debugTrace,
                plannerStatsJson: cacheHit.PlannerStatsJson,
                sqlShapeStatsJson: cacheHit.SqlShapeStatsJson,
                emitterStatsJson: cacheHit.EmitterStatsJson);
        }

        var translator = new KustoToRelational(_catalog, diagnostics);
        var relNode = translator.Translate(kql);
        debugTrace?.Add($"Translate complete: hasErrors={diagnostics.HasErrors}, relNode={(relNode is null ? "null" : relNode.GetType().Name)}");

        if (diagnostics.HasErrors || relNode is null)
        {
            return QueryResult.FromDiagnostics(diagnostics, debugTrace);
        }

        try
        {
            var plannerDecision = DecidePlannerExecution(relNode);
            debugTrace?.Add($"Planner gateway: decision={plannerDecision.Decision}, reason={plannerDecision.Reason}, joinCount={plannerDecision.JoinCount}, estimatedRows={plannerDecision.EstimatedRows?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
            if (plannerDecision.Decision == "run")
            {
                relNode = _planner.Plan(relNode, new PlannerContext(Enabled: true, MaxIterations: _plannerMaxIterations));
                debugTrace?.Add($"Planner complete: relNode={relNode.GetType().Name}");
            }
            else
            {
                debugTrace?.Add("Planner bypassed by gateway");
            }
        }
        catch (Exception ex)
        {
            diagnostics.AddError(
                DiagnosticPhase.Emit,
                "Failed during logical planning stage.",
                ex.Message,
                code: QueryDiagnosticCodes.PlannerFailed);

            return QueryResult.FromDiagnostics(diagnostics, debugTrace);
        }

        string? plannerStats = null;
        if (_developerMode && _planner is IPlannerTelemetry telemetry && telemetry.LastRunStats is not null)
        {
            plannerStats = JsonSerializer.Serialize(telemetry.LastRunStats);
        }

        string sql;
        string? sqlShapeStats;
        string? emitterStats;
        try
        {
            var emitter = new DuckDbQueryEmitter(_defaultLimit, applyDefaultLimit: false);
            sql = emitter.Emit(relNode);
            var shape = SqlShapeMetrics.FromSql(sql);
            sqlShapeStats = JsonSerializer.Serialize(shape);
            emitterStats = JsonSerializer.Serialize(emitter.LastRunStats);
            debugTrace?.Add($"Emit complete: sqlLength={sql.Length}, cteStages={shape.CteStageCount}, selects={shape.SelectCount}, joins={shape.JoinCount}");
            debugTrace?.Add($"Emitter stats: {emitterStats}");
        }
        catch (Exception ex)
        {
            diagnostics.AddError(
                DiagnosticPhase.Emit,
                "Failed to generate SQL from query model.",
                ex.Message,
                code: QueryDiagnosticCodes.SqlEmitFailed);

            return QueryResult.FromDiagnostics(diagnostics, debugTrace);
        }

        try
        {
            var conn = _connectionFactory.GetConnection();
            debugTrace?.Add("Execute start");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _timeoutSeconds;

            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.Default);

            var columns = new List<ResultColumn>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new ResultColumn(reader.GetName(i), reader.GetDataTypeName(i)));
            }

            var columnData = ReadAllColumns(reader, maxRows);
            var exposedSql = _developerMode ? sql : null;
            debugTrace?.Add($"Execute complete: rows={(columnData.Length == 0 ? 0 : columnData[0].Count)}, columns={columns.Count}");

            if (_compileCacheCapacity > 0)
            {
                var entry = new CompileCacheEntry(sql, plannerStats, sqlShapeStats, emitterStats);
                RememberCompileCache(cacheKey, entry);
                debugTrace?.Add("Compile cache store");
            }

            return QueryResult.FromData(columns, columnData, exposedSql, plannerStats, sqlShapeStats, debugTrace, diagnostics);
        }
        catch (DuckDBException ex)
        {
            diagnostics.AddError(
                DiagnosticPhase.Execute,
                NormalizeDuckDbError(ex.Message),
                BuildDeveloperDetail(sql, ex.Message),
                code: QueryDiagnosticCodes.ExecuteDuckDbFailed);

            return QueryResult.FromDiagnostics(diagnostics, debugTrace);
        }
        catch (Exception ex)
        {
            diagnostics.AddError(
                DiagnosticPhase.Execute,
                "An internal error occurred while executing the query.",
                BuildDeveloperDetail(sql, $"{ex.GetType().Name}: {ex.Message}"),
                code: QueryDiagnosticCodes.ExecuteUnhandledFailed);

            return QueryResult.FromDiagnostics(diagnostics, debugTrace);
        }
    }

    public QueryStreamResult ExecuteStreamedDataOnly(string kql, Func<DuckDBDataReader, bool> onRow)
    {
        ArgumentNullException.ThrowIfNull(onRow);

        var diagnostics = new DiagnosticBag();
        var debugTrace = _developerMode ? new List<string>() : null;
        debugTrace?.Add($"Runtime start (streamed data-only): plannerMaxIterations={_plannerMaxIterations}, timeoutSeconds={_timeoutSeconds}");

        var cacheKey = new CompileCacheKey(kql, _catalog.CatalogVersion, _plannerMaxIterations, _defaultLimit, _policyEpoch, _compilerEpoch);
        string sql;
        string? plannerStats = null;
        string? sqlShapeStats;
        string? emitterStats;

        if (_compileCacheCapacity > 0 && _compileCache.TryGetValue(cacheKey, out var cacheHit))
        {
            sql = cacheHit.Sql;
            plannerStats = cacheHit.PlannerStatsJson;
            sqlShapeStats = cacheHit.SqlShapeStatsJson;
            debugTrace?.Add($"Compile cache hit: catalogVersion={cacheKey.CatalogVersion}");
        }
        else
        {
            var translator = new KustoToRelational(_catalog, diagnostics);
            var relNode = translator.Translate(kql);
            if (diagnostics.HasErrors || relNode is null)
            {
                return QueryStreamResult.FromDiagnostics(diagnostics, debugTrace);
            }

            var plannerDecision = DecidePlannerExecution(relNode);
            debugTrace?.Add($"Planner gateway: decision={plannerDecision.Decision}, reason={plannerDecision.Reason}, joinCount={plannerDecision.JoinCount}, estimatedRows={plannerDecision.EstimatedRows?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}");
            if (plannerDecision.Decision == "run")
            {
                relNode = _planner.Plan(relNode, new PlannerContext(Enabled: true, MaxIterations: _plannerMaxIterations));
            }

            if (_developerMode && plannerDecision.Decision == "run" && _planner is IPlannerTelemetry telemetry && telemetry.LastRunStats is not null)
            {
                plannerStats = JsonSerializer.Serialize(telemetry.LastRunStats);
            }

            var emitter = new DuckDbQueryEmitter(_defaultLimit, applyDefaultLimit: false);
            sql = emitter.Emit(relNode);
            sqlShapeStats = JsonSerializer.Serialize(SqlShapeMetrics.FromSql(sql));
            emitterStats = JsonSerializer.Serialize(emitter.LastRunStats);

            if (_compileCacheCapacity > 0)
            {
                RememberCompileCache(cacheKey, new CompileCacheEntry(sql, plannerStats, sqlShapeStats, emitterStats));
            }
        }

        try
        {
            var conn = _connectionFactory.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _timeoutSeconds;

            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.SequentialAccess);

            var columns = new List<ResultColumn>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new ResultColumn(reader.GetName(i), reader.GetDataTypeName(i)));
            }

            var rowCount = 0;
            while (reader.Read())
            {
                rowCount++;
                if (!onRow(reader))
                {
                    break;
                }
            }

            return QueryStreamResult.FromData(
                columns,
                rowCount,
                _developerMode ? sql : null,
                plannerStats,
                sqlShapeStats,
                debugTrace,
                diagnostics);
        }
        catch (DuckDBException ex)
        {
            diagnostics.AddError(
                DiagnosticPhase.Execute,
                NormalizeDuckDbError(ex.Message),
                BuildDeveloperDetail(sql, ex.Message),
                code: QueryDiagnosticCodes.ExecuteDuckDbFailed);

            return QueryStreamResult.FromDiagnostics(
                diagnostics,
                debugTrace,
                generatedSql: _developerMode ? sql : null,
                plannerStatsJson: plannerStats,
                sqlShapeStatsJson: sqlShapeStats);
        }
        catch (Exception ex)
        {
            diagnostics.AddError(
                DiagnosticPhase.Execute,
                "An internal error occurred while executing the query.",
                BuildDeveloperDetail(sql, $"{ex.GetType().Name}: {ex.Message}"),
                code: QueryDiagnosticCodes.ExecuteUnhandledFailed);

            return QueryStreamResult.FromDiagnostics(
                diagnostics,
                debugTrace,
                generatedSql: _developerMode ? sql : null,
                plannerStatsJson: plannerStats,
                sqlShapeStatsJson: sqlShapeStats);
        }
    }
}