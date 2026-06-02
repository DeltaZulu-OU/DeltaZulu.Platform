namespace Hunting.Data;

using System.Collections.Concurrent;
using System.Text.Json;
using DuckDB.NET.Data;
using Hunting.Core.Catalog;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Planning;
using Hunting.Core.Policy;
using Hunting.Core.QueryModel;
using Hunting.Core.Render;
using Hunting.Core.Translation;

/// <summary>
/// Result of a query execution — either data or diagnostics.
/// </summary>
public sealed class QueryResult
{
    public int ColumnCount => Columns.Count;
    public IReadOnlyList<IReadOnlyList<object?>> ColumnData { get; init; } = [];
    public IReadOnlyList<ResultColumn> Columns { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerStatsJson { get; init; }
    public RenderSpec RenderSpec { get; init; } = RenderSpecDefaults.Table();
    public int RowCount => ColumnData.Count == 0 ? 0 : ColumnData[0].Count;
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public static QueryResult FromData(
        List<ResultColumn> columns,
        List<object?>[] columnData,
        string? sql,
        string? plannerStatsJson,
        string? sqlShapeStatsJson,
        List<string>? debugTrace,
        DiagnosticBag diagnostics,
        RenderSpec renderSpec)
    {
        var readonlyColumnData = new IReadOnlyList<object?>[columnData.Length];
        for (var i = 0; i < columnData.Length; i++)
        {
            readonlyColumnData[i] = columnData[i];
        }

        return new QueryResult
        {
            Success = true,
            Columns = columns,
            ColumnData = readonlyColumnData,
            GeneratedSql = sql,
            PlannerStatsJson = plannerStatsJson,
            SqlShapeStatsJson = sqlShapeStatsJson,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics,
            RenderSpec = renderSpec
        };
    }

    public static QueryResult FromDiagnostics(DiagnosticBag diagnostics, List<string>? debugTrace = null, RenderSpec? renderSpec = null) => new QueryResult
    {
        Success = false,
        DebugTrace = debugTrace ?? [],
        Diagnostics = diagnostics,
        RenderSpec = renderSpec ?? RenderSpecDefaults.Table()
    };

    public object? GetValue(int row, int column) => ColumnData[column][row];
}

/// <summary>
/// <para>
/// Orchestrates the full query pipeline:
///   KQL string → ParseAndAnalyze → policy → translate → emit → execute → results
/// </para>
/// <para>
/// Returns either a QueryResult with column metadata and columnar data, or a list
/// of QueryDiagnostic errors.
/// </para>
/// </summary>
public sealed class QueryRuntime
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly ConcurrentDictionary<CompileCacheKey, CompileCacheEntry> _compileCache = new();
    private readonly int _compileCacheCapacity = 256;
    private readonly ConcurrentQueue<CompileCacheKey> _compileCacheOrder = new();
    private readonly DuckDbConnectionFactory _connectionFactory;
    private readonly int _defaultLimit;
    private readonly bool _developerMode;
    private readonly bool _includeSensitiveDeveloperDetail;
    private readonly IRelationalPlanner _planner;
    private readonly bool _plannerGatewayEnabled;
    private readonly int _plannerGatewayJoinComplexityThreshold;
    private readonly long _plannerGatewayMaxEstimatedRows;
    private readonly int _plannerMaxIterations;
    private readonly int _timeoutSeconds;
    private long _compilerEpoch;
    private long _policyEpoch;

