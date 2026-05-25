namespace Hunting.Core.Translation;

using Catalog;
using Kusto.Language;
using Kusto.Language.Syntax;
using Policy;
using QueryModel;

/// <summary>
/// Translates analyzed Kusto AST into a RelNode intermediate query model.
///
/// Pipeline: KQL string → Kusto.Language ParseAndAnalyze → this translator → RelNode tree
///
/// IMPORTANT: Kusto.Language uses FilterOperator (not WhereOperator) for the
/// KQL 'where' keyword. The class name follows the internal AST naming, not
/// the KQL keyword. Similarly, TopOperator has flat properties (Expression,
/// ByExpression) rather than a ByClause wrapper.
/// </summary>
public sealed class KustoToRelational
{
    private readonly ApprovedViewCatalog _catalog;
    private readonly DiagnosticBag _diagnostics;

    public KustoToRelational(ApprovedViewCatalog catalog, DiagnosticBag diagnostics)
    {
        _catalog = catalog;
        _diagnostics = diagnostics;
    }

    public RelNode? Translate(string kql)
    {
        if (string.IsNullOrWhiteSpace(kql))
        {
            _diagnostics.AddError(DiagnosticPhase.Parse, "KQL input is empty.");
            return null;
        }

        var globals = _catalog.BuildGlobalState();
        var code = KustoCode.ParseAndAnalyze(kql, globals);

        foreach (var diag in code.GetDiagnostics())
        {
            if (diag.Severity == Kusto.Language.DiagnosticSeverity.Error)
            {
                _diagnostics.AddError(DiagnosticPhase.Parse,
                    diag.Message, diag.Code, diag.Start, diag.Length);
            }
        }

        // Note: do not short-circuit translation here. Keep parse diagnostics in the bag
        // but continue to translation so policy checks can run (e.g., unapproved table names).

        var statements = code.Syntax.GetDescendants<Statement>()
            .Where(s => IsTopLevel(s, code.Syntax))
            .ToList();

        if (statements.Count == 0)
        {
            _diagnostics.AddError(DiagnosticPhase.Parse, "No statement found in KQL input.");
            return null;
        }

        if (statements.Count >= 2 && statements[0] is LetStatement)
        {
            return TranslateLetChain(statements);
        }

        return TranslateStatement(statements[^1]);
    }

    private static bool IsTopLevel(SyntaxNode node, SyntaxNode root)
    {
        var p = node.Parent;
        while (p is not null && p != root)
        {
            if (p is Statement)
            {
                return false;
            }

            p = p.Parent;
        }
        return true;
    }

    // ─── Statement level ────────────────────────────────────────────

    private RelNode? TranslateStatement(SyntaxNode node) => node switch
    {
        ExpressionStatement es => TranslateExpression(es.Expression),
        LetStatement => Reject(node, "Standalone let statement without a following query expression."),
        _ => Reject(node, $"Unsupported statement type: {node.Kind}")
    };

    private RelNode? TranslateLetChain(List<Statement> statements)
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

    // ─── Expression level ───────────────────────────────────────────

    private RelNode? TranslateExpression(Expression expr) => expr switch
    {
        PipeExpression pipe => TranslatePipe(pipe),
        NameReference name => TranslateTableRef(name),
        PathExpression path => TranslateTableRefExpression(path),
        FunctionCallExpression fn => TranslateTabularFunction(fn),
        ParenthesizedExpression paren => TranslateExpression(paren.Expression),
        _ => Reject(expr, $"Unsupported tabular expression: {expr.Kind}")
    };

    private RelNode? TryTranslateExpression(Expression expr)
    {
        try { return TranslateExpression(expr); }
        catch (NotSupportedException) { return null; }
        // Do NOT catch broader exceptions here. A NullReferenceException or
        // InvalidOperationException inside TranslateExpression is a translator bug,
        // not an unsupported construct, and must propagate.
    }

    private RelNode? TranslateTableRefExpression(PathExpression path)
    {
        // PathExpression represents dotted identifiers like schema.table or db.schema.table.
        var parts = path.GetDescendants<NameReference>().Select(n => n.SimpleName).ToList();
        if (parts.Count == 0)
        {
            return Reject(path, "Empty path expression");
        }

        var tableName = parts.Count == 1 ? parts[0] : parts[1];
        // For now accept schema-qualified names like internal.secret_table — policy checks will reject unapproved schemas.
        if (!_catalog.IsApproved(tableName))
        {
            var sanitizedTableName = parts.Count == 1 ? tableName : string.Join('.', parts);
            _diagnostics.AddError(DiagnosticPhase.Policy,
                $"Table '{sanitizedTableName}' is not an approved hunting view. Only main.* views are queryable.");
            return null;
        }

        return new ScanNode(tableName);
    }

    private RelNode? TranslatePipe(PipeExpression pipe)
    {
        var input = TranslateExpression(pipe.Expression);
        if (input is null)
        {
            return null;
        }

        return TranslateOperator(pipe.Operator, input);
    }

    private RelNode? TranslateTableRef(NameReference name)
    {
        var tableName = name.SimpleName;
        if (!_catalog.IsApproved(tableName))
        {
            _diagnostics.AddError(DiagnosticPhase.Policy,
                $"Table '{tableName}' is not an approved hunting view. Only main.* views are queryable.");
            return null;
        }
        return new ScanNode(tableName);
    }

    private RelNode? TranslateTabularFunction(FunctionCallExpression fn)
    {
        var name = fn.Name.SimpleName;
        _diagnostics.AddError(DiagnosticPhase.Translate,
            $"Tabular function '{name}' is not supported as a query source.");
        return null;
    }

    // ─── Tabular operators ──────────────────────────────────────────
    // NOTE: Kusto.Language class name is FilterOperator, not WhereOperator.

    private RelNode? TranslateOperator(QueryOperator op, RelNode input) => op switch
    {
        FilterOperator filter => TranslateFilter(filter, input),
        ProjectOperator proj => TranslateProject(proj, input),
        ExtendOperator ext => TranslateExtend(ext, input),
        SummarizeOperator summ => TranslateSummarize(summ, input),
        SortOperator sort => TranslateSort(sort, input),
        TakeOperator take => TranslateTake(take, input),
        TopOperator top => TranslateTop(top, input),
        DistinctOperator dist => TranslateDistinct(dist, input),
        CountOperator => TranslateCount(input),
        JoinOperator join => TranslateJoin(join, input),
        SerializeOperator => input,
        _ => Reject(op, $"Unsupported operator: {op.Kind}")
    };

    private RelNode TranslateFilter(FilterOperator filter, RelNode input)
    {
        var predicate = TranslateScalar(filter.Condition);
        return new FilterNode(input, predicate);
    }

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
            projections.Add(TranslateProjectionExpr(UnwrapSeparated(elem)));
        }

        return new ProjectNode(input, projections);
    }

    private RelNode TranslateExtend(ExtendOperator ext, RelNode input)
    {
        var extensions = new List<ProjectionExpr>();
        foreach (var elem in ext.Expressions)
        {
            extensions.Add(TranslateProjectionExpr(UnwrapSeparated(elem)));
        }

        return new ExtendNode(input, extensions);
    }

    private RelNode TranslateSummarize(SummarizeOperator summ, RelNode input)
    {
        var aggregates = new List<ProjectionExpr>();
        foreach (var elem in summ.Aggregates)
        {
            aggregates.Add(TranslateProjectionExpr(UnwrapSeparated(elem)));
        }

        var groupBy = new List<ScalarExpr>();
        if (summ.ByClause is SummarizeByClause byClause)
        {
            foreach (var elem in byClause.Expressions)
            {
                groupBy.Add(TranslateScalar(UnwrapSeparated(elem)));
            }
        }

        return new AggregateNode(input, aggregates, groupBy);
    }

    private RelNode TranslateSort(SortOperator sort, RelNode input)
    {
        var sorts = new List<SortExpr>();
        foreach (var elem in sort.Expressions)
        {
            sorts.Add(TranslateSortExpr(UnwrapSeparated(elem)));
        }

        return new SortNode(input, sorts);
    }

    private RelNode TranslateTake(TakeOperator take, RelNode input)
    {
        var count = GetIntLiteral(take.Expression);
        return new LimitNode(input, count);
    }

    private RelNode TranslateTop(TopOperator top, RelNode input)
    {
        // TopOperator has flat structure: Expression (count) + ByExpression (sort expr)
        var count = GetIntLiteral(top.Expression);
        var sorts = new List<SortExpr> { TranslateSortExpr(top.ByExpression) };
        return new LimitNode(new SortNode(input, sorts), count);
    }

    private RelNode TranslateDistinct(DistinctOperator dist, RelNode input)
    {
        var projections = new List<ProjectionExpr>();
        foreach (var elem in dist.Expressions)
        {
            projections.Add(TranslateProjectionExpr(UnwrapSeparated(elem)));
        }

        return new DistinctNode(input, projections);
    }

    private RelNode TranslateCount(RelNode input) => new AggregateNode(
            input,
            Aggregates: [new ProjectionExpr("Count", new FunctionCall("count", []))],
            GroupBy: []);

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
            case "leftsemi" or "semi": kind = JoinKind.LeftSemi; break;
            case "leftanti" or "anti": kind = JoinKind.LeftAnti; break;
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
        var onClause = join.GetDescendants<JoinOnClause>().FirstOrDefault();
        ScalarExpr predicate;

        if (onClause is not null)
        {
            // KQL join on ColName means: left.ColName = right.ColName
            // For MVP: bare column names in the on-list produce equality pairs.
            var parts = new List<ScalarExpr>();
            foreach (var elem in onClause.Expressions)
            {
                var condition = UnwrapSeparated(elem);
                parts.Add(TranslateJoinCondition(condition));
            }

            predicate = parts.Count switch
            {
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

        return new JoinNode(input, right, kind, predicate);
    }

    /// <summary>
    /// Translate a single join condition element.
    /// KQL: join on DeviceName → left.DeviceName = right.DeviceName
    /// KQL: join on $left.A == $right.B → A = B (already a binary expression)
    /// </summary>
    private ScalarExpr TranslateJoinCondition(Expression condition)
    {
        // Bare column name: DeviceName → left.DeviceName = right.DeviceName
        if (condition is NameReference name)
        {
            var col = new ColumnRef(name.SimpleName);
            return new BinaryScalar(col, ScalarBinaryOp.Eq, col);
        }

        // $left.Col == $right.Col: MemberAccess or qualified name → translate normally
        // For now fall through to general scalar translation
        return TranslateScalarExpr(condition);
    }

    // ─── Scalar expressions ─────────────────────────────────────────

    private ScalarExpr TranslateScalar(SyntaxElement elem)
    {
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

    private ScalarExpr TranslateScalarExpr(Expression expr)
    {
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

    private ScalarExpr TranslateScalarCore(Expression expr) => expr switch
    {
        InExpression inExpr => TranslateInExpression(inExpr),
        LiteralExpression lit => TranslateLiteral(lit),
        NameReference name => new ColumnRef(name.SimpleName),
        CompoundNamedExpression cne => TranslateScalarExpr(cne.Expression),
        BinaryExpression bin => TranslateBinaryScalar(bin),
        PrefixUnaryExpression un => TranslateUnaryScalar(un),
        FunctionCallExpression fn => TranslateFunctionCall(fn),
        ParenthesizedExpression paren => TranslateScalarExpr(paren.Expression),
        SimpleNamedExpression named => TranslateScalarExpr(named.Expression),
        _ => throw new NotSupportedException(
            $"Unsupported scalar expression: {expr.Kind} ({expr.GetType().Name})")
    };

    private ScalarExpr? TryTranslateScalar(Expression expr)
    {
        try { return TranslateScalarCore(expr); }
        catch (NotSupportedException) { return null; }
        // Do NOT catch broader exceptions. Unexpected exceptions are translator bugs.
    }

    private static ScalarExpr TranslateLiteral(LiteralExpression lit) => lit.Kind switch
    {
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
        _ => new LiteralScalar(lit.LiteralValue, LiteralKind.String)
    };

    private ScalarExpr TranslateBinaryScalar(BinaryExpression bin)
    {
        var left = TranslateScalarExpr(bin.Left);
        var right = TranslateScalarExpr(bin.Right);

        var op = bin.Kind switch
        {
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

    private ScalarExpr TranslateInExpression(Expression expr)
    {
        var children = Enumerable.Range(0, expr.ChildCount)
            .Select(expr.GetChild)
            .ToList();

        var leftNode = children.OfType<Expression>().FirstOrDefault()
            ?? throw new NotSupportedException("IN expression is missing a left operand");

        var left = TranslateScalarExpr(leftNode);

        var listNode = children
            .FirstOrDefault(c => c.GetType().Name == "ExpressionList")
            ?? throw new NotSupportedException("IN expression requires a parenthesized expression list");

        var directItems = ExtractDirectExpressionListItems(listNode).ToList();

        if (directItems.Count == 0)
        {
            throw new NotSupportedException("IN expression has an empty or unsupported expression list");
        }

        var items = directItems
            .Select(TranslateScalarExpr)
            .ToList();

        return new BinaryScalar(left, ScalarBinaryOp.In, new ListScalar(items));
    }

    private static IReadOnlyList<Expression> ExtractDirectExpressionListItems(SyntaxElement listNode)
    {
        var result = new List<Expression>();

        for (var i = 0; i < listNode.ChildCount; i++)
        {
            var child = listNode.GetChild(i);

            switch (child)
            {
                case Expression expr:
                    result.Add(expr);
                    break;

                case SeparatedElement<Expression> separated:
                    result.Add(separated.Element);
                    break;

                case SyntaxList<SeparatedElement<Expression>> list:
                    foreach (var separated in list)
                    {
                        result.Add(separated.Element);
                    }
                    break;
            }
        }

        return result;
    }

    private ScalarExpr TranslateUnaryScalar(PrefixUnaryExpression un)
    {
        var operand = TranslateScalarExpr(un.Expression);
        // KQL only has unary plus and minus as PrefixUnaryExpression.
        // KQL 'not(expr)' is a FunctionCallExpression handled in TranslateFunctionCall.
        var op = un.Kind switch
        {
            SyntaxKind.UnaryMinusExpression => ScalarUnaryOp.Negate,
            SyntaxKind.UnaryPlusExpression => ScalarUnaryOp.Negate, // +x is rarely used, treat as noop via negate(negate)
            _ => throw new NotSupportedException($"Unsupported unary operator: {un.Kind}")
        };
        return new UnaryScalar(op, operand);
    }

    private ScalarExpr TranslateFunctionCall(FunctionCallExpression fn)
    {
        var name = fn.Name.SimpleName;
        var args = new List<ScalarExpr>();
        foreach (var elem in fn.ArgumentList.Expressions)
        {
            args.Add(TranslateScalarExpr(UnwrapSeparated(elem)));
        }

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
            var sqlFn = name switch
            {
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

    // ─── Projection helpers ─────────────────────────────────────────

    private ProjectionExpr TranslateProjectionExpr(Expression expr)
    {
        if (expr is SimpleNamedExpression named)
        {
            var alias = named.Name.SimpleName;
            var value = TranslateScalarExpr(named.Expression);
            return new ProjectionExpr(alias, value);
        }

        var scalar = TranslateScalarExpr(expr);
        string name;
        if (scalar is ColumnRef col)
        {
            name = col.Name;
        }
        else if (expr is FunctionCallExpression fn)
        {
            // For unnamed aggregates like count(), KQL uses a default alias like 'count_'.
            // Derive a safe alias from the function name and append '_' to match KQL convention.
            name = fn.Name.SimpleName + "_";
        }
        else
        {
            // Fallback: use the expression text, but sanitize whitespace and quotes.
            name = expr.ToString().Replace("\"", "").Trim();
        }

        return new ProjectionExpr(name, scalar);
    }

    private SortExpr TranslateSortExpr(Expression expr)
    {
        // KQL default sort direction is DESC
        var direction = SortDirection.Desc;
        var colExpr = expr;

        if (expr is OrderedExpression ordered)
        {
            colExpr = ordered.Expression;
            // OrderedExpression.Ordering is an OrderingClause
            // OrderingClause constructor: (ascOrDesc token, nullsClause)
            var orderingClause = ordered.Ordering;
            if (orderingClause is not null)
            {
                // Access the asc/desc keyword token from the OrderingClause
                // The first child token determines direction
                var dirToken = orderingClause.GetFirstToken();
                if (dirToken is not null)
                {
                    direction = dirToken.Kind == SyntaxKind.AscKeyword
                        ? SortDirection.Asc
                        : SortDirection.Desc;
                }
            }
        }

        var scalar = TranslateScalarExpr(colExpr);
        return new SortExpr(scalar, direction);
    }

    // ─── Utilities ──────────────────────────────────────────────────
    private static Expression? UnwrapSeparated(SyntaxNode node)
    {
        // Kusto.Language wraps list members in SeparatedElement<T>.
        if (node is SeparatedElement<Expression> separated)
        {
            return separated.Element;
        }

        if (node is Expression expr)
        {
            return expr;
        }

        return null;
    }

    private int GetIntLiteral(Expression expr)
    {
        if (expr is LiteralExpression lit)
        {
            if (lit.LiteralValue is long l)
            {
                return (int)l;
            }

            if (lit.LiteralValue is int i)
            {
                return i;
            }

            if (lit.LiteralValue is double d)
            {
                return (int)d;
            }
        }
        if (expr is SimpleNamedExpression named)
        {
            return GetIntLiteral(named.Expression);
        }

        // Do not silently substitute 100. Reject with a diagnostic.
        _diagnostics.AddError(DiagnosticPhase.Translate,
            "take/top count must be a positive integer literal. " +
            "Variable or expression counts are not supported in MVP.",
            expr.ToString(), expr.TextStart, expr.Width);
        return -1; // sentinel — caller checks HasErrors before proceeding
    }

    private RelNode? Reject(SyntaxNode node, string reason)
    {
        _diagnostics.AddError(DiagnosticPhase.Translate, reason,
            $"{node.Kind} at position {node.TextStart}",
            node.TextStart, node.Width);
        return null;
    }
}