namespace Hunting.Tests.Translation;

using Hunting.Core.Catalog;
using Hunting.Core.DuckDbSql;
using Hunting.Core.Policy;
using Hunting.Core.QueryModel;
using Hunting.Core.Translation;
using Hunting.Schema;

/// <summary>
/// Red-green-refactor harness for KQL → RelNode translation.
/// Each test specifies a KQL input and asserts the shape of the
/// resulting RelNode tree.
/// </summary>
[TestClass]
public sealed class KustoToRelationalTests
{
    private static ApprovedViewCatalog _catalog = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        _catalog = new ApprovedViewCatalog();
        SchemaConventions.RegisterCanonicalViews(_catalog);
    }

    private (RelNode? Node, DiagnosticBag Diag) Translate(string kql)
    {
        var diag = new DiagnosticBag();
        var translator = new KustoToRelational(_catalog, diag);
        var node = translator.Translate(kql);
        return (node, diag);
    }

    private static T AssertIs<T>(RelNode? node) where T : RelNode
    {
        Assert.IsNotNull(node, "RelNode was null");
        Assert.IsInstanceOfType(node, typeof(T),
            $"Expected {typeof(T).Name}, got {node!.GetType().Name}");
        return (T)node;
    }

    private static T AssertIs<T>(ScalarExpr? exp) where T : ScalarExpr
    {
        Assert.IsNotNull(exp, "ScalarExpr was null");
        Assert.IsInstanceOfType(exp, typeof(T),
            $"Expected {typeof(T).Name}, got {exp!.GetType().Name}");
        return (T)exp;
    }

    // ─── ScanNode + LimitNode ────────────────────────────────

    [TestMethod]
    [Description("Bare table reference → ScanNode")]
    public void Scan_BareTable()
    {
        var (result, diag) = Translate("ProcessEvent");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var scan = AssertIs<ScanNode>(result);
        Assert.AreEqual("ProcessEvent", scan.ViewName);
    }

    [TestMethod]
    [Description("take N → LimitNode wrapping ScanNode")]
    public void Limit_Take()
    {
        var (result, diag) = Translate("ProcessEvent | take 20");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var limit = AssertIs<LimitNode>(result);
        Assert.AreEqual(20, limit.Count);
        AssertIs<ScanNode>(limit.Input);
    }

    // ─── FilterNode + scalar expressions ─────────────────────

    [TestMethod]
    [Description("where with string equality → FilterNode")]
    public void Filter_StringEquality()
    {
        var (result, diag) = Translate(
            """ProcessEvent | where FileName == "powershell.exe" """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var filter = AssertIs<FilterNode>(result);
        var binary = AssertIs<BinaryScalar>(filter.Predicate);
        Assert.AreEqual(ScalarBinaryOp.Eq, binary.Op);
    }

    [TestMethod]
    [Description("where with ago() time comparison")]
    public void Filter_AgoComparison()
    {
        var (result, diag) = Translate(
            "ProcessEvent | where Timestamp > ago(7d)");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var filter = AssertIs<FilterNode>(result);
        var binary = AssertIs<BinaryScalar>(filter.Predicate);
        Assert.AreEqual(ScalarBinaryOp.Gt, binary.Op);
    }

    [TestMethod]
    [Description("where with compound predicate (and)")]
    public void Filter_CompoundAnd()
    {
        var (result, diag) = Translate(
            """ProcessEvent | where FileName == "cmd.exe" and ProcessId > 0""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var filter = AssertIs<FilterNode>(result);
        var and = AssertIs<BinaryScalar>(filter.Predicate);
        Assert.AreEqual(ScalarBinaryOp.And, and.Op);
    }

    // ─── ProjectNode + SortNode ──────────────────────────────

    [TestMethod]
    [Description("project selecting named columns")]
    public void Project_NamedColumns()
    {
        var (result, diag) = Translate(
            "ProcessEvent | project Timestamp, DeviceName, FileName");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var proj = AssertIs<ProjectNode>(result);
        Assert.HasCount(3, proj.Projections);
        Assert.AreEqual("Timestamp", proj.Projections[0].Alias);
    }

    [TestMethod]
    [Description("sort by with desc direction")]
    public void Sort_Desc()
    {
        var (result, diag) = Translate(
            "ProcessEvent | sort by Timestamp desc");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var sort = AssertIs<SortNode>(result);
        Assert.AreEqual(SortDirection.Desc, sort.Sorts[0].Direction,
            "KQL default sort direction is DESC");
    }

    // ─── ExtendNode ──────────────────────────────────────────

    [TestMethod]
    [Description("extend adding a computed column")]
    public void Extend_SingleColumn()
    {
        var (result, diag) = Translate(
            """ProcessEvent | extend lower_name = tolower(FileName)""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.HasCount(1, ext.Extensions);
        Assert.AreEqual("lower_name", ext.Extensions[0].Alias);
    }

    [TestMethod]
    [Description("chained extend — second extend wraps first")]
    public void Extend_ChainedReference()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | extend lower_name = tolower(FileName)
            | extend name_len = strlen(lower_name)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext2 = AssertIs<ExtendNode>(result);
        AssertIs<ExtendNode>(ext2.Input);
    }

    [TestMethod]
    [Description("extract() with 3 args translates as scalar function call")]
    public void Extend_Extract_WithOptionalTypeLiteralOmitted()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | extend EncodedPayload = extract("-enc\\s+([^\\s]+)", 1, ProcessCommandLine)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.AreEqual("EncodedPayload", ext.Extensions[0].Alias);
        var fn = AssertIs<FunctionCall>(ext.Extensions[0].Expression);
        Assert.AreEqual("extract", fn.Name, true);
        Assert.HasCount(3, fn.Args);
    }

    [TestMethod]
    [Description("extract() with verbatim regex string parses and translates")]
    public void Extend_Extract_WithVerbatimRegexString()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | extend EncodedPayload = extract(@"-enc\s+([^\s]+)", 1, ProcessCommandLine)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        var fn = AssertIs<FunctionCall>(ext.Extensions[0].Expression);
        Assert.AreEqual("extract", fn.Name, true);
        Assert.HasCount(3, fn.Args);
    }

    [TestMethod]
    [Description("extract() with 4 args translates as scalar function call")]
    public void Extend_Extract_WithTypeLiteralProvided()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | extend EncodedPayload = extract("-enc\\s+([^\\s]+)", 1, ProcessCommandLine, typeof(string))
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        var fn = AssertIs<FunctionCall>(ext.Extensions[0].Expression);
        Assert.AreEqual("extract", fn.Name, true);
        Assert.HasCount(4, fn.Args);
    }

    [TestMethod]
    [Description("Trivial function batch translates as scalar function calls with expected arity")]
    public void Extend_TrivialFunctionBatch_ValidArity()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | extend
                Arr = strcat_array(split(FileName, ","), ","),
                Keys = bag_keys(AdditionalFields),
                HasUser = bag_has_key(AdditionalFields, "User"),
                Merged = bag_merge(AdditionalFields, AdditionalFields),
                Len = array_length(split(FileName, ",")),
                E2 = exp2(3),
                E10 = exp10(2)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.HasCount(7, ext.Extensions);
    }

    
    [TestMethod]
    public void Filter_Between_Translates()
    {
        var (result, diag) = Translate("ProcessEvent | where ProcessId between (1 .. 5)");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var filter = AssertIs<FilterNode>(result);
        var and = AssertIs<BinaryScalar>(filter.Predicate);
        Assert.AreEqual(ScalarBinaryOp.And, and.Op);
    }

    [TestMethod]
    public void Print_TabularFunction_Translates()
    {
        var (result, diag) = Translate("print X=1, Y='a'");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var proj = AssertIs<ProjectNode>(result);
        Assert.IsInstanceOfType<SingletonRowNode>(proj.Input);
        Assert.AreEqual(2, proj.Projections.Count);
    }

    // ─── AggregateNode ───────────────────────────────────────

    [TestMethod]
    [Description("summarize count() by column")]
    public void Aggregate_CountBy()
    {
        var (result, diag) = Translate(
            "ProcessEvent | summarize count() by FileName");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var agg = AssertIs<AggregateNode>(result);
        Assert.HasCount(1, agg.Aggregates);
        Assert.HasCount(1, agg.GroupBy);
    }

    [TestMethod]
    [Description("summarize with multiple aggregates")]
    public void Aggregate_MultipleAggregates()
    {
        var (result, diag) = Translate(
            """ProcessEvent | summarize event_count = count(), earliest = min(Timestamp) by DeviceName""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var agg = AssertIs<AggregateNode>(result);
        Assert.HasCount(2, agg.Aggregates);
    }

    // ─── LetBindingNode ──────────────────────────────────────

    [TestMethod]
    [Description("scalar let binding")]
    public void Let_ScalarBinding()
    {
        var (result, diag) = Translate(
            """let cutoff = ago(7d); ProcessEvent | where Timestamp > cutoff""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var let_ = AssertIs<LetBindingNode>(result);
        Assert.AreEqual("cutoff", let_.Name);
        Assert.IsNotNull(let_.ScalarValue);
    }

    // ─── Composed pipelines ─────────────────────────────────────────

    [TestMethod]
    [Description("Full vertical slice: where + project + take")]
    public void Composed_VerticalSlice()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where FileName == "powershell.exe"
            | project Timestamp, DeviceName, ProcessCommandLine
            | take 20
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        // LimitNode → ProjectNode → FilterNode → ScanNode
        var limit = AssertIs<LimitNode>(result);
        var proj = AssertIs<ProjectNode>(limit.Input);
        var filter = AssertIs<FilterNode>(proj.Input);
        AssertIs<ScanNode>(filter.Input);
    }

    [TestMethod]
    [Description("Summarize pipeline: where + summarize + sort + take")]
    public void Composed_SummarizePipeline()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where Timestamp > ago(1d)
            | summarize count() by FileName
            | sort by count_ desc
            | take 10
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var limit = AssertIs<LimitNode>(result);
        var sort = AssertIs<SortNode>(limit.Input);
        var agg = AssertIs<AggregateNode>(sort.Input);
        AssertIs<FilterNode>(agg.Input);
    }

    // ─── Serialize / Window operators ──────────────────────────────

    [TestMethod]
    [Description("serialize + prev() → ExtendNode with WindowScalarExpr(lag)")]
    public void Window_Prev()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | serialize
            | extend prev_ts = prev(Timestamp)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.AreEqual("prev_ts", ext.Extensions[0].Alias);
        Assert.IsInstanceOfType<WindowScalarExpr>(ext.Extensions[0].Expression);
    }

    [TestMethod]
    [Description("serialize + next() → ExtendNode with WindowScalarExpr(lead)")]
    public void Window_Next()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | serialize
            | extend next_ts = next(Timestamp)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.IsInstanceOfType<WindowScalarExpr>(ext.Extensions[0].Expression);
    }

    [TestMethod]
    [Description("serialize + row_number()")]
    public void Window_RowNumber()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | serialize rn = row_number()
            """);
        // serialize with assignment may parse differently — accept extend or serialize form
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
    }

    [TestMethod]
    [Description("serialize + row_cumsum() → WindowScalarExpr with frame")]
    public void Window_RowCumsum()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | summarize event_count = count() by FileName
            | serialize
            | extend running_total = row_cumsum(event_count)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        var winExpr = AssertIs<WindowScalarExpr>(ext.Extensions[0].Expression);
        Assert.IsNotNull(winExpr.Window.Frame);
    }

    [TestMethod]
    [Description("Beaconing detection pattern")]
    public void Composed_BeaconingPattern()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where FileName == "beacon.exe"
            | sort by Timestamp asc
            | serialize
            | extend prev_ts = prev(Timestamp)
            | extend gap_seconds = datetime_diff('second', prev_ts, Timestamp)
            | project Timestamp, DeviceName, gap_seconds
            | take 100
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        Assert.IsNotNull(result);
        AssertIs<LimitNode>(result);
    }

    // ─── Spec-derived: join rejection ────────────────────────────────

    [TestMethod]
    [Description("Bare join (no kind=) must be REJECTED")]
    public void Join_BareDefault_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsTrue(diag.HasErrors, "Bare join should be rejected");
        Assert.Contains(d => d.Message.Contains("innerunique") || d.Message.Contains("Bare"), diag.All,
            "Diagnostic should mention innerunique or bare join semantics");
    }

    [TestMethod]
    [Description("join kind=leftsemi → JoinNode with LeftSemi")]
    public void Join_LeftSemi()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=leftsemi (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.LeftSemi, join.Kind);
    }

    [TestMethod]
    [Description("join kind=leftanti → JoinNode with LeftAnti")]
    public void Join_LeftAnti()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=leftanti (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.LeftAnti, join.Kind);
    }

    [TestMethod]
    [Description("join kind=rightouter → JoinNode with RightOuter")]
    public void Join_RightOuter()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=rightouter (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.RightOuter, join.Kind);
    }

    [TestMethod]
    [Description("join kind=fullouter → JoinNode with FullOuter")]
    public void Join_FullOuter()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=fullouter (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.FullOuter, join.Kind);
    }

    [TestMethod]
    [Description("join kind=rightsemi → JoinNode with RightSemi")]
    public void Join_RightSemi()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=rightsemi (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.RightSemi, join.Kind);
    }

    [TestMethod]
    [Description("join kind=rightanti → JoinNode with RightAnti")]
    public void Join_RightAnti()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=rightanti (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.RightAnti, join.Kind);
    }

    [TestMethod]
    [Description("lookup on Col translates to leftouter JoinNode")]
    public void Lookup_TranslatesToLeftOuterJoin()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | lookup (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.LeftOuter, join.Kind);
        Assert.AreEqual(JoinFlavor.Lookup, join.Flavor);
    }

    [TestMethod]
    [Description("lookup without on clause is rejected with diagnostic")]
    public void Lookup_WithoutOnClause_IsRejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | lookup (ProcessEvent | take 5)
            """);
        Assert.IsNull(result);
        Assert.IsTrue(diag.HasErrors, "lookup without on clause should fail");
        Assert.Contains(d =>
                string.Equals(d.DeveloperDetail, "KQL_LOOKUP_NO_CONDITION", StringComparison.OrdinalIgnoreCase) ||
                d.Message.Contains("Missing join on condition clause", StringComparison.OrdinalIgnoreCase), diag.All,
            string.Join("\n", diag.All));
    }

    [TestMethod]
    [Description("lookup on multiple columns combines predicates with AND")]
    public void Lookup_OnMultipleColumns_CombinesWithAnd()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | lookup (ProcessEvent | take 5) on DeviceName, AccountName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        var and = AssertIs<BinaryScalar>(join.OnPredicate);
        Assert.AreEqual(ScalarBinaryOp.And, and.Op);
    }

    [TestMethod]
    [Description("sample n translates to SampleNode")]
    public void Sample_TranslatesToSampleNode()
    {
        var (result, diag) = Translate("ProcessEvent | sample 10");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var sample = AssertIs<SampleNode>(result);
        Assert.AreEqual(10, sample.Count);
    }

    [TestMethod]
    [Description("sample with negative number is rejected")]
    public void Sample_Negative_Rejected()
    {
        var (result, diag) = Translate("ProcessEvent | sample -2");
        Assert.IsNull(result);
        Assert.IsTrue(diag.HasErrors, "sample with negative should produce error");
    }

    [TestMethod]
    [Description("sample-distinct n of Col translates to DistinctNode followed by SampleNode")]
    public void SampleDistinct_TranslatesToNode()
    {
        var (result, diag) = Translate("ProcessEvent | sample-distinct 5 of FileName");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var sample = AssertIs<SampleNode>(result);
        Assert.AreEqual(5, sample.Count);
        var distinct = AssertIs<DistinctNode>(sample.Input);
        Assert.HasCount(1, distinct.Projections);
        var col = AssertIs<ColumnRef>(distinct.Projections[0].Expression);
        Assert.AreEqual("FileName", col.Name);
    }

    [TestMethod]
    [Description("Bare-column join 'on Col' produces $left.Col == $right.Col, not Col == Col")]
    public void Join_OnCondition_QualifiesEachSide()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | join kind=inner (ProcessEvent | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        var predicate = AssertIs<BinaryScalar>(join.OnPredicate);
        var left = AssertIs<ColumnRef>(predicate.Left);
        var right = AssertIs<ColumnRef>(predicate.Right);
        Assert.AreEqual(JoinSide.Left, left.Qualifier, "left side must carry the $left qualifier");
        Assert.AreEqual(JoinSide.Right, right.Qualifier, "right side must carry the $right qualifier");
    }

    // ─── Unary operators ─────────────────────────────────────────────

    [TestMethod]
    [Description("Unary plus is identity: +x translates to x, not -x")]
    public void UnaryPlus_IsIdentity()
    {
        var (result, diag) = Translate("ProcessEvent | extend Doubled = +ProcessId");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        // +ProcessId must be the bare column reference, never a UnaryScalar(Negate).
        var col = AssertIs<ColumnRef>(ext.Extensions[0].Expression);
        Assert.AreEqual("ProcessId", col.Name);
    }

    // ─── Multi-statement routing ─────────────────────────────────────

    [TestMethod]
    [Description("Multiple query statements are rejected, not silently dropped")]
    public void MultipleQueryStatements_Rejected()
    {
        var (result, diag) = Translate(
            "ProcessEvent | take 5; ProcessEvent | take 3");
        Assert.IsTrue(diag.HasErrors,
            "Two query expressions should be rejected rather than silently dropping the first");
        Assert.IsNull(result);
    }

    // ─── Spec-derived: sort default direction ────────────────────

    [TestMethod]
    [Description("sort by Column (no direction) defaults to DESC in KQL")]
    public void Sort_DefaultDesc()
    {
        var (result, diag) = Translate(
            "ProcessEvent | sort by Timestamp");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var sort = AssertIs<SortNode>(result);
        Assert.AreEqual(SortDirection.Desc, sort.Sorts[0].Direction,
            "KQL default sort direction is DESC");
    }

    // ─── Spec-derived: case-insensitive equality ─────────────────

    [TestMethod]
    [Description("=~ case-insensitive equality")]
    public void Filter_CaseInsensitiveEq()
    {
        var (result, diag) = Translate(
            """ProcessEvent | where FileName =~ "Powershell.EXE" """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        AssertIs<FilterNode>(result);
    }

    // ─── Policy: unapproved table rejected ──────────────────────────

    [TestMethod]
    [Description("Unapproved table name produces parse error")]
    public void Parse_UnapprovedTable()
    {
        var (_, diag) = Translate("silver.secret_table | take 10");
        Assert.IsTrue(diag.HasErrors);
        AssertPolicyOrParseError(diag);
    }

    [TestMethod]
    [Description("Semicolon and dot-command text inside string literal should not be treated as mixed statements")]
    public void Policy_SemicolonDotInsideStringLiteral_NotMixedStatement()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where ProcessCommandLine contains "; .drop table silver.secret"
            | take 1
            """);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("Query followed by executable management dot-command must be rejected")]
    public void Policy_QueryFollowedByDotCommand_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where FileName == "cmd.exe";
            .drop table ProcessEvent
            """);

        Assert.IsNull(result);
        AssertPolicyError(diag);
    }

    [TestMethod]
    [Description("Management dot-command followed by query must be rejected")]
    public void Policy_DotCommandFollowedByQuery_Rejected()
    {
        var (result, diag) = Translate(
            """
            .drop table ProcessEvent;
            ProcessEvent
            | take 1
            """);

        Assert.IsNull(result);
        AssertPolicyError(diag);
    }

    [TestMethod]
    [Description("Single management dot-command must be rejected")]
    public void Policy_SingleDotCommand_Rejected()
    {
        var (result, diag) = Translate(
            """
            .show tables
            """);

        Assert.IsNull(result);
        AssertPolicyError(diag);
    }

    [TestMethod]
    [Description("Dot-command after newline without semicolon must be rejected if parser recognizes it as executable input")]
    public void Policy_QueryThenDotCommandOnNewLine_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | take 1
            .drop table ProcessEvent
            """);

        Assert.IsNull(result);
        AssertPolicyOrParseError(diag);
    }

    [TestMethod]
    [Description("Multiple query statements must be rejected rather than silently translating only one")]
    public void Policy_TwoQueryStatements_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent | take 1;
            NetworkSession | take 1
            """);

        Assert.IsNull(result);
        AssertDiagnosticContaining(diag, "Multiple query statements");
    }

    [TestMethod]
    [Description("Let bindings may precede the final query")]
    public void Policy_LetBindingThenQuery_Allowed()
    {
        var (result, diag) = Translate(
            """
            let TargetFile = "cmd.exe";
            ProcessEvent
            | where FileName == TargetFile
            | take 1
            """);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("Let binding followed by dot-command must be rejected")]
    public void Policy_LetBindingThenDotCommand_Rejected()
    {
        var (result, diag) = Translate(
            """
            let TargetFile = "cmd.exe";
            .drop table ProcessEvent
            """);

        Assert.IsNull(result);
        AssertPolicyError(diag);
    }

    [TestMethod]
    [Description("Dot-command text inside let string value must not trigger policy error")]
    public void Policy_DotCommandInsideLetString_Allowed()
    {
        var (result, diag) = Translate(
            """
            let Suspicious = "; .drop table ProcessEvent";
            ProcessEvent
            | where ProcessCommandLine contains Suspicious
            | take 1
            """);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("SQL-looking injection text inside string literal must remain inert")]
    public void Policy_SqlInjectionTextInsideStringLiteral_Allowed()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where ProcessCommandLine contains "'; DROP TABLE ProcessEvent; --"
            | take 1
            """);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("SQL comment marker inside string literal must remain inert")]
    public void Policy_SqlCommentMarkerInsideStringLiteral_Allowed()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where ProcessCommandLine contains "-- pretend SQL comment"
            | take 1
            """);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("Semicolon inside string literal must not split KQL statements")]
    public void Policy_SemicolonInsideStringLiteral_Allowed()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where ProcessCommandLine contains "cmd.exe; whoami; hostname"
            | take 1
            """);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("Single quote inside KQL string should be accepted as literal data")]
    public void Policy_SingleQuoteInsideStringLiteral_Allowed()
    {
        var kql = """
        ProcessEvent
        | where ProcessCommandLine contains "O'Reilly"
        | take 1
        """;

        var (result, diag) = Translate(kql);

        Assert.IsNotNull(result);
        AssertNoPolicyErrors(diag);
    }

    [TestMethod]
    [Description("Single quote inside string literal must be escaped when relational plan is emitted as SQL")]
    public void SqlEmitter_SingleQuoteInsideStringLiteral_Escaped()
    {
        var kql = """
        ProcessEvent
        | where ProcessCommandLine contains "O'Reilly"
        | take 1
        """;

        var (rel, diag) = Translate(kql);

        Assert.IsNotNull(rel);
        AssertNoPolicyErrors(diag);

        var sql = new DuckDbQueryEmitter().Emit(rel);

        Assert.Contains("O''Reilly", sql);
        AssertNoSecondSqlStatement(sql);
    }

    [TestMethod]
    [Description("Newline inside string literal must not create a second executable statement")]
    public void Policy_NewlineInsideStringLiteral_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where ProcessCommandLine contains @"first line
            .drop table ProcessEvent"
            | take 1
            """);

        Assert.IsNull(result);
        AssertPolicyError(diag);
    }

    [TestMethod]
    [Description("Unapproved table name should be rejected even if query syntax is otherwise valid")]
    public void Policy_UnapprovedTableName_Rejected()
    {
        var (result, diag) = Translate(
            """
            silver.secret
            | take 1
            """);

        Assert.IsNull(result);
        AssertPolicyOrParseError(diag);
    }

    [TestMethod]
    [Description("Table-name-like SQL injection must not be accepted as a table reference")]
    public void Policy_TableNameSqlInjection_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent; DROP TABLE ProcessEvent
            | take 1
            """);

        Assert.IsNull(result);
        AssertPolicyOrParseError(diag);
    }

    [TestMethod]
    [Description("Column-name-like SQL injection must not be accepted as a valid projected column")]
    public void Policy_ColumnNameSqlInjection_Rejected()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | project FileName, ["x); DROP TABLE ProcessEvent; --"]
            """);

        Assert.IsNull(result);
        AssertPolicyOrParseError(diag);
    }

    [TestMethod]
    [Description("Unsupported KQL command syntax must fail closed")]
    public void Policy_UnsupportedCommandSyntax_FailsClosed()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | evaluate pivot(FileName)
            """);

        Assert.IsNull(result);
        AssertPolicyOrTranslateError(diag);
    }

    [TestMethod]
    [Description("Malformed input that contains a management command must not translate a recovered partial query")]
    public void Policy_MalformedQueryWithDotCommand_FailsClosed()
    {
        var (result, diag) = Translate(
            """
            ProcessEvent
            | where FileName ==
            .drop table ProcessEvent
            """);

        Assert.IsNull(result);
        AssertPolicyOrParseError(diag);
    }

    private static void AssertPolicyError(DiagnosticBag diag) => Assert.Contains(
            d => d.Phase == DiagnosticPhase.Policy, diag.All,
            "Expected at least one policy diagnostic.");

    private static void AssertNoPolicyErrors(DiagnosticBag diag) => Assert.DoesNotContain(
            d => d.Phase == DiagnosticPhase.Policy, diag.All,
            "Expected no policy diagnostics.");

    private static void AssertPolicyOrParseError(DiagnosticBag diag) => Assert.Contains(
            d =>
                d.Phase == DiagnosticPhase.Policy ||
                d.Phase == DiagnosticPhase.Parse, diag.All,
            "Expected a policy or parse diagnostic.");

    private static void AssertPolicyOrTranslateError(DiagnosticBag diag) => Assert.Contains(
            d =>
                d.Phase == DiagnosticPhase.Policy ||
                d.Phase == DiagnosticPhase.Translate, diag.All,
            "Expected a policy or translation diagnostic.");

    private static void AssertDiagnosticContaining(DiagnosticBag diag, string text) => Assert.Contains(
            d => d.Message.Contains(text, StringComparison.OrdinalIgnoreCase), diag.All,
            $"Expected diagnostic containing: {text}");

    private static void AssertNoPolicyErrorContaining(DiagnosticBag diag, string text) => Assert.DoesNotContain(
            d =>
                d.Phase == DiagnosticPhase.Policy &&
                d.Message.Contains(text, StringComparison.OrdinalIgnoreCase), diag.All,
            $"Did not expect policy diagnostic containing: {text}");

    private static void AssertNoSecondSqlStatement(string sql)
    {
        var normalized = sql.Trim();

        if (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1];
        }

        Assert.IsFalse(
            normalized.Contains(';'),
            "Generated SQL must not contain multiple statements.");
    }
}