    public QueryRuntime(
        ApprovedViewCatalog catalog,
        DuckDbConnectionFactory connectionFactory,
        int defaultLimit = 10_000,
        int timeoutSeconds = 30,
        bool developerMode = false,
        bool includeSensitiveDeveloperDetail = false,
        int plannerMaxIterations = 3,
        int compileCacheCapacity = 256,
        long policyEpoch = 1,
        long compilerEpoch = 1,
        IRelationalPlanner? planner = null,
        bool plannerGatewayEnabled = false,
        long plannerGatewayMaxEstimatedRows = 50_000,
        int plannerGatewayJoinComplexityThreshold = 2)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        if (defaultLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultLimit), defaultLimit, "Default limit must be greater than zero.");
        }

        if (timeoutSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "Timeout must be greater than zero.");
        }

        if (plannerMaxIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(plannerMaxIterations), plannerMaxIterations, "Planner max iterations must be at least one.");
        }

        if (compileCacheCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(compileCacheCapacity), compileCacheCapacity, "Compile cache capacity must be zero or greater.");
        }

        if (plannerGatewayMaxEstimatedRows < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannerGatewayMaxEstimatedRows), plannerGatewayMaxEstimatedRows, "Planner gateway estimated-row threshold must be zero or greater.");
        }

        if (plannerGatewayJoinComplexityThreshold < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannerGatewayJoinComplexityThreshold), plannerGatewayJoinComplexityThreshold, "Planner gateway join complexity threshold must be zero or greater.");
        }

        if (policyEpoch < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(policyEpoch), policyEpoch, "Policy epoch must be at least one.");
        }

        if (compilerEpoch < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(compilerEpoch), compilerEpoch, "Compiler epoch must be at least one.");
        }
        _catalog = catalog;
        _connectionFactory = connectionFactory;
        _defaultLimit = defaultLimit;
        _timeoutSeconds = timeoutSeconds;
        _developerMode = developerMode;
        _includeSensitiveDeveloperDetail = includeSensitiveDeveloperDetail;
        _planner = planner ?? new RelationalPlanner();
        _plannerMaxIterations = plannerMaxIterations;
        _plannerGatewayEnabled = plannerGatewayEnabled;
        _plannerGatewayMaxEstimatedRows = plannerGatewayMaxEstimatedRows;
        _plannerGatewayJoinComplexityThreshold = plannerGatewayJoinComplexityThreshold;
        _compileCacheCapacity = compileCacheCapacity;
        _policyEpoch = policyEpoch;
        _compilerEpoch = compilerEpoch;
    }

    /// <summary>
    /// Execute a KQL query and return the result.
    /// </summary>
    public QueryResult Execute(string kql) => Execute(kql, maxRows: null);

    public QueryResult Execute(string kql, int? maxRows)
    {
        var diagnostics = new DiagnosticBag();
        var debugTrace = _developerMode ? new List<string>() : null;
        debugTrace?.Add($"Runtime start: plannerMaxIterations={_plannerMaxIterations}, timeoutSeconds={_timeoutSeconds}");

        var (queryWithoutRender, renderSpec) = RenderDirectiveParser.Extract(kql);
        if (renderSpec.IsFallback && !string.IsNullOrWhiteSpace(renderSpec.FallbackReason))
        {
            diagnostics.AddWarning(DiagnosticPhase.Translate, renderSpec.FallbackReason);
        }
        var cacheKey = new CompileCacheKey(queryWithoutRender, _catalog.CatalogVersion, _plannerMaxIterations, _defaultLimit, _policyEpoch, _compilerEpoch);
        if (_compileCacheCapacity > 0 && _compileCache.TryGetValue(cacheKey, out var cacheHit))
        {
            debugTrace?.Add($"Compile cache hit: catalogVersion={cacheKey.CatalogVersion}");
            return ExecuteSql(cacheHit.Sql, maxRows, diagnostics, debugTrace, plannerStatsJson: cacheHit.PlannerStatsJson, sqlShapeStatsJson: cacheHit.SqlShapeStatsJson, emitterStatsJson: cacheHit.EmitterStatsJson, renderSpec: renderSpec);
        }

        // Phase 1: Parse + Translate
        var translator = new KustoToRelational(_catalog, diagnostics);
        var relNode = translator.Translate(queryWithoutRender);
        debugTrace?.Add($"Translate complete: hasErrors={diagnostics.HasErrors}, relNode={(relNode is null ? "null" : relNode.GetType().Name)}");

        if (diagnostics.HasErrors || relNode is null)
        {
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }

        // Phase 2: Optional logical planning
        try
        {
            var plannerDecision = DecidePlannerExecution(relNode);
            debugTrace?.Add($"Planner gateway: decision={plannerDecision.Decision}, reason={plannerDecision.Reason}, joinCount={plannerDecision.JoinCount}, estimatedRows={plannerDecision.EstimatedRows?.ToString() ?? "unknown"}");
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
            diagnostics.AddError(DiagnosticPhase.Emit,
                "Failed during logical planning stage.",
                ex.Message,
                code: QueryDiagnosticCodes.PlannerFailed);
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }

        string? plannerStats = null;
        if (_developerMode && _planner is IPlannerTelemetry telemetry && telemetry.LastRunStats is not null)
        {
            plannerStats = JsonSerializer.Serialize(telemetry.LastRunStats);
        }

        // Phase 3: Emit SQL
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
            diagnostics.AddError(DiagnosticPhase.Emit,
                "Failed to generate SQL from query model.",
                ex.Message,
                code: QueryDiagnosticCodes.SqlEmitFailed);
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }

        // Phase 4: Execute against DuckDB
        try
        {
            var conn = _connectionFactory.GetConnection();
            debugTrace?.Add("Execute start");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _timeoutSeconds;

            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.Default);

            // Read column metadata
            var columns = new List<ResultColumn>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new ResultColumn(
                    reader.GetName(i),
                    reader.GetDataTypeName(i)));
            }

            // Read rows
            var columnData = ReadAllColumns(reader, maxRows);

            // GeneratedSql is only exposed in developer mode — SQL is an internal artifact
            var exposedSql = _developerMode ? sql : null;
            debugTrace?.Add($"Execute complete: rows={(columnData.Length == 0 ? 0 : columnData[0].Count)}, columns={columns.Count}");
            if (_compileCacheCapacity > 0)
            {
                var entry = new CompileCacheEntry(sql, plannerStats, sqlShapeStats, emitterStats);
                RememberCompileCache(cacheKey, entry);
                debugTrace?.Add("Compile cache store");
            }

            return QueryResult.FromData(columns, columnData, exposedSql, plannerStats, sqlShapeStats, debugTrace, diagnostics, renderSpec);
        }
        catch (DuckDBException ex)
        {
            diagnostics.AddError(DiagnosticPhase.Execute,
                NormalizeDuckDbError(ex.Message),
                BuildDeveloperDetail(sql, ex.Message),
                code: QueryDiagnosticCodes.ExecuteDuckDbFailed);
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }
        catch (Exception ex)
        {
            diagnostics.AddError(DiagnosticPhase.Execute,
                "An internal error occurred while executing the query.",
                BuildDeveloperDetail(sql, $"{ex.GetType().Name}: {ex.Message}"),
                code: QueryDiagnosticCodes.ExecuteUnhandledFailed);
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }
    }

    /// <summary>
    /// Execute a KQL query and stream rows to a callback to avoid full in-memory row materialization.
    /// The callback can use typed DuckDBDataReader getters directly, avoiding object[] row allocation and boxing.
    /// </summary>
    public QueryStreamResult ExecuteStreamed(string kql, Func<DuckDBDataReader, bool> onRow)
    {
        ArgumentNullException.ThrowIfNull(onRow);

        var diagnostics = new DiagnosticBag();
        var debugTrace = _developerMode ? new List<string>() : null;
        debugTrace?.Add($"Runtime start (streamed): plannerMaxIterations={_plannerMaxIterations}, timeoutSeconds={_timeoutSeconds}");

        var (queryWithoutRender, renderSpec) = RenderDirectiveParser.Extract(kql);
        if (renderSpec.IsFallback && !string.IsNullOrWhiteSpace(renderSpec.FallbackReason))
        {
            diagnostics.AddWarning(DiagnosticPhase.Translate, renderSpec.FallbackReason);
        }
        var cacheKey = new CompileCacheKey(queryWithoutRender, _catalog.CatalogVersion, _plannerMaxIterations, _defaultLimit, _policyEpoch, _compilerEpoch);
        string sql;
        string? plannerStats = null;
        string? sqlShapeStats;
        string? emitterStats;

        if (_compileCacheCapacity > 0 && _compileCache.TryGetValue(cacheKey, out var cacheHit))
        {
            sql = cacheHit.Sql;
            plannerStats = cacheHit.PlannerStatsJson;
            sqlShapeStats = cacheHit.SqlShapeStatsJson;
            emitterStats = cacheHit.EmitterStatsJson;
            debugTrace?.Add($"Compile cache hit: catalogVersion={cacheKey.CatalogVersion}");
        }
        else
        {
            var translator = new KustoToRelational(_catalog, diagnostics);
            var relNode = translator.Translate(queryWithoutRender);
            if (diagnostics.HasErrors || relNode is null)
            {
                return QueryStreamResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
            }

            var plannerDecision = DecidePlannerExecution(relNode);
            debugTrace?.Add($"Planner gateway: decision={plannerDecision.Decision}, reason={plannerDecision.Reason}, joinCount={plannerDecision.JoinCount}, estimatedRows={plannerDecision.EstimatedRows?.ToString() ?? "unknown"}");
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

            return QueryStreamResult.FromData(columns, rowCount, _developerMode ? sql : null, plannerStats, sqlShapeStats, debugTrace, diagnostics, renderSpec);
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
                renderSpec,
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
                renderSpec,
                generatedSql: _developerMode ? sql : null,
                plannerStatsJson: plannerStats,
                sqlShapeStatsJson: sqlShapeStats);
        }
    }

    public QueryTabularResult ExecuteTabular(string kql, int? maxRows = null)
    {
        List<object?>[]? columnData = null;
        var streamed = ExecuteStreamed(
            kql,
            reader => {
                if (maxRows.HasValue && columnData is not null && columnData.Length > 0 && columnData[0].Count >= maxRows.Value)
                {
                    return false;
                }

                columnData ??= CreateColumnData(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    columnData[i].Add(reader.IsDBNull(i) ? null : reader.GetValue(i));
                }

                return true;
            });

        if (!streamed.Success)
        {
            return QueryTabularResult.FromDiagnostics(streamed.Diagnostics, streamed.DebugTrace, streamed.RenderSpec);
        }

        columnData ??= CreateColumnData(streamed.Columns.Count);
        var columnNames = new string[streamed.Columns.Count];
        for (var i = 0; i < streamed.Columns.Count; i++)
        {
            columnNames[i] = streamed.Columns[i].Name;
        }
        var readonlyColumnData = new IReadOnlyList<object?>[columnData.Length];
        for (var i = 0; i < columnData.Length; i++)
        {
            readonlyColumnData[i] = columnData[i];
        }
        return QueryTabularResult.FromData(
            columnNames,
            readonlyColumnData,
            streamed.RowCount,
            streamed.GeneratedSql,
            streamed.PlannerStatsJson,
            streamed.SqlShapeStatsJson,
            streamed.DebugTrace,
            streamed.Diagnostics,
            streamed.RenderSpec);
    }

    public void SetCompileEpochs(long policyEpoch, long compilerEpoch)
    {
        if (policyEpoch < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(policyEpoch), policyEpoch, "Policy epoch must be at least one.");
        }

        if (compilerEpoch < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(compilerEpoch), compilerEpoch, "Compiler epoch must be at least one.");
        }

        _policyEpoch = policyEpoch;
        _compilerEpoch = compilerEpoch;
        _compileCache.Clear();
        while (_compileCacheOrder.TryDequeue(out _)) { }
    }

    private static bool AreSimpleProjections(IReadOnlyList<ProjectionExpr> projections)
    {
        foreach (var projection in projections)
        {
            if (!IsSimpleScalar(projection.Expression))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSimpleSorts(IReadOnlyList<SortExpr> sorts)
    {
        foreach (var sort in sorts)
        {
            if (!IsSimpleScalar(sort.Expression))
            {
                return false;
            }
        }

        return true;
    }

    private static Func<DuckDBDataReader, int, object?>[] BuildTypedReaders(DuckDBDataReader reader)
    {
        var readers = new Func<DuckDBDataReader, int, object?>[reader.FieldCount];
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var fieldType = reader.GetFieldType(i);
            readers[i] = fieldType == typeof(string) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetString(idx)
                : fieldType == typeof(int) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetInt32(idx)
                : fieldType == typeof(long) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetInt64(idx)
                : fieldType == typeof(short) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetInt16(idx)
                : fieldType == typeof(byte) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetByte(idx)
                : fieldType == typeof(bool) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetBoolean(idx)
                : fieldType == typeof(float) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetFloat(idx)
                : fieldType == typeof(double) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetDouble(idx)
                : fieldType == typeof(decimal) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetDecimal(idx)
                : fieldType == typeof(DateTime) ? static (r, idx) => r.IsDBNull(idx) ? null : r.GetDateTime(idx)
                : static (r, idx) => r.IsDBNull(idx) ? null : r.GetValue(idx);
        }

        return readers;
    }

    private static void CollectViewNames(RelNode node, HashSet<string> views)
    {
        switch (node)
        {
            case ScanNode scan:
                views.Add(scan.ViewName);
                break;

            case JoinNode join:
                CollectViewNames(join.Left, views);
                CollectViewNames(join.Right, views);
                break;

            case FilterNode f:
                CollectViewNames(f.Input, views);
                break;

            case ProjectNode p:
                CollectViewNames(p.Input, views);
                break;

            case ExtendNode e:
                CollectViewNames(e.Input, views);
                break;

            case AggregateNode a:
                CollectViewNames(a.Input, views);
                break;

            case SortNode s:
                CollectViewNames(s.Input, views);
                break;

            case LimitNode l:
                CollectViewNames(l.Input, views);
                break;

            case SampleNode s:
                CollectViewNames(s.Input, views);
                break;

            case DistinctNode d:
                CollectViewNames(d.Input, views);
                break;

            case LetBindingNode l:
                if (l.TabularValue is not null)
                {
                    CollectViewNames(l.TabularValue, views);
                }

                CollectViewNames(l.Body, views);
                break;
        }
    }

    private static int CountJoins(RelNode node) => node switch
    {
        JoinNode j => 1 + CountJoins(j.Left) + CountJoins(j.Right),
        FilterNode f => CountJoins(f.Input),
        ProjectNode p => CountJoins(p.Input),
        ExtendNode e => CountJoins(e.Input),
        AggregateNode a => CountJoins(a.Input),
        SortNode s => CountJoins(s.Input),
        LimitNode l => CountJoins(l.Input),
        SampleNode s => CountJoins(s.Input),
        DistinctNode d => CountJoins(d.Input),
        LetBindingNode l => (l.TabularValue is null ? 0 : CountJoins(l.TabularValue)) + CountJoins(l.Body),
        _ => 0
    };

    private static List<object?>[] CreateColumnData(int count)
    {
        var columns = new List<object?>[count];
        for (var i = 0; i < count; i++)
        {
            columns[i] = [];
        }

        return columns;
    }

    private static bool IsSimpleScalar(ScalarExpr expr) => expr switch
    {
        ColumnRef => true,
        LiteralScalar => true,
        StarExpr => true,
        BinaryScalar b => IsSimpleScalar(b.Left) && IsSimpleScalar(b.Right),
        UnaryScalar u => IsSimpleScalar(u.Operand),
        _ => false
    };

    private static bool IsSimpleShape(RelNode node) => node switch
    {
        ScanNode => true,
        FilterNode f => IsSimpleShape(f.Input) && IsSimpleScalar(f.Predicate),
        ProjectNode p => IsSimpleShape(p.Input) && AreSimpleProjections(p.Projections),
        ExtendNode e => IsSimpleShape(e.Input) && AreSimpleProjections(e.Extensions),
        SortNode s => IsSimpleShape(s.Input) && AreSimpleSorts(s.Sorts),
        LimitNode l => IsSimpleShape(l.Input),
        SampleNode s => IsSimpleShape(s.Input),
        _ => false
    };

    /// <summary>
    /// Translate known DuckDB error patterns into KQL-terms messages.
    /// </summary>
    private static string NormalizeDuckDbError(string message)
    {
        if (message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
        {
            return "The referenced table or column does not exist. Check column names against the schema browser.";
        }

        if (message.Contains("syntax error", StringComparison.OrdinalIgnoreCase))
        {
            return "Internal query translation produced invalid SQL. This is a bug — please report it.";
        }

        if (message.Contains("conversion", StringComparison.OrdinalIgnoreCase))
        {
            return "A type conversion error occurred. Check that your filter values match the column types.";
        }

        if (message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Query execution timed out. Try narrowing your time range or adding more filters.";
        }

        if (message.Contains("out of memory", StringComparison.OrdinalIgnoreCase))
        {
            return "Query used too much memory. Try adding filters or reducing the result set.";
        }

        return $"Query execution failed: {message}";
    }

    private static List<object?>[] ReadAllColumns(DuckDBDataReader reader, int? maxRows)
    {
        var columns = CreateColumnData(reader.FieldCount);
        var typedReaders = BuildTypedReaders(reader);
        var rowCount = 0;
        while (reader.Read())
        {
            if (maxRows.HasValue && rowCount >= maxRows.Value)
            {
                break;
            }
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns[i].Add(typedReaders[i](reader, i));
            }
            rowCount++;
        }

        return columns;
    }

    private string BuildDeveloperDetail(string sql, string exceptionText)
    {
        if (!_developerMode)
        {
            return "Developer detail is disabled.";
        }

        if (_includeSensitiveDeveloperDetail)
        {
            return $"SQL: {sql}\nException: {exceptionText}";
        }

        return $"SQL length: {sql.Length}\nException: {exceptionText}";
    }

    private PlannerGatewayDecision DecidePlannerExecution(RelNode root)
    {
        if (!_plannerGatewayEnabled)
        {
            return new PlannerGatewayDecision("run", "gateway_disabled", null, CountJoins(root));
        }

        var joinCount = CountJoins(root);
        if (joinCount > _plannerGatewayJoinComplexityThreshold)
        {
            return new PlannerGatewayDecision("run", "join_complexity", null, joinCount);
        }

        if (IsSimpleShape(root))
        {
            return new PlannerGatewayDecision("bypass", "simple_shape", null, joinCount);
        }

        if (!TryEstimateReferencedVolume(root, out var estimatedRows))
        {
            return new PlannerGatewayDecision("run", "estimate_failed", null, joinCount);
        }

        return estimatedRows <= _plannerGatewayMaxEstimatedRows
            ? new PlannerGatewayDecision("bypass", "middle_shape_below_threshold", estimatedRows, joinCount)
            : new PlannerGatewayDecision("run", "middle_shape_above_threshold", estimatedRows, joinCount);
    }

    private QueryResult ExecuteSql(
                                string sql,
        int? maxRows,
        DiagnosticBag diagnostics,
        List<string>? debugTrace,
        string? plannerStatsJson,
        string? sqlShapeStatsJson,
        string? emitterStatsJson,
        RenderSpec renderSpec)
    {
        try
        {
            var conn = _connectionFactory.GetConnection();
            debugTrace?.Add("Execute start");
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = _timeoutSeconds;

            using var reader = cmd.ExecuteReader(System.Data.CommandBehavior.Default);

            var columns = new List<ResultColumn>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(new ResultColumn(reader.GetName(i), reader.GetDataTypeName(i)));
            }

            var columnData = ReadAllColumns(reader, maxRows);

            var exposedSql = _developerMode ? sql : null;
            debugTrace?.Add($"Execute complete: rows={(columnData.Length == 0 ? 0 : columnData[0].Count)}, columns={columns.Count}");
            if (!string.IsNullOrWhiteSpace(emitterStatsJson))
            {
                debugTrace?.Add($"Emitter stats: {emitterStatsJson}");
            }
            return QueryResult.FromData(columns, columnData, exposedSql, plannerStatsJson, sqlShapeStatsJson, debugTrace, diagnostics, renderSpec);
        }
        catch (DuckDBException ex)
        {
            diagnostics.AddError(DiagnosticPhase.Execute,
                NormalizeDuckDbError(ex.Message),
                BuildDeveloperDetail(sql, ex.Message),
                code: QueryDiagnosticCodes.ExecuteDuckDbFailed);
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }
        catch (Exception ex)
        {
            diagnostics.AddError(DiagnosticPhase.Execute,
                "An internal error occurred while executing the query.",
                BuildDeveloperDetail(sql, $"{ex.GetType().Name}: {ex.Message}"),
                code: QueryDiagnosticCodes.ExecuteUnhandledFailed);
            return QueryResult.FromDiagnostics(diagnostics, debugTrace, renderSpec);
        }
    }

    private void RememberCompileCache(CompileCacheKey key, CompileCacheEntry entry)
    {
        _compileCache[key] = entry;
        _compileCacheOrder.Enqueue(key);
        while (_compileCache.Count > _compileCacheCapacity && _compileCacheOrder.TryDequeue(out var oldest))
        {
            _compileCache.TryRemove(oldest, out _);
        }
    }

    private sealed record CompileCacheKey(string Kql, long CatalogVersion, int PlannerMaxIterations, int DefaultLimit, long PolicyEpoch, long CompilerEpoch);
    private sealed record CompileCacheEntry(string Sql, string? PlannerStatsJson, string? SqlShapeStatsJson, string? EmitterStatsJson);

    private bool TryEstimateReferencedVolume(RelNode root, out long estimatedRows)
    {
        estimatedRows = 0;
        var viewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectViewNames(root, viewNames);
        if (viewNames.Count == 0)
        {
            return false;
        }

        try
        {
            var conn = _connectionFactory.GetConnection();
            using var cmd = conn.CreateCommand();
            foreach (var viewName in viewNames)
            {
                cmd.CommandText = $"SELECT COUNT(*) FROM {viewName}";
                var raw = cmd.ExecuteScalar();
                if (raw is null || raw is DBNull)
                {
                    return false;
                }

                estimatedRows += Convert.ToInt64(raw);
            }

            return true;
        }
        catch
        {
            estimatedRows = 0;
            return false;
        }
    }

    private sealed record PlannerGatewayDecision(string Decision, string Reason, long? EstimatedRows, int JoinCount);
}

/// <summary>
/// Column metadata from a query result.
/// </summary>
public sealed record ResultColumn(string Name, string TypeName);

public sealed class QueryStreamResult
{
    public IReadOnlyList<ResultColumn> Columns { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerStatsJson { get; init; }
    public RenderSpec RenderSpec { get; init; } = RenderSpecDefaults.Table();
    public int RowCount { get; init; }
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public static QueryStreamResult FromData(List<ResultColumn> columns, int rowCount, string? sql, string? plannerStatsJson, string? sqlShapeStatsJson, List<string>? debugTrace, DiagnosticBag diagnostics, RenderSpec renderSpec) => new()
    {
        Success = true,
        Columns = columns,
        RowCount = rowCount,
        GeneratedSql = sql,
        PlannerStatsJson = plannerStatsJson,
        SqlShapeStatsJson = sqlShapeStatsJson,
        DebugTrace = debugTrace ?? [],
        Diagnostics = diagnostics,
        RenderSpec = renderSpec
    };

    public static QueryStreamResult FromDiagnostics(
        DiagnosticBag diagnostics,
        List<string>? debugTrace = null,
        RenderSpec? renderSpec = null,
        string? generatedSql = null,
        string? plannerStatsJson = null,
        string? sqlShapeStatsJson = null) => new()
        {
            Success = false,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics,
            RenderSpec = renderSpec ?? RenderSpecDefaults.Table(),
            GeneratedSql = generatedSql,
            PlannerStatsJson = plannerStatsJson,
            SqlShapeStatsJson = sqlShapeStatsJson
        };
}

public sealed class QueryTabularResult
{
    public IReadOnlyList<IReadOnlyList<object?>> ColumnData { get; init; } = [];
    public IReadOnlyList<string> Columns { get; init; } = [];
    public IReadOnlyList<string> DebugTrace { get; init; } = [];
    public DiagnosticBag Diagnostics { get; init; } = new();
    public string? GeneratedSql { get; init; }
    public string? PlannerStatsJson { get; init; }
    public RenderSpec RenderSpec { get; init; } = RenderSpecDefaults.Table();
    public int RowCount { get; init; }
    public string? SqlShapeStatsJson { get; init; }
    public bool Success { get; init; }

    public static QueryTabularResult FromData(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<object?>> columnData,
        int rowCount,
        string? sql,
        string? plannerStatsJson,
        string? sqlShapeStatsJson,
        IReadOnlyList<string>? debugTrace,
        DiagnosticBag diagnostics,
        RenderSpec renderSpec) => new()
        {
            Success = true,
            RowCount = rowCount,
            Columns = columns,
            ColumnData = columnData,
            GeneratedSql = sql,
            PlannerStatsJson = plannerStatsJson,
            SqlShapeStatsJson = sqlShapeStatsJson,
            DebugTrace = debugTrace ?? [],
            Diagnostics = diagnostics,
            RenderSpec = renderSpec
        };

    public static QueryTabularResult FromDiagnostics(DiagnosticBag diagnostics, IReadOnlyList<string>? debugTrace = null, RenderSpec? renderSpec = null) => new()
    {
        Success = false,
        DebugTrace = debugTrace ?? [],
        Diagnostics = diagnostics,
        RenderSpec = renderSpec ?? RenderSpecDefaults.Table()
    };
}