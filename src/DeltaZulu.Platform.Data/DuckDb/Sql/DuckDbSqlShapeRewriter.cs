
using System.Text.RegularExpressions;

namespace DeltaZulu.Platform.Data.DuckDb.Sql;
internal sealed partial class DuckDbSqlShapeRewriter
{
    private readonly DuckDbStageRegistry _stages;

    internal DuckDbSqlShapeRewriter(DuckDbStageRegistry stages)
    {
        _stages = stages;
    }

    internal sealed record TerminalTopK(string Source, string OrderBy, int Limit);
    internal sealed record TerminalOrder(string Source, string OrderBy);
    internal sealed record TerminalLimit(string Source, int Limit);
    private sealed record SelectStageShape(
        string Projection,
        string Source,
        string? Predicate = null,
        string? GroupByClause = null,
        string? OrderBy = null,
        int? Limit = null);

    [GeneratedRegex(
        @"^SELECT \* FROM (golden\.[A-Za-z_][A-Za-z0-9_]*)$",
        RegexOptions.IgnoreCase)]
    internal static partial Regex TrivialSourceStageRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>[A-Za-z0-9_.]+) ORDER BY (?<order>.+) LIMIT (?<limit>\d+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminalTopKRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>[A-Za-z0-9_.]+) ORDER BY (?<order>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminalOrderRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>.+) LIMIT (?<limit>\d+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex TerminalLimitRegex();

    [GeneratedRegex(
        @"^SELECT (?<proj>.+) FROM (?<source>[A-Za-z0-9_.]+)(?<group>\s+GROUP BY .+)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex FinalStageInlineRegex();

    [GeneratedRegex(
        @"^SELECT \* FROM (?<source>[A-Za-z0-9_.]+) WHERE (?<pred>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex FilterStageInlineRegex();

    [GeneratedRegex(
        @"^SELECT \*, (?<extensions>.+) FROM (?<source>[A-Za-z0-9_.]+) WHERE (?<pred>.+)$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExtendWithFilterStageRegex();

    [GeneratedRegex(@"^(?<expr>.+)\s+AS\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.IgnoreCase, "en-150")]
    private static partial Regex DecodedPattern();

    internal void TryInlineSingleComputedScope(
        ref string finalSource,
        ref TerminalLimit? terminalLimit)
    {
        var sourceToken = terminalLimit?.Source ?? finalSource;
        var stageIdx = _stages.GetStageIndex(sourceToken);
        if (stageIdx < 0)
        {
            return;
        }

        var stage = _stages.Ctes[stageIdx];
        var match = ExtendWithFilterStageRegex().Match(stage.Sql);
        if (!match.Success)
        {
            return;
        }

        if (_stages.CountStageReferences(stage.Name) != 0)
        {
            return;
        }

        var source = match.Groups["source"].Value;
        if (_stages.IsStageReference(source))
        {
            return;
        }

        var derivedSource = $"(SELECT *, {match.Groups["extensions"].Value} FROM {source} WHERE {match.Groups["pred"].Value}) AS s";
        _stages.RemoveStageAt(stageIdx);

        if (terminalLimit is null)
        {
            finalSource = derivedSource;
            return;
        }

        terminalLimit = terminalLimit with { Source = derivedSource };
        finalSource = derivedSource;
    }

    internal bool TryRenderDerivedComputedScope(
        string? columns,
        TerminalTopK? terminalTopK,
        TerminalOrder? terminalOrder,
        TerminalLimit? terminalLimit,
        out string sql)
    {
        sql = string.Empty;
        if (terminalTopK is not null || terminalOrder is not null || terminalLimit is null || columns is null || columns == "*")
        {
            return false;
        }

        var stage2Idx = _stages.GetStageIndex(terminalLimit.Source);
        if (stage2Idx < 0)
        {
            return false;
        }

        var stage2 = _stages.Ctes[stage2Idx];
        var stage2Match = ExtendWithFilterStageRegex().Match(stage2.Sql);
        if (!stage2Match.Success)
        {
            return false;
        }

        var stage1Name = stage2Match.Groups["source"].Value;
        var stage1Idx = _stages.GetStageIndex(stage1Name);
        if (stage1Idx < 0)
        {
            return false;
        }

        var stage1 = _stages.Ctes[stage1Idx];
        var stage1Match = ExtendWithFilterStageRegex().Match(stage1.Sql);
        if (!stage1Match.Success)
        {
            return false;
        }

        if (_stages.CountStageReferences(stage1Name) != 1)
        {
            return false;
        }

        var outerColumns = columns;
        var decodedMatch = DecodedPattern().Match(stage2Match.Groups["extensions"].Value);
        if (decodedMatch.Success)
        {
            var alias = decodedMatch.Groups["alias"].Value;
            var expr = decodedMatch.Groups["expr"].Value;
            var aliasPattern = $@"(?<![A-Za-z0-9_\.]){Regex.Escape(alias)}(?![A-Za-z0-9_])";
            var replaced = false;
            outerColumns = Regex.Replace(
                outerColumns,
                aliasPattern,
                _ => {
                    if (replaced)
                    {
                        return _.Value;
                    }

                    replaced = true;
                    return $"{expr} AS {alias}";
                });
        }

        sql = $"SELECT {outerColumns} FROM (SELECT *, {stage1Match.Groups["extensions"].Value} FROM {stage1Match.Groups["source"].Value} WHERE {stage1Match.Groups["pred"].Value}) AS s WHERE {stage2Match.Groups["pred"].Value} LIMIT {terminalLimit.Limit}";
        return true;
    }

    internal TerminalTopK? ShapeSql(ref string finalSource)
    {
        var terminalTopK = TryExtractTerminalTopK(finalSource);
        var substitutions = new Dictionary<string, string>(StringComparer.Ordinal);
        var refCounts = _stages.BuildStageRefCounts();
        for (var i = _stages.Ctes.Count - 1; i >= 0; i--)
        {
            var cte = _stages.Ctes[i];
            var match = TrivialSourceStageRegex().Match(cte.Sql);
            if (!match.Success)
            {
                continue;
            }

            var usage = refCounts.TryGetValue(cte.Name, out var count) ? count : 0;
            // If a trivial source stage is referenced exactly once, we can inline it
            // into that consumer. If it is referenced zero times, it is already dead
            // after earlier shaping (e.g. terminal top-k extraction) and can be dropped.
            if (usage > 1)
            {
                continue;
            }

            substitutions[cte.Name] = match.Groups[1].Value;
            _stages.RemoveStageAt(i);
        }

        if (substitutions.Count > 0)
        {
            for (var i = 0; i < _stages.Ctes.Count; i++)
            {
                // Replace whole stage tokens only. A plain string.Replace of
                // "__kql_stage_1" would also corrupt "__kql_stage_10",
                // "__kql_stage_11", etc. since it is a prefix of those names.
                var sql = DuckDbQueryEmitter.StageRefRegex().Replace(
                _stages.Ctes[i].Sql,
                m => substitutions.TryGetValue(m.Value, out var rep) ? rep : m.Value);

                _stages.ReplaceStageSql(i, sql);
            }

            foreach (var pair in substitutions)
            {
                if (string.Equals(finalSource, pair.Key, StringComparison.Ordinal))
                {
                    finalSource = pair.Value;
                }

                if (terminalTopK is not null
                    && string.Equals(terminalTopK.Source, pair.Key, StringComparison.Ordinal))
                {
                    terminalTopK = terminalTopK with { Source = pair.Value };
                }
            }
        }

        return terminalTopK;
    }

    private static SelectStageShape? ParseSelectShape(string sql)
    {
        if (TryParseSimpleSelectShape(sql, out var simple))
        {
            return simple;
        }

        var topK = TerminalTopKRegex().Match(sql);
        if (topK.Success)
        {
            return new SelectStageShape(
                Projection: "*",
                Source: topK.Groups["source"].Value,
                OrderBy: topK.Groups["order"].Value,
                Limit: int.Parse(topK.Groups["limit"].Value));
        }

        var order = TerminalOrderRegex().Match(sql);
        if (order.Success)
        {
            return new SelectStageShape("*", order.Groups["source"].Value, OrderBy: order.Groups["order"].Value);
        }

        var filter = FilterStageInlineRegex().Match(sql);
        if (filter.Success)
        {
            return new SelectStageShape(
                Projection: "*",
                Source: filter.Groups["source"].Value,
                Predicate: filter.Groups["pred"].Value);
        }

        var final = FinalStageInlineRegex().Match(sql);
        if (final.Success)
        {
            return new SelectStageShape(
                Projection: final.Groups["proj"].Value,
                Source: final.Groups["source"].Value,
                GroupByClause: final.Groups["group"].Value);
        }

        return null;
    }

    private static bool TryParseSimpleSelectShape(string sql, out SelectStageShape? shape)
    {
        shape = null;
        var text = sql.Trim();
        if (!text.StartsWith("SELECT * FROM ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var from = text["SELECT * FROM ".Length..];
        var orderByIdx = from.IndexOf(" ORDER BY ", StringComparison.OrdinalIgnoreCase);
        var limitIdx = from.IndexOf(" LIMIT ", StringComparison.OrdinalIgnoreCase);
        var whereIdx = from.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);

        if (whereIdx > 0)
        {
            var source = from[..whereIdx];
            var pred = from[(whereIdx + " WHERE ".Length)..];
            shape = new SelectStageShape("*", source, Predicate: pred);
            return true;
        }

        if (orderByIdx > 0 && limitIdx > orderByIdx)
        {
            var source = from[..orderByIdx];
            var order = from[(orderByIdx + " ORDER BY ".Length)..limitIdx];
            var limitText = from[(limitIdx + " LIMIT ".Length)..];
            if (int.TryParse(limitText, out var lim))
            {
                shape = new SelectStageShape("*", source, OrderBy: order, Limit: lim);
                return true;
            }
        }

        if (orderByIdx > 0)
        {
            var source = from[..orderByIdx];
            var order = from[(orderByIdx + " ORDER BY ".Length)..];
            shape = new SelectStageShape("*", source, OrderBy: order);
            return true;
        }

        if (limitIdx > 0)
        {
            var source = from[..limitIdx];
            var limitText = from[(limitIdx + " LIMIT ".Length)..];
            if (int.TryParse(limitText, out var lim))
            {
                shape = new SelectStageShape("*", source, Limit: lim);
                return true;
            }
        }

        return false;
    }

    private TerminalTopK? TryExtractTerminalTopK(string finalSource)
    {
        var idx = _stages.GetStageIndex(finalSource);
        if (idx < 0)
        {
            return null;
        }

        var terminal = _stages.Ctes[idx];
        var parsed = ParseSelectShape(terminal.Sql);
        if (parsed is null || parsed.OrderBy is null || parsed.Limit is null || parsed.Projection != "*")
        {
            return null;
        }

        _stages.RemoveStageAt(idx);
        return new TerminalTopK(
            parsed.Source,
            parsed.OrderBy,
            parsed.Limit.Value);
    }

    internal TerminalOrder? TryExtractTerminalOrder(string finalSource)
    {
        var idx = _stages.GetStageIndex(finalSource);
        if (idx < 0)
        {
            return null;
        }

        var terminal = _stages.Ctes[idx];
        var parsed = ParseSelectShape(terminal.Sql);
        if (parsed is null || parsed.OrderBy is null || parsed.Limit is not null || parsed.Projection != "*")
        {
            return null;
        }

        _stages.RemoveStageAt(idx);
        return new TerminalOrder(parsed.Source, parsed.OrderBy);
    }

    internal TerminalLimit? TryExtractTerminalLimit(string finalSource)
    {
        var idx = _stages.GetStageIndex(finalSource);
        if (idx < 0)
        {
            return null;
        }

        var terminal = _stages.Ctes[idx];
        var match = TerminalLimitRegex().Match(terminal.Sql);
        if (!match.Success)
        {
            return null;
        }

        _stages.RemoveStageAt(idx);
        return new TerminalLimit(
            match.Groups["source"].Value,
            int.Parse(match.Groups["limit"].Value));
    }

    internal void TryInlineFinalStage(ref string finalSource, ref string? columns)
    {
        if (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal))
        {
            return;
        }

        var currentSource = finalSource;
        var idx = _stages.GetStageIndex(currentSource);
        finalSource = currentSource;
        if (idx < 0)
        {
            return;
        }

        var cte = _stages.Ctes[idx];
        var m = FinalStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        // Inline only when this final stage is not referenced by any other CTE.
        var refs = _stages.CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        columns = m.Groups["proj"].Value;
        var source = m.Groups["source"].Value;
        var groupBy = m.Groups["group"].Value;
        finalSource = string.IsNullOrWhiteSpace(groupBy) ? source : $"{source}{groupBy}";

        _stages.RemoveStageAt(idx);
    }

    internal void TryInlineSingleUseFilterStageIntoProjection(ref string finalSource, ref string? columns)
    {
        if (columns is null || string.Equals(columns, "*", StringComparison.Ordinal))
        {
            return;
        }

        // We cannot use ref string in FindIndex, so we need to capture the index and the filter stage name separately here.
        // Do not inline currentSource.
        var currentSource = finalSource;
        var idx = _stages.GetStageIndex(currentSource);
        finalSource = currentSource;
        if (idx < 0)
        {
            return;
        }

        var cte = _stages.Ctes[idx];
        var m = FilterStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        var refs = _stages.CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        var source = m.Groups["source"].Value;
        var predicate = m.Groups["pred"].Value;

        var sourceIdx = _stages.GetStageIndex(source);
        if (sourceIdx >= 0)
        {
            var sourceCte = _stages.Ctes[sourceIdx];
            var sourceFilter = FilterStageInlineRegex().Match(sourceCte.Sql);
            if (sourceFilter.Success)
            {
                var sourceRefs = _stages.CountStageReferences(sourceCte.Name);
                if (sourceRefs == 1)
                {
                    source = sourceFilter.Groups["source"].Value;
                    var sourcePredicate = sourceFilter.Groups["pred"].Value;
                    predicate = $"({sourcePredicate}) AND ({predicate})";
                    _stages.RemoveStageAt(sourceIdx);

                    if (sourceIdx < idx)
                    {
                        idx--;
                    }
                }
            }
        }

        finalSource = $"{source} WHERE {predicate}";
        _stages.RemoveStageAt(idx);
    }

    internal void TryInlinePassThroughBaseStage(ref string finalSource)
    {
        var whereIdx = finalSource.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase);
        if (whereIdx <= 0)
        {
            return;
        }

        var sourceToken = finalSource[..whereIdx].Trim();
        var predicateTail = finalSource[whereIdx..];
        var idx = _stages.GetStageIndex(sourceToken);
        if (idx < 0)
        {
            return;
        }

        var cte = _stages.Ctes[idx];
        var match = TrivialSourceStageRegex().Match(cte.Sql);
        if (!match.Success)
        {
            return;
        }

        if (_stages.CountStageReferences(cte.Name) != 0)
        {
            return;
        }

        finalSource = $"{match.Groups["source"].Value}{predicateTail}";
        _stages.RemoveStageAt(idx);
    }

    internal void TryInlineSingleUseAggregateStageIntoTerminalTopK(
        ref TerminalTopK? terminalTopK,
        ref string? columns)
    {
        if (terminalTopK is null || (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal)))
        {
            return;
        }

        var sourceStage = terminalTopK.Source;
        var idx = _stages.GetStageIndex(sourceStage);
        if (idx < 0)
        {
            return;
        }

        var cte = _stages.Ctes[idx];
        var m = FinalStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        var groupBy = m.Groups["group"].Value;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return; // rule is intentionally scoped to aggregate shapes
        }

        var refs = _stages.CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        columns = m.Groups["proj"].Value;
        terminalTopK = terminalTopK with
        {
            Source = $"{m.Groups["source"].Value}{groupBy}"
        };

        _stages.RemoveStageAt(idx);
    }

    internal void TryInlineSingleUseAggregateStageIntoTerminalOrder(
        ref TerminalOrder? terminalOrder,
        ref string? columns)
    {
        if (terminalOrder is null || (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal)))
        {
            return;
        }

        var sourceStage = terminalOrder.Source;
        var idx = _stages.GetStageIndex(sourceStage);
        if (idx < 0)
        {
            return;
        }

        var cte = _stages.Ctes[idx];
        var m = FinalStageInlineRegex().Match(cte.Sql);
        if (!m.Success)
        {
            return;
        }

        var groupBy = m.Groups["group"].Value;
        if (string.IsNullOrWhiteSpace(groupBy))
        {
            return;
        }

        var refs = _stages.CountStageReferences(cte.Name);
        if (refs != 0)
        {
            return;
        }

        columns = m.Groups["proj"].Value;
        terminalOrder = terminalOrder with
        {
            Source = $"{m.Groups["source"].Value}{groupBy}"
        };

        _stages.RemoveStageAt(idx);
    }

    internal void TryInlineSingleUseAggregateFilterStageIntoTerminalOrder(
        ref TerminalOrder? terminalOrder,
        ref string? columns)
    {
        if (terminalOrder is null || (columns is not null && !string.Equals(columns, "*", StringComparison.Ordinal)))
        {
            return;
        }

        var terminalOrderSource = terminalOrder.Source;
        var filterIdx = _stages.GetStageIndex(terminalOrderSource);
        if (filterIdx < 0)
        {
            return;
        }

        var filterCte = _stages.Ctes[filterIdx];
        var filterMatch = FilterStageInlineRegex().Match(filterCte.Sql);
        if (!filterMatch.Success)
        {
            return;
        }

        var aggregateStageName = filterMatch.Groups["source"].Value;
        var aggregateIdx = _stages.GetStageIndex(aggregateStageName);
        if (aggregateIdx < 0)
        {
            return;
        }

        var aggregateCte = _stages.Ctes[aggregateIdx];
        var aggregateMatch = FinalStageInlineRegex().Match(aggregateCte.Sql);
        if (!aggregateMatch.Success || string.IsNullOrWhiteSpace(aggregateMatch.Groups["group"].Value))
        {
            return;
        }

        if (_stages.CountStageReferences(filterCte.Name) != 0 || _stages.CountStageReferences(aggregateCte.Name) != 1)
        {
            return;
        }

        var projection = aggregateMatch.Groups["proj"].Value;
        var predicate = RewriteAggregateAliasPredicate(
            filterMatch.Groups["pred"].Value,
            projection);
        var aggregateSource = aggregateMatch.Groups["source"].Value;
        var aggregateGroupBy = aggregateMatch.Groups["group"].Value;

        // Fold a single-use pre-aggregate filter stage into the aggregate source
        // so optimized output becomes FROM base WHERE ... GROUP BY ... HAVING ...
        // instead of retaining a leading WITH filter CTE.
        var preFilterIdx = _stages.GetStageIndex(aggregateSource);
        if (preFilterIdx >= 0)
        {
            var preFilterCte = _stages.Ctes[preFilterIdx];
            var preFilterMatch = FilterStageInlineRegex().Match(preFilterCte.Sql);
            if (preFilterMatch.Success && _stages.CountStageReferences(preFilterCte.Name) == 1)
            {
                aggregateSource = $"{preFilterMatch.Groups["source"].Value} WHERE {preFilterMatch.Groups["pred"].Value}";
                _stages.RemoveStageAt(preFilterIdx);

                if (preFilterIdx < filterIdx)
                {
                    filterIdx--;
                }
                if (preFilterIdx < aggregateIdx)
                {
                    aggregateIdx--;
                }
            }
        }

        columns = projection;
        terminalOrder = terminalOrder with
        {
            Source = $"{aggregateSource}{aggregateGroupBy} HAVING {predicate}"
        };

        // Remove the higher index first so the lower index stays valid. Preserve
        // the existing specialized-collapse stats behavior: one cache invalidation
        // and no individual stage-remove increments.
        if (filterIdx > aggregateIdx)
        {
            _stages.RemoveStagesAtWithoutTracking(filterIdx, aggregateIdx);
        }
        else
        {
            _stages.RemoveStagesAtWithoutTracking(aggregateIdx, filterIdx);
        }
    }

    private static string RewriteAggregateAliasPredicate(string predicate, string projection)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ParseAliasedProjectionItems(projection))
        {
            map[item.Alias] = item.Expression;
        }

        var rewritten = predicate;
        foreach (var (alias, expr) in map)
        {
            rewritten = Regex.Replace(
                rewritten,
                $@"\b{Regex.Escape(alias)}\b",
                expr,
                RegexOptions.IgnoreCase);
        }

        return rewritten;
    }

    private static IReadOnlyList<(string Alias, string Expression)> ParseAliasedProjectionItems(string projection)
    {
        var items = new List<(string Alias, string Expression)>();
        var chunks = SplitTopLevelByComma(projection);
        foreach (var chunk in chunks)
        {
            var candidate = chunk.Trim();
            if (candidate.Length == 0)
            {
                continue;
            }

            var asIndex = candidate.LastIndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex <= 0)
            {
                continue;
            }

            var expr = candidate[..asIndex].Trim();
            var alias = candidate[(asIndex + 4)..].Trim();
            if (!IsSimpleSqlIdentifier(alias))
            {
                continue;
            }

            items.Add((alias, expr));
        }

        return items;
    }

    private static List<string> SplitTopLevelByComma(string text)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var inSingle = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\'' && (i == 0 || text[i - 1] != '\\'))
            {
                inSingle = !inSingle;
            }

            if (inSingle)
            {
                continue;
            }

            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')' && depth > 0)
            {
                depth--;
            }
            else if (ch == ',' && depth == 0)
            {
                parts.Add(text[start..i]);
                start = i + 1;
            }
        }

        parts.Add(text[start..]);
        return parts;
    }

    private static bool IsSimpleSqlIdentifier(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        if (!(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_'))
            {
                return false;
            }
        }

        return true;
    }
}