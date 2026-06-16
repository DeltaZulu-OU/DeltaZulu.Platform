using DeltaZulu.Platform.Domain.Analytics.Catalog;
using DeltaZulu.Platform.Domain.Analytics.Policy;
using DeltaZulu.Platform.Domain.Analytics.QueryModel;
using Kusto.Language.Syntax;

namespace DeltaZulu.Platform.Application.Analytics.Translation;

/// <summary>
/// <para>Translates analyzed Kusto AST into a RelNode intermediate query model.</para>
/// <para>Pipeline: KQL string → Kusto.Language ParseAndAnalyze → this translator → RelNode tree</para>
/// <para>
/// IMPORTANT: Kusto.Language uses FilterOperator (not WhereOperator) for the
/// KQL 'where' keyword. The class name follows the internal AST naming, not
/// the KQL keyword. Similarly, TopOperator has flat properties (Expression,
/// ByExpression) rather than a ByClause wrapper.
/// </para>
/// </summary>
internal sealed class KustoQueryTranslator
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly DiagnosticBag _diagnostics;
    private readonly KustoQueryDocumentAnalyzer _documentAnalyzer;
    private readonly KustoProjectionTranslator _projectionTranslator;

    public KustoQueryTranslator(ApprovedViewCatalog catalog, DiagnosticBag diagnostics)
    {
        _catalog = catalog;
        _diagnostics = diagnostics;
        _documentAnalyzer = new KustoQueryDocumentAnalyzer(catalog, diagnostics);
        _projectionTranslator = new KustoProjectionTranslator(TranslateScalarExpr);
    }

    public RelNode? Translate(string kql)
    {
        var document = _documentAnalyzer.Analyze(kql);
        if (document is null)
        {
            return null;
        }

        var statements = document.Statements;

        if (statements.Count == 0)
        {
            _diagnostics.AddError(DiagnosticPhase.Parse, "No statement found in KQL input.");
            return null;
        }

        if (statements.Count >= 2)
        {
            var leading = statements.Take(statements.Count - 1).ToList();
            var final = statements[^1];

            if (!leading.All(s => s is LetStatement))
            {
                _diagnostics.AddError(
                    DiagnosticPhase.Policy,
                    "Only 'let' bindings may precede the final query expression. Multiple query statements are not supported.");

                return null;
            }

            if (!IsTranslatableQueryStatement(final))
            {
                _diagnostics.AddError(
                    DiagnosticPhase.Policy,
                    "A let chain must end with a query expression.");

                return null;
            }

            return TranslateLetChain(statements);
        }

        if (document.HasParseErrors)
        {
            return null;
        }

        if (!IsTranslatableQueryStatement(statements[0]))
        {
            _diagnostics.AddError(
                DiagnosticPhase.Policy,
                "Only query expressions are supported. Management commands and other statement types are not allowed.");

            return null;
        }

        return TranslateStatement(statements[0]);
    }

    private static bool IsTranslatableQueryStatement(Statement statement) => statement is ExpressionStatement;

    #region Statement level

    private RelNode? TranslateLetChain(IReadOnlyList<Statement> statements)
    {
        var lets = statements.OfType<LetStatement>().ToList();
        var finalStmt = statements.LastOrDefault(s => s is ExpressionStatement);

        if (finalStmt is null)
        {
            _diagnostics.AddError(DiagnosticPhase.Translate,
                "Let binding(s) found but no following query expression.");
            return null;
        }

        var body = TranslateStatement(finalStmt);
        if (body is null)
        {
            return null;
        }

        for (var i = lets.Count - 1; i >= 0; i--)
        {
            var let_ = lets[i];
            var name = let_.Name.SimpleName;
            var valueExpr = let_.Expression;

            var scalarExpr = TryTranslateScalar(valueExpr);
            if (scalarExpr is not null)
            {
                body = new LetBindingNode(name, TabularValue: null, ScalarValue: scalarExpr, Body: body);
                continue;
            }

            var tabNode = TryTranslateExpression(valueExpr);
            if (tabNode is not null)
            {
                body = new LetBindingNode(name, TabularValue: tabNode, ScalarValue: null, Body: body);
                continue;
            }

            _diagnostics.AddError(DiagnosticPhase.Translate,
                $"Could not translate let binding '{name}'.");
            return null;
        }

        return body;
    }

    private RelNode? TranslateStatement(SyntaxNode node) => node switch {
        ExpressionStatement es => TranslateExpression(es.Expression),
        LetStatement => Reject(node, "Standalone let statement without a following query expression."),
        _ => Reject(node, $"Unsupported statement type: {node.Kind}")
    };

    #endregion Statement level

    #region Expression level

    private RelNode? TranslateExpression(Expression expr) => expr switch {
        PipeExpression pipe => TranslatePipe(pipe),
        NameReference name => TranslateTableRef(name),
        PathExpression path => TranslateTableRefExpression(path),
        FunctionCallExpression fn => TranslateTabularFunction(fn),
        PrintOperator print => TranslatePrint(print),
        ParenthesizedExpression paren => TranslateExpression(paren.Expression),
        _ => Reject(expr, $"Unsupported tabular expression: {expr.Kind}")
    };

    private RelNode? TranslatePipe(PipeExpression pipe)
    {
        var input = TranslateExpression(pipe.Expression);
        if (input is null)
        {
            return null;
        }

        return TranslateOperator(pipe.Operator, input);
    }

    private RelNode? TranslatePrint(PrintOperator print)
    {
        var projections = new List<ProjectionExpr>();
        foreach (var elem in print.Expressions)
        {
            projections.Add(_projectionTranslator.TranslateProjectionExpr(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        if (projections.Count == 0)
        {
            _diagnostics.AddError(DiagnosticPhase.Translate, "print requires at least one expression.");
            return null;
        }

        return new ProjectNode(new SingletonRowNode(), projections);
    }

    private RelNode? TranslateTableRef(NameReference name)
    {
        var tableName = name.SimpleName;
        if (!_catalog.IsApproved(tableName))
        {
            _diagnostics.AddError(DiagnosticPhase.Policy,
                $"Table '{tableName}' is not an approved hunting view. Only golden.* views are queryable.");
            return null;
        }
        return new ScanNode(tableName);
    }

    private RelNode? TranslateTableRefExpression(PathExpression path)
    {
        var parts = KustoSyntaxHelpers.GetPathParts(path);
        if (parts.Count == 0)
        {
            return Reject(path, "Empty path expression");
        }

        if (!_documentAnalyzer.TableReferencePolicy.TryValidateTablePathQualifiers(parts, out var tableName))
        {
            return null;
        }

        if (!_catalog.IsApproved(tableName))
        {
            var sanitizedTableName = parts.Count == 1 ? tableName : string.Join('.', parts);
            _diagnostics.AddError(DiagnosticPhase.Policy,
                $"Table '{sanitizedTableName}' is not an approved hunting view. Only golden.* views are queryable.");
            return null;
        }

        return new ScanNode(tableName);
    }

    private RelNode? TranslateTabularFunction(FunctionCallExpression fn)
    {
        var name = fn.Name.SimpleName;
        if (name.Equals("print", StringComparison.OrdinalIgnoreCase))
        {
            var projections = new List<ProjectionExpr>();
            foreach (var elem in fn.ArgumentList.Expressions)
            {
                var expr = KustoSyntaxHelpers.UnwrapSeparated(elem);
                projections.Add(_projectionTranslator.TranslateProjectionExpr(expr));
            }

            if (projections.Count == 0)
            {
                _diagnostics.AddError(DiagnosticPhase.Translate, "print requires at least one expression.");
                return null;
            }

            return new ProjectNode(new SingletonRowNode(), projections);
        }

        _diagnostics.AddError(DiagnosticPhase.Translate,
            $"Tabular function '{name}' is not supported as a query source.");
        return null;
    }

    private RelNode? TryTranslateExpression(Expression expr)
    {
        try { return TranslateExpression(expr); }
        catch (NotSupportedException) { return null; }
        // Do NOT catch broader exceptions here. A NullReferenceException or
        // InvalidOperationException inside TranslateExpression is a translator bug,
        // not an unsupported construct, and must propagate.
    }

    #endregion Expression level

    #region Tabular operators

    // NOTE: Kusto.Language class name is FilterOperator, not WhereOperator.

    private RelNode TranslateCount(RelNode input) => new AggregateNode(
            input,
            Aggregates: [new ProjectionExpr("Count", new FunctionCall("count", []))],
            GroupBy: []);

    private RelNode TranslateDistinct(DistinctOperator dist, RelNode input)
    {
        var projections = new List<ProjectionExpr>();
        foreach (var elem in dist.Expressions)
        {
            projections.Add(_projectionTranslator.TranslateProjectionExpr(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        return new DistinctNode(input, projections);
    }

    private RelNode TranslateExtend(ExtendOperator ext, RelNode input)
    {
        var extensions = new List<ProjectionExpr>();
        foreach (var elem in ext.Expressions)
        {
            extensions.Add(_projectionTranslator.TranslateProjectionExpr(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        return new ExtendNode(input, extensions);
    }

    private RelNode TranslateFilter(FilterOperator filter, RelNode input)
    {
        var predicate = TranslateScalar(filter.Condition);
        return new FilterNode(input, predicate);
    }

    private RelNode? TranslateJoin(JoinOperator join, RelNode input)
    {
        // JoinOperator.Parameters is a SyntaxList<NamedParameter>
        var kindParam = join.Parameters
            .OfType<NamedParameter>()
            .FirstOrDefault(p => p.Name.SimpleName == "kind");

        if (kindParam is null)
        {
            _diagnostics.AddError(DiagnosticPhase.Policy,
                "Bare 'join' without 'kind=' is blocked. KQL default is 'innerunique' which " +
                "deduplicates the left side before joining. Use 'join kind=inner' explicitly.",
                "KQL_BARE_JOIN_BLOCKED");
            return null;
        }

        var kindStr = kindParam.Expression?.ToString()?.Trim()?.ToLowerInvariant();
        if (kindStr is null or "innerunique")
        {
            _diagnostics.AddError(DiagnosticPhase.Policy,
                $"Join kind '{kindStr ?? "innerunique"}' is not supported. " +
                "KQL innerunique deduplicates the left side before joining, which has no " +
                "direct SQL equivalent. Use 'kind=inner' for standard inner join semantics.",
                "KQL_JOIN_KIND_INNERUNIQUE");
            return null;
        }

        JoinKind kind;
        switch (kindStr)
        {
            case "inner": kind = JoinKind.Inner; break;
            case "leftouter": kind = JoinKind.LeftOuter; break;
            case "rightouter": kind = JoinKind.RightOuter; break;
            case "fullouter": kind = JoinKind.FullOuter; break;
            case "leftsemi" or "semi": kind = JoinKind.LeftSemi; break;
            case "leftanti" or "anti": kind = JoinKind.LeftAnti; break;
            case "rightsemi": kind = JoinKind.RightSemi; break;
            case "rightanti": kind = JoinKind.RightAnti; break;
            default:
                _diagnostics.AddError(DiagnosticPhase.Policy,
                    $"Join kind '{kindStr}' is not supported in MVP.",
                    $"KQL_JOIN_KIND_{kindStr.ToUpperInvariant()}");
                return null;
        }

        var right = TranslateExpression(join.Expression);
        if (right is null)
        {
            return null;
        }

        // JoinOperator does NOT expose a .Condition property on its generated class —
        // confirmed by CS1061. The on/where clause must be accessed via GetDescendants<T>.
        var onClauses = join.GetDescendants<JoinOnClause>();
        var onClause = onClauses.Count > 0 ? onClauses[0] : null;
        ScalarExpr predicate;

        if (onClause is not null)
        {
            // KQL join on ColName means: left.ColName = right.ColName
            // For MVP: bare column names in the on-list produce equality pairs.
            var parts = new List<ScalarExpr>();
            foreach (var elem in onClause.Expressions)
            {
                var condition = KustoSyntaxHelpers.UnwrapSeparated(elem);
                parts.Add(TranslateJoinCondition(condition));
            }

            predicate = parts.Count switch {
                0 => new LiteralScalar(true, LiteralKind.Bool),
                1 => parts[0],
                _ => parts.Skip(1).Aggregate(parts[0],
                    (acc, p) => new BinaryScalar(acc, ScalarBinaryOp.And, p))
            };
        }
        else
        {
            // No on-clause: join with no condition is not safe in MVP
            _diagnostics.AddError(DiagnosticPhase.Translate,
                "Join has no 'on' clause. An explicit on condition is required.",
                "KQL_JOIN_NO_CONDITION");
            return null;
        }

        return new JoinNode(input, right, kind, predicate, JoinFlavor.GenericJoin);
    }

    /// <summary>
    /// Translate a single join condition element.
    /// KQL: join on DeviceName → left.DeviceName = right.DeviceName
    /// KQL: join on $left.A == $right.B → A = B (already a binary expression)
    /// </summary>
    private ScalarExpr TranslateJoinCondition(Expression? condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        // Bare column name: DeviceName → $left.DeviceName == $right.DeviceName.
        // The two sides must carry distinct join-side qualifiers; emitting the
        // same unqualified ColumnRef on both sides produces `Col = Col`, which is
        // either an ambiguous-column error or an always-true Cartesian join.
        if (condition is NameReference name)
        {
            var left = new ColumnRef(name.SimpleName, JoinSide.Left);
            var right = new ColumnRef(name.SimpleName, JoinSide.Right);
            return new BinaryScalar(left, ScalarBinaryOp.Eq, right);
        }

        // $left.Col == $right.Col: MemberAccess or qualified name → translate normally
        // For now fall through to general scalar translation
        return TranslateScalarExpr(condition);
    }

    private RelNode? TranslateLookup(LookupOperator lookup, RelNode input)
    {
        var right = TranslateExpression(lookup.Expression);
        if (right is null)
        {
            return null;
        }

        var onClauses = lookup.GetDescendants<JoinOnClause>();
        var onClause = onClauses.Count > 0 ? onClauses[0] : null;
        if (onClause is null)
        {
            _diagnostics.AddError(DiagnosticPhase.Translate,
                "lookup has no 'on' clause. An explicit on condition is required.",
                "KQL_LOOKUP_NO_CONDITION");
            return null;
        }

        var parts = new List<ScalarExpr>();
        foreach (var elem in onClause.Expressions)
        {
            parts.Add(TranslateJoinCondition(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        if (parts.Count == 0)
        {
            _diagnostics.AddError(DiagnosticPhase.Translate,
                "lookup has no 'on' clause. An explicit on condition is required.",
                "KQL_LOOKUP_NO_CONDITION");
            return null;
        }

        var predicate = parts.Count switch {
            1 => parts[0],
            _ => parts.Skip(1).Aggregate(parts[0],
                (acc, p) => new BinaryScalar(acc, ScalarBinaryOp.And, p))
        };

        return new JoinNode(input, right, JoinKind.LeftOuter, predicate, JoinFlavor.Lookup);
    }

    private RelNode? TranslateOperator(QueryOperator op, RelNode input) => op switch {
        FilterOperator filter => TranslateFilter(filter, input),
        ProjectOperator proj => TranslateProject(proj, input),
        ExtendOperator ext => TranslateExtend(ext, input),
        SummarizeOperator summ => TranslateSummarize(summ, input),
        SortOperator sort => TranslateSort(sort, input),
        TakeOperator take => TranslateTake(take, input),
        SampleOperator sample => TranslateSample(sample, input),
        SampleDistinctOperator sampleDistinct => TranslateSampleDistinct(sampleDistinct, input),
        TopOperator top => TranslateTop(top, input),
        DistinctOperator dist => TranslateDistinct(dist, input),
        CountOperator => TranslateCount(input),
        JoinOperator join => TranslateJoin(join, input),
        LookupOperator lookup => TranslateLookup(lookup, input),
        SerializeOperator => input,
        PrintOperator print => TranslatePrint(print),
        _ => Reject(op, $"Unsupported operator: {op.Kind}")
    };

    private RelNode? TranslateProject(ProjectOperator proj, RelNode input)
    {
        // Kusto 'project' requires at least one expression. Reject empty project.
        if (proj.Expressions is null || proj.Expressions.Count == 0)
        {
            _diagnostics.AddError(DiagnosticPhase.Translate,
                "project requires at least one expression.");
            return null;
        }

        var projections = new List<ProjectionExpr>();
        foreach (var elem in proj.Expressions)
        {
            projections.Add(_projectionTranslator.TranslateProjectionExpr(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        return new ProjectNode(input, projections);
    }

    private RelNode? TranslateSample(SampleOperator sample, RelNode input)
    {
        var count = KustoLiteralReader.GetIntLiteral(_diagnostics, sample.Expression);
        if (count < 0)
        {
            return null;
        }
        return new SampleNode(input, count);
    }

    private RelNode? TranslateSampleDistinct(SampleDistinctOperator sampleDistinct, RelNode input)
    {
        var count = KustoLiteralReader.GetIntLiteral(_diagnostics, sampleDistinct.Expression);
        if (count < 0)
        {
            return null;
        }

        var projection = new ProjectionExpr("sample_distinct_value", TranslateScalarExpr(sampleDistinct.OfExpression));
        var distinct = new DistinctNode(input, [projection]);
        return new SampleNode(distinct, count);
    }

    private RelNode TranslateSort(SortOperator sort, RelNode input)
    {
        var sorts = new List<SortExpr>();
        foreach (var elem in sort.Expressions)
        {
            sorts.Add(_projectionTranslator.TranslateSortExpr(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        return new SortNode(input, sorts);
    }

    private RelNode TranslateSummarize(SummarizeOperator summ, RelNode input)
    {
        var aggregates = new List<ProjectionExpr>();
        foreach (var elem in summ.Aggregates)
        {
            aggregates.Add(_projectionTranslator.TranslateProjectionExpr(KustoSyntaxHelpers.UnwrapSeparated(elem)));
        }

        var groupBy = new List<ScalarExpr>();
        if (summ.ByClause is SummarizeByClause byClause)
        {
            foreach (var elem in byClause.Expressions)
            {
                groupBy.Add(TranslateScalar(KustoSyntaxHelpers.UnwrapSeparated(elem)));
            }
        }

        return new AggregateNode(input, aggregates, groupBy);
    }

    private RelNode? TranslateTake(TakeOperator take, RelNode input)
    {
        var count = KustoLiteralReader.GetIntLiteral(_diagnostics, take.Expression);
        // GetIntLiteral returns -1 and adds a diagnostic when the count is not a
        // valid non-negative literal. Do not build a LimitNode(-1): DuckDB treats
        // LIMIT -1 as "no limit", which silently bypasses the row safety cap.
        if (count < 0)
        {
            return null;
        }
        return new LimitNode(input, count);
    }

    private RelNode? TranslateTop(TopOperator top, RelNode input)
    {
        // TopOperator has flat structure: Expression (count) + ByExpression (sort expr)
        var count = KustoLiteralReader.GetIntLiteral(_diagnostics, top.Expression);
        if (count < 0)
        {
            return null;
        }
        var sorts = new List<SortExpr> { _projectionTranslator.TranslateSortExpr(top.ByExpression) };
        return new LimitNode(new SortNode(input, sorts), count);
    }

    #endregion Tabular operators

    #region Scalar expressions

    private static ScalarExpr TranslateLiteral(LiteralExpression lit) => lit.Kind switch {
        SyntaxKind.StringLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.String),
        SyntaxKind.LongLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.Long),
        SyntaxKind.IntLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.Int),
        SyntaxKind.RealLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.Real),
        SyntaxKind.BooleanLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.Bool),
        SyntaxKind.NullLiteralExpression =>
            new LiteralScalar(null, LiteralKind.Null),
        SyntaxKind.DateTimeLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.DateTime),
        SyntaxKind.TimespanLiteralExpression =>
            new LiteralScalar(lit.LiteralValue, LiteralKind.Timespan),
        // Per architecture constraint: unsupported constructs are rejected, not
        // silently approximated. Promoting an unknown literal kind to String would
        // mistranslate values (e.g. guid/decimal literals) without warning.
        _ => throw new NotSupportedException(
            $"Unsupported literal kind: {lit.Kind}")
    };

    private static ScalarExpr TranslateTypeOfLiteral(TypeOfLiteralExpression typeOf) =>
        // Keep typeof(T) as a scalar literal so functions that accept an optional
        // type-literal argument (for example extract(..., typeof(string))) can be
        // represented in RelNode without producing a translator error.
        new LiteralScalar(typeOf.ToString(), LiteralKind.String);

    private ScalarExpr TranslateBinaryScalar(BinaryExpression bin)
    {
        var left = TranslateScalarExpr(bin.Left);
        var right = TranslateScalarExpr(bin.Right);

        var op = bin.Kind switch {
            SyntaxKind.EqualExpression => ScalarBinaryOp.Eq,
            SyntaxKind.NotEqualExpression => ScalarBinaryOp.Neq,
            SyntaxKind.LessThanExpression => ScalarBinaryOp.Lt,
            SyntaxKind.LessThanOrEqualExpression => ScalarBinaryOp.Lte,
            SyntaxKind.GreaterThanExpression => ScalarBinaryOp.Gt,
            SyntaxKind.GreaterThanOrEqualExpression => ScalarBinaryOp.Gte,
            SyntaxKind.AndExpression => ScalarBinaryOp.And,
            SyntaxKind.OrExpression => ScalarBinaryOp.Or,
            SyntaxKind.AddExpression => ScalarBinaryOp.Add,
            SyntaxKind.SubtractExpression => ScalarBinaryOp.Sub,
            SyntaxKind.MultiplyExpression => ScalarBinaryOp.Mul,
            SyntaxKind.DivideExpression => ScalarBinaryOp.Div,
            SyntaxKind.ModuloExpression => ScalarBinaryOp.Mod,
            // Case-insensitive equality
            SyntaxKind.EqualTildeExpression => ScalarBinaryOp.Eq,
            SyntaxKind.BangTildeExpression => ScalarBinaryOp.Neq,
            // String operators — case-insensitive (default)
            SyntaxKind.ContainsExpression => ScalarBinaryOp.Contains,
            SyntaxKind.NotContainsExpression => ScalarBinaryOp.NotContains,
            SyntaxKind.StartsWithExpression => ScalarBinaryOp.StartsWith,
            SyntaxKind.NotStartsWithExpression => ScalarBinaryOp.NotStartsWith,
            SyntaxKind.EndsWithExpression => ScalarBinaryOp.EndsWith,
            SyntaxKind.NotEndsWithExpression => ScalarBinaryOp.NotEndsWith,
            // String operators — case-sensitive (_cs variants)
            SyntaxKind.ContainsCsExpression => ScalarBinaryOp.ContainsCs,
            SyntaxKind.NotContainsCsExpression => ScalarBinaryOp.NotContainsCs,
            SyntaxKind.StartsWithCsExpression => ScalarBinaryOp.StartsWithCs,
            SyntaxKind.NotStartsWithCsExpression => ScalarBinaryOp.NotStartsWithCs,
            SyntaxKind.EndsWithCsExpression => ScalarBinaryOp.EndsWithCs,
            SyntaxKind.NotEndsWithCsExpression => ScalarBinaryOp.NotEndsWithCs,
            // In
            SyntaxKind.InExpression => ScalarBinaryOp.In,
            SyntaxKind.NotInExpression => ScalarBinaryOp.NotIn,
            // Has — word-boundary match (approximated via regex \b in DuckDB)
            SyntaxKind.HasExpression => ScalarBinaryOp.Has,
            SyntaxKind.NotHasExpression => ScalarBinaryOp.NotHas,
            SyntaxKind.HasCsExpression => ScalarBinaryOp.HasCs,
            SyntaxKind.NotHasCsExpression => ScalarBinaryOp.NotHasCs,
            // Has prefix/suffix — case-insensitive and case-sensitive variants
            SyntaxKind.HasPrefixExpression => ScalarBinaryOp.HasPrefix,
            SyntaxKind.NotHasPrefixExpression => ScalarBinaryOp.NotHasPrefix,
            SyntaxKind.HasPrefixCsExpression => ScalarBinaryOp.HasPrefixCs,
            SyntaxKind.NotHasPrefixCsExpression => ScalarBinaryOp.NotHasPrefixCs,
            SyntaxKind.HasSuffixExpression => ScalarBinaryOp.HasSuffix,
            SyntaxKind.NotHasSuffixExpression => ScalarBinaryOp.NotHasSuffix,
            SyntaxKind.HasSuffixCsExpression => ScalarBinaryOp.HasSuffixCs,
            SyntaxKind.NotHasSuffixCsExpression => ScalarBinaryOp.NotHasSuffixCs,
            // Regex — note: !matches regex does not exist as a SyntaxKind;
            // negation is wrapped in UnaryNotExpression by the parser
            SyntaxKind.MatchesRegexExpression => ScalarBinaryOp.MatchesRegex,
            _ => throw new NotSupportedException(
                $"Unsupported binary operator: {bin.Kind}")
        };

        // For =~ and !~, wrap both sides in tolower() for case-insensitive comparison
        if (bin.Kind is SyntaxKind.EqualTildeExpression or SyntaxKind.BangTildeExpression)
        {
            left = new FunctionCall("tolower", [left]);
            right = new FunctionCall("tolower", [right]);
        }

        if (op == ScalarBinaryOp.In || op == ScalarBinaryOp.NotIn)
        {
            // Expect right to be a ListScalar
            if (right is ListScalar list)
            {
                return new BinaryScalar(left, op, list);
            }
            // Fallback: use right as-is (may be a subquery or other expression)
        }

        return new BinaryScalar(left, op, right);
    }

    private ScalarExpr TranslateFunctionCall(FunctionCallExpression fn)
    {
        var name = fn.Name.SimpleName;
        var syntaxArgs = new List<Expression>();
        foreach (var elem in fn.ArgumentList.Expressions)
        {
            syntaxArgs.Add(KustoSyntaxHelpers.UnwrapSeparated(elem)
                ?? throw new NotSupportedException($"{name}() contains an unsupported argument shape."));
        }

        var args = syntaxArgs
            .Select(TranslateScalarExpr)
            .ToList();

        KustoFunctionArgumentValidator.Validate(name, args, syntaxArgs);

        // KQL 'not(expr)' is a FunctionCallExpression, not a PrefixUnaryExpression.
        // SyntaxKind.UnaryNotExpression does not exist — this is the correct path for NOT.
        if (name == "not" && args.Count == 1)
        {
            return new UnaryScalar(ScalarUnaryOp.Not, args[0]);
        }

        // prev/next → window scalar expressions
        if (name is "prev" or "next")
        {
            return new WindowScalarExpr(
                name == "prev" ? "lag" : "lead",
                args,
                new WindowSpec(PartitionBy: [], OrderBy: []));
        }

        if (name is "row_number" or "row_cumsum" or "row_rank_dense" or "row_rank_min")
        {
            var sqlFn = name switch {
                "row_number" => "row_number",
                "row_cumsum" => "sum",
                "row_rank_dense" => "dense_rank",
                "row_rank_min" => "rank",
                _ => name
            };

            var frame = name == "row_cumsum"
                ? new WindowFrame(WindowFrameType.Rows,
                    new WindowBound(WindowBoundKind.UnboundedPreceding),
                    new WindowBound(WindowBoundKind.CurrentRow))
                : null;

            return new WindowScalarExpr(sqlFn, args,
                new WindowSpec(PartitionBy: [], OrderBy: [], Frame: frame));
        }

        return new FunctionCall(name, args);
    }

    private ScalarExpr TranslateInExpression(Expression expr)
    {
        var children = Enumerable.Range(0, expr.ChildCount)
            .Select(expr.GetChild)
            .ToList();

        var leftNode = children.OfType<Expression>().FirstOrDefault()
            ?? throw new NotSupportedException("IN expression is missing a left operand");

        var left = TranslateScalarExpr(leftNode);

        var listNode = children
            .OfType<ExpressionList>()
            .FirstOrDefault()
            ?? throw new NotSupportedException("IN expression requires a parenthesized expression list");

        var directItems = KustoSyntaxHelpers.ExtractDirectExpressionListItems(listNode).ToList();

        if (directItems.Count == 0)
        {
            throw new NotSupportedException("IN expression has an empty or unsupported expression list");
        }

        var items = directItems
            .Select(TranslateScalarExpr)
            .ToList();

        return new BinaryScalar(left, ScalarBinaryOp.In, new ListScalar(items));
    }

    private ScalarExpr TranslateScalar(SyntaxElement? elem)
    {
        ArgumentNullException.ThrowIfNull(elem);

        if (elem is Expression expr)
        {
            return TranslateScalarExpr(expr);
        }

        // Fallback for non-expression nodes (e.g., JoinConditionClause)
        _diagnostics.AddError(DiagnosticPhase.Translate,
            $"Expected expression, got {elem.Kind}.",
            elem.ToString(), elem.TextStart, elem.Width);
        return new LiteralScalar(null, LiteralKind.Null);
    }

    private ScalarExpr TranslateScalarCore(Expression expr)
    {
        var between = TryTranslateBetweenExpression(expr);
        if (between is not null)
        {
            return between;
        }

        return expr switch {
            InExpression inExpr => TranslateInExpression(inExpr),
            LiteralExpression lit => TranslateLiteral(lit),
            NameReference name => new ColumnRef(name.SimpleName),
            CompoundNamedExpression cne => TranslateScalarExpr(cne.Expression),
            BinaryExpression bin => TranslateBinaryScalar(bin),
            PrefixUnaryExpression un => TranslateUnaryScalar(un),
            FunctionCallExpression fn => TranslateFunctionCall(fn),
            TypeOfLiteralExpression typeOf => TranslateTypeOfLiteral(typeOf),
            ParenthesizedExpression paren => TranslateScalarExpr(paren.Expression),
            SimpleNamedExpression named => TranslateScalarExpr(named.Expression),
            _ => throw new NotSupportedException(
                $"Unsupported scalar expression: {expr.Kind} ({expr.GetType().Name})")
        };
    }

    private ScalarExpr TranslateScalarExpr(Expression? expr)
    {
        ArgumentNullException.ThrowIfNull(expr);

        try
        {
            return TranslateScalarCore(expr);
        }
        catch (NotSupportedException ex)
        {
            _diagnostics.AddError(DiagnosticPhase.Translate,
                ex.Message, expr.ToString(), expr.TextStart, expr.Width);
            return new LiteralScalar(null, LiteralKind.Null);
        }
    }

    private ScalarExpr TranslateUnaryScalar(PrefixUnaryExpression un)
    {
        var operand = TranslateScalarExpr(un.Expression);
        // KQL only has unary plus and minus as PrefixUnaryExpression.
        // KQL 'not(expr)' is a FunctionCallExpression handled in TranslateFunctionCall.
        return un.Kind switch {
            SyntaxKind.UnaryMinusExpression => new UnaryScalar(ScalarUnaryOp.Negate, operand),
            // Unary plus is the identity operation: +x == x. Returning a Negate
            // here would flip the sign and silently corrupt the value.
            SyntaxKind.UnaryPlusExpression => operand,
            _ => throw new NotSupportedException($"Unsupported unary operator: {un.Kind}")
        };
    }

    private ScalarExpr? TryTranslateBetweenExpression(Expression expr)
    {
        var kind = expr.Kind.ToString();
        if (!kind.Contains("Between", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = expr.GetDescendants<Expression>().ToList();
        if (parts.Count < 3)
        {
            throw new NotSupportedException("between requires value, lower bound, and upper bound.");
        }

        var value = TranslateScalarExpr(parts[0]);
        var lower = TranslateScalarExpr(parts[1]);
        var upper = TranslateScalarExpr(parts[2]);

        var betweenExpr = new BinaryScalar(
            new BinaryScalar(value, ScalarBinaryOp.Gte, lower),
            ScalarBinaryOp.And,
            new BinaryScalar(value, ScalarBinaryOp.Lte, upper));

        return kind.StartsWith("Not", StringComparison.OrdinalIgnoreCase)
            ? new UnaryScalar(ScalarUnaryOp.Not, betweenExpr)
            : betweenExpr;
    }

    private ScalarExpr? TryTranslateScalar(Expression expr)
    {
        try { return TranslateScalarCore(expr); }
        catch (NotSupportedException) { return null; }
        // Do NOT catch broader exceptions. Unexpected exceptions are translator bugs.
    }

    #endregion Scalar expressions

    #region Utilities

    private RelNode? Reject(SyntaxNode node, string reason)
    {
        _diagnostics.AddError(DiagnosticPhase.Translate, reason,
            $"{node.Kind} at position {node.TextStart}",
            node.TextStart, node.Width);
        return null;
    }

    #endregion Utilities
}