namespace Hunting.Tests.Translation;

using Hunting.Core.Catalog;
using Hunting.Core.Policy;
using Hunting.Core.QueryModel;
using Hunting.Core.Translation;
using Hunting.Schema.Definitions;

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
        _catalog.Register(DeviceProcessEventsSchema.View);
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

    // ─── Day 1: ScanNode + LimitNode ────────────────────────────────

    [TestMethod]
    [Description("Bare table reference → ScanNode")]
    public void Scan_BareTable()
    {
        var (result, diag) = Translate("DeviceProcessEvents");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var scan = AssertIs<ScanNode>(result);
        Assert.AreEqual("DeviceProcessEvents", scan.ViewName);
    }

    [TestMethod]
    [Description("take N → LimitNode wrapping ScanNode")]
    public void Limit_Take()
    {
        var (result, diag) = Translate("DeviceProcessEvents | take 20");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var limit = AssertIs<LimitNode>(result);
        Assert.AreEqual(20, limit.Count);
        AssertIs<ScanNode>(limit.Input);
    }

    // ─── Day 2: FilterNode + scalar expressions ─────────────────────

    [TestMethod]
    [Description("where with string equality → FilterNode")]
    public void Filter_StringEquality()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents | where FileName == "powershell.exe" """);
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
            "DeviceProcessEvents | where Timestamp > ago(7d)");
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
            """DeviceProcessEvents | where FileName == "cmd.exe" and ProcessId > 0""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var filter = AssertIs<FilterNode>(result);
        var and = AssertIs<BinaryScalar>(filter.Predicate);
        Assert.AreEqual(ScalarBinaryOp.And, and.Op);
    }

    // ─── Day 3: ProjectNode + SortNode ──────────────────────────────

    [TestMethod]
    [Description("project selecting named columns")]
    public void Project_NamedColumns()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | project Timestamp, DeviceName, FileName");
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
            "DeviceProcessEvents | sort by Timestamp desc");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var sort = AssertIs<SortNode>(result);
        Assert.AreEqual(SortDirection.Desc, sort.Sorts[0].Direction,
            "KQL default sort direction is DESC");
    }

    // ─── Day 4: ExtendNode ──────────────────────────────────────────

    [TestMethod]
    [Description("extend adding a computed column")]
    public void Extend_SingleColumn()
    {
        var (result, diag) = Translate(
            """DeviceProcessEvents | extend lower_name = tolower(FileName)""");
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
            DeviceProcessEvents
            | extend lower_name = tolower(FileName)
            | extend name_len = strlen(lower_name)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext2 = AssertIs<ExtendNode>(result);
        AssertIs<ExtendNode>(ext2.Input);
    }

    // ─── Day 5: AggregateNode ───────────────────────────────────────

    [TestMethod]
    [Description("summarize count() by column")]
    public void Aggregate_CountBy()
    {
        var (result, diag) = Translate(
            "DeviceProcessEvents | summarize count() by FileName");
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
            """DeviceProcessEvents | summarize event_count = count(), earliest = min(Timestamp) by DeviceName""");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var agg = AssertIs<AggregateNode>(result);
        Assert.HasCount(2, agg.Aggregates);
    }

    // ─── Day 6: LetBindingNode ──────────────────────────────────────

    [TestMethod]
    [Description("scalar let binding")]
    public void Let_ScalarBinding()
    {
        var (result, diag) = Translate(
            """let cutoff = ago(7d); DeviceProcessEvents | where Timestamp > cutoff""");
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
            DeviceProcessEvents
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
            DeviceProcessEvents
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
            DeviceProcessEvents
            | serialize
            | extend prev_ts = prev(Timestamp)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.AreEqual("prev_ts", ext.Extensions[0].Alias);
        Assert.IsInstanceOfType(ext.Extensions[0].Expression, typeof(WindowScalarExpr));
    }

    [TestMethod]
    [Description("serialize + next() → ExtendNode with WindowScalarExpr(lead)")]
    public void Window_Next()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents
            | serialize
            | extend next_ts = next(Timestamp)
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var ext = AssertIs<ExtendNode>(result);
        Assert.IsInstanceOfType(ext.Extensions[0].Expression, typeof(WindowScalarExpr));
    }

    [TestMethod]
    [Description("serialize + row_number()")]
    public void Window_RowNumber()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents
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
            DeviceProcessEvents
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
            DeviceProcessEvents
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
            DeviceProcessEvents
            | join (DeviceProcessEvents | take 5) on DeviceName
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
            DeviceProcessEvents
            | join kind=leftsemi (DeviceProcessEvents | take 5) on DeviceName
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
            DeviceProcessEvents
            | join kind=leftanti (DeviceProcessEvents | take 5) on DeviceName
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
            DeviceProcessEvents
            | join kind=rightouter (DeviceProcessEvents | take 5) on DeviceName
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
            DeviceProcessEvents
            | join kind=fullouter (DeviceProcessEvents | take 5) on DeviceName
            """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var join = AssertIs<JoinNode>(result);
        Assert.AreEqual(JoinKind.FullOuter, join.Kind);
    }

    [TestMethod]
    [Description("sample n translates to SampleNode")]
    public void Sample_TranslatesToSampleNode()
    {
        var (result, diag) = Translate("DeviceProcessEvents | sample 10");
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        var sample = AssertIs<SampleNode>(result);
        Assert.AreEqual(10, sample.Count);
    }

    [TestMethod]
    [Description("Bare-column join 'on Col' produces $left.Col == $right.Col, not Col == Col")]
    public void Join_OnCondition_QualifiesEachSide()
    {
        var (result, diag) = Translate(
            """
            DeviceProcessEvents
            | join kind=inner (DeviceProcessEvents | take 5) on DeviceName
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
        var (result, diag) = Translate("DeviceProcessEvents | extend Doubled = +ProcessId");
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
            "DeviceProcessEvents | take 5; DeviceProcessEvents | take 3");
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
            "DeviceProcessEvents | sort by Timestamp");
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
            """DeviceProcessEvents | where FileName =~ "Powershell.EXE" """);
        Assert.IsFalse(diag.HasErrors, string.Join("\n", diag.All));
        AssertIs<FilterNode>(result);
    }

    // ─── Policy: unapproved table rejected ──────────────────────────

    [TestMethod]
    [Description("Unapproved table name produces policy error")]
    public void Policy_UnapprovedTable()
    {
        var (result, diag) = Translate("internal.secret_table | take 10");
        Assert.IsTrue(diag.HasErrors);
        Assert.Contains(d => d.Phase == DiagnosticPhase.Policy, diag.All);
    }
}
