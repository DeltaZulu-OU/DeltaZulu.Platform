namespace DeltaZulu.Hunting.Tests.Planning;

using DeltaZulu.Hunting.Core.Planning;
using DeltaZulu.Hunting.Core.QueryModel;

[TestClass]
public sealed class RelationalPlannerTests
{
    [TestMethod]
    public void Plan_Disabled_ReturnsInputUnchanged()
    {
        var planner = new RelationalPlanner();
        RelNode node = new ScanNode("ProcessEvent");

        var planned = planner.Plan(node, new PlannerContext(Enabled: false));

        Assert.AreSame(node, planned);
    }

    [TestMethod]
    public void IdentityProjectOverProject_CollapsesToInnerProject()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                    new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))
                ]),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ScanNode>(outer.Input);
        Assert.HasCount(2, outer.Projections);
    }

    [TestMethod]
    public void FilterOverPassthroughProject_PushesDown()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]),
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var p = (ProjectNode)planned;
        Assert.IsInstanceOfType<FilterNode>(p.Input);
    }

    [TestMethod]
    public void FilterOverAliasedProject_DoesNotPushDown()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("Name", new ColumnRef("DeviceName"))]),
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<FilterNode>(planned);
    }

    [TestMethod]
    public void FilterUsingProjectedAlias_PushesDownWithRemap()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("Name", new ColumnRef("DeviceName"))]),
            new BinaryScalar(new ColumnRef("Name"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var project = (ProjectNode)planned;
        Assert.IsInstanceOfType<FilterNode>(project.Input);
        var filter = (FilterNode)project.Input;
        var pred = (BinaryScalar)filter.Predicate;
        Assert.IsInstanceOfType<ColumnRef>(pred.Left);
        Assert.AreEqual("DeviceName", ((ColumnRef)pred.Left).Name, "Predicate should be remapped to source column before pushdown");
    }

    [TestMethod]
    public void Plan_IsIdempotent_ForStableTree()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]),
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        var once = planner.Plan(node, new PlannerContext(Enabled: true));
        var twice = planner.Plan(once, new PlannerContext(Enabled: true));

        Assert.AreEqual(once, twice, "Planner should converge to fixed point and remain stable");
    }

    [TestMethod]
    public void FilterOverDuplicateAliasProject_DoesNotPushDown()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("Name", new ColumnRef("DeviceName")),
                    new ProjectionExpr("Name", new ColumnRef("Timestamp"))
                ]),
            new BinaryScalar(new ColumnRef("Name"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<FilterNode>(planned, "Ambiguous alias mapping must not be pushed down");
    }

    [TestMethod]
    public void PlannerContext_MaxIterations_AtLeastOnePass()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]),
            [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true, MaxIterations: 0));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ScanNode>(outer.Input);
    }

    [TestMethod]
    public void PlannerStats_AreCaptured_WhenEnabled()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]),
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        _ = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsNotNull(planner.LastRunStats);
        Assert.IsGreaterThanOrEqualTo(1, planner.LastRunStats!.Iterations);
        Assert.IsGreaterThanOrEqualTo(1, planner.LastRunStats.TotalRulesAttempted);
        Assert.IsFalse(planner.LastRunStats.HitRuleApplicationBudget);
        Assert.HasCount(6, planner.LastRunStats.PassStats, "Expected six registered planner passes");
    }

    [TestMethod]
    public void ProjectionPruning_Removes_Unused_ProjectColumns()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("A", new ColumnRef("DeviceName")),
                    new ProjectionExpr("B", new ColumnRef("Timestamp"))
                ]),
            [new ProjectionExpr("A", new ColumnRef("A"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var stats = planner.LastRunStats!;
        var prune = stats.PassStats.First(s => s.Name == "ProjectionPruningPass");
        Assert.IsGreaterThan(0, prune.Applied, "Projection pruning should apply at least one rewrite");
    }

    [TestMethod]
    public void ProjectionPruning_WithExtend_PreservesRequiredPassthroughInputColumns()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ExtendNode(
                new ProjectNode(
                    new ScanNode("ProcessEvent"),
                    [
                        new ProjectionExpr("A", new ColumnRef("DeviceName")),
                        new ProjectionExpr("B", new ColumnRef("Timestamp"))
                    ]),
                [new ProjectionExpr("X", new FunctionCall("tolower", [new ColumnRef("A")]))]),
            [new ProjectionExpr("A", new ColumnRef("A"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var outer = (ProjectNode)planned;
        var ext = (ExtendNode)outer.Input;
        var innerProj = (ProjectNode)ext.Input;

        Assert.Contains(p => p.Alias == "A", innerProj.Projections, "Required passthrough column A must be preserved");
    }

    [TestMethod]
    public void ProjectionPruning_DoesNotReduceVisibleProjectToSortKey()
    {
        var node =
            new LimitNode(
                new SortNode(
                    new ProjectNode(
                        new ScanNode("ProcessEvent"),
                        [
                            new ProjectionExpr("AccountName", new ColumnRef("AccountName")),
                        new ProjectionExpr("LaunchCount", new ColumnRef("LaunchCount")),
                        new ProjectionExpr("DeviceCount", new ColumnRef("DeviceCount"))
                        ]),
                    [new SortExpr(new ColumnRef("LaunchCount"), SortDirection.Desc)]),
                25);

        var planned = new RelationalPlanner().Plan(
            node,
            new PlannerContext(Enabled: true, MaxIterations: 3));

        var project = FindFirst<ProjectNode>(planned);

        Assert.IsNotNull(project);
        CollectionAssert.AreEqual(
            new[] { "AccountName", "LaunchCount", "DeviceCount" },
            project!.Projections.Select(p => p.Alias).ToArray());
    }

    [TestMethod]
    public void LookupJoin_ProjectColumns_AreBoundToJoinSides()
    {
        var planner = new RelationalPlanner();

        var leftAgg = new AggregateNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("LaunchCount", new FunctionCall("count", []))],
            [new ColumnRef("AccountName")]);
        var rightAgg = new AggregateNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("DeviceCount", new FunctionCall("dcount", [new ColumnRef("DeviceName")]))],
            [new ColumnRef("AccountName")]);
        var join = new JoinNode(
            leftAgg,
            rightAgg,
            JoinKind.LeftOuter,
            new BinaryScalar(new ColumnRef("AccountName", JoinSide.Left), ScalarBinaryOp.Eq, new ColumnRef("AccountName", JoinSide.Right)),
            JoinFlavor.Lookup);
        RelNode node = new ProjectNode(
            join,
            [
                new ProjectionExpr("AccountName", new ColumnRef("AccountName")),
                new ProjectionExpr("LaunchCount", new ColumnRef("LaunchCount")),
                new ProjectionExpr("DeviceCount", new ColumnRef("DeviceCount"))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var project = (ProjectNode)planned;
        Assert.AreEqual(JoinSide.Left, ((ColumnRef)project.Projections[0].Expression).Qualifier);
        Assert.AreEqual(JoinSide.Left, ((ColumnRef)project.Projections[1].Expression).Qualifier);
        Assert.AreEqual(JoinSide.Right, ((ColumnRef)project.Projections[2].Expression).Qualifier);
    }

    [TestMethod]
    public void CommonScalarHoist_ReusesDuplicateExpressions()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("X", new FunctionCall("tolower", [new ColumnRef("DeviceName")])),
                new ProjectionExpr("Y", new FunctionCall("tolower", [new ColumnRef("DeviceName")]))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ExtendNode>(outer.Input);
        Assert.IsInstanceOfType<ColumnRef>(outer.Projections[1].Expression, "Second duplicate should be rewritten to reference first computed alias");
    }

    [TestMethod]
    public void CommonScalarHoist_OnExtend_ReusesDuplicateExpressionsWithoutProjectInjection()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("X", new FunctionCall("tolower", [new ColumnRef("DeviceName")])) ,
                new ProjectionExpr("Y", new FunctionCall("tolower", [new ColumnRef("DeviceName")]))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ExtendNode>(planned);
        var ext = (ExtendNode)planned;
        Assert.HasCount(2, ext.Extensions);
        Assert.IsInstanceOfType<ColumnRef>(ext.Extensions[1].Expression);
        Assert.AreEqual("X", ((ColumnRef)ext.Extensions[1].Expression).Name);
    }

    [TestMethod]
    public void NonIdentityProject_IsPreserved()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]),
            [new ProjectionExpr("Ts", new ColumnRef("Timestamp"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ProjectNode>(outer.Input);
    }

    [TestMethod]
    [Description("Identity projection that reorders columns is not collapsed (would change output order)")]
    public void IdentityProjectThatReorders_IsNotCollapsed()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("A", new ColumnRef("DeviceName")),
                    new ProjectionExpr("B", new ColumnRef("Timestamp")),
                    new ProjectionExpr("C", new ColumnRef("FileName"))
                ]),
            [
                new ProjectionExpr("C", new ColumnRef("C")),
                new ProjectionExpr("B", new ColumnRef("B")),
                new ProjectionExpr("A", new ColumnRef("A"))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        // Collapsing would discard the outer C,B,A ordering and emit A,B,C. The
        // outer projection must survive as a distinct node preserving its order.
        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ProjectNode>(outer.Input);
        Assert.AreEqual("C", outer.Projections[0].Alias);
        Assert.AreEqual("B", outer.Projections[1].Alias);
        Assert.AreEqual("A", outer.Projections[2].Alias);
    }

    [TestMethod]
    [Description("Window functions with different partitions are not deduplicated as a common subexpression")]
    public void CommonScalarHoist_DistinctWindowPartitions_NotMerged()
    {
        var planner = new RelationalPlanner();

        var byDevice = new WindowScalarExpr("row_number", [],
            new WindowSpec(PartitionBy: [new ColumnRef("DeviceName")], OrderBy: []));
        var byFile = new WindowScalarExpr("row_number", [],
            new WindowSpec(PartitionBy: [new ColumnRef("FileName")], OrderBy: []));

        RelNode node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("X", byDevice),
                new ProjectionExpr("Y", byFile)
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<WindowScalarExpr>(outer.Projections[1].Expression,
            "Distinct window partitions must not be merged — their ScalarKey must differ");
    }

    [TestMethod]
    [Description("Hoisted first occurrence references the extended column rather than recomputing the expression")]
    public void CommonScalarHoist_FirstOccurrence_ReferencesHoistedColumn()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("X", new FunctionCall("tolower", [new ColumnRef("DeviceName")])),
                new ProjectionExpr("Y", new FunctionCall("tolower", [new ColumnRef("DeviceName")]))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var outer = (ProjectNode)planned;
        var ext = (ExtendNode)outer.Input;

        // The expression is computed once in the ExtendNode...
        Assert.IsInstanceOfType<FunctionCall>(ext.Extensions[0].Expression);
        // ...and both outer projections reference the hoisted column, not re-evaluate it.
        Assert.IsInstanceOfType<ColumnRef>(outer.Projections[0].Expression,
            "First occurrence must reference the hoisted column rather than recompute the expression");
        Assert.IsInstanceOfType<ColumnRef>(outer.Projections[1].Expression);
    }

    [TestMethod]
    [Description("Projection pruning matches required columns case-insensitively (KQL semantics)")]
    public void ProjectionPruning_CaseInsensitiveColumnMatch_KeepsReferencedColumn()
    {
        var planner = new RelationalPlanner();

        // A downstream sort references 'devicename' (lowercase); the projection
        // alias is 'DeviceName'. KQL treats them as the same column, so DeviceName
        // must survive pruning while the unreferenced FileName is dropped.
        RelNode node = new SortNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("DeviceName", new ColumnRef("DeviceName")),
                    new ProjectionExpr("FileName", new ColumnRef("FileName"))
                ]),
            [new SortExpr(new ColumnRef("devicename"), SortDirection.Desc)]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var sort = (SortNode)planned;
        var proj = (ProjectNode)sort.Input;
        Assert.Contains(p => p.Alias == "DeviceName", proj.Projections,
            "The case-insensitively referenced column must not be pruned");
    }

    [TestMethod]
    [Description("Function names are case-insensitive and should be treated as equivalent for common scalar hoisting")]
    public void CommonScalarHoist_FunctionNameCaseInsensitive_Deduplicates()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("X", new FunctionCall("tolower", [new ColumnRef("DeviceName")])) ,
                new ProjectionExpr("Y", new FunctionCall("ToLower", [new ColumnRef("DeviceName")]))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ExtendNode>(outer.Input);
        Assert.IsInstanceOfType<ColumnRef>(outer.Projections[1].Expression,
            "Function name casing differences should not block common subexpression hoisting");
    }

    [TestMethod]
    [Description("Planner enforces a global rule-application budget as a control against runaway optimization")]
    public void PlannerContext_MaxRuleApplications_StopsEarlyAndSetsBudgetFlag()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [
                    new ProjectionExpr("A", new ColumnRef("DeviceName")),
                    new ProjectionExpr("B", new ColumnRef("Timestamp"))
                ]),
            [new ProjectionExpr("A", new ColumnRef("A"))]);

        _ = planner.Plan(node, new PlannerContext(Enabled: true, MaxIterations: 10, MaxRuleApplications: 1));

        Assert.IsNotNull(planner.LastRunStats);
        Assert.IsTrue(planner.LastRunStats!.HitRuleApplicationBudget,
            "A tight rule budget should stop optimization and be observable in telemetry");
    }

    [TestMethod]
    [Description("Identity projection collapse should treat column aliases case-insensitively")]
    public void IdentityProjectionCollapse_CaseOnlyAliasDifference_Collapses()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("ProcessEvent"),
                [new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]),
            [new ProjectionExpr("devicename", new ColumnRef("DeviceName"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var project = (ProjectNode)planned;
        Assert.IsInstanceOfType<ScanNode>(project.Input, "Outer identity projection should collapse to inner project");
        Assert.AreEqual("DeviceName", project.Projections[0].Alias);
    }

    [TestMethod]
    [Description("A computed column consumed only by the following filter is inlined and its extend dropped")]
    public void FilterExtendInline_DeadComputedColumn_InlinedIntoFilter()
    {
        var planner = new RelationalPlanner();

        // ProcessEvent
        //   | extend CommandUrlEncoded = url_encode(ProcessCommandLine)
        //   | extend HasEncodedCmd = indexof(CommandUrlEncoded, "%2Denc") >= 0
        //   | where HasEncodedCmd
        //   | project Timestamp, CommandUrlEncoded
        var encoded = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("CommandUrlEncoded", new FunctionCall("url_encode", [new ColumnRef("ProcessCommandLine")]))]);
        var flag = new ExtendNode(
            encoded,
            [new ProjectionExpr("HasEncodedCmd", new BinaryScalar(
                new FunctionCall("indexof", [new ColumnRef("CommandUrlEncoded"), new LiteralScalar("%2Denc", LiteralKind.String)]),
                ScalarBinaryOp.Gte,
                new LiteralScalar(0L, LiteralKind.Long)))]);
        RelNode node = new ProjectNode(
            new FilterNode(flag, new ColumnRef("HasEncodedCmd")),
            [
                new ProjectionExpr("Timestamp", new ColumnRef("Timestamp")),
                new ProjectionExpr("CommandUrlEncoded", new ColumnRef("CommandUrlEncoded"))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var project = (ProjectNode)planned;
        var filter = (FilterNode)project.Input;
        Assert.IsInstanceOfType<BinaryScalar>(filter.Predicate,
            "The boolean flag column must be inlined as its defining expression, not left as a column reference");

        // The throwaway HasEncodedCmd extend is gone; the still-needed
        // CommandUrlEncoded extend survives.
        var ext = (ExtendNode)filter.Input;
        Assert.HasCount(1, ext.Extensions);
        Assert.AreEqual("CommandUrlEncoded", ext.Extensions[0].Alias);

        var stats = planner.LastRunStats!;
        var pass = stats.PassStats.First(s => s.Name == "FilterExtendInlinePass");
        Assert.IsGreaterThan(0, pass.Applied, "Inline pass should apply at least one rewrite");
    }

    [TestMethod]
    [Description("A computed column kept in the projection output is not inlined away")]
    public void FilterExtendInline_ColumnUsedDownstream_NotInlined()
    {
        var planner = new RelationalPlanner();

        // The flag is both filtered on AND projected, so it is live above the
        // filter and must remain materialized.
        var flag = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [new ProjectionExpr("HasEncodedCmd", new BinaryScalar(
                new ColumnRef("ProcessId"), ScalarBinaryOp.Gt, new LiteralScalar(0L, LiteralKind.Long)))]);
        RelNode node = new ProjectNode(
            new FilterNode(flag, new ColumnRef("HasEncodedCmd")),
            [new ProjectionExpr("HasEncodedCmd", new ColumnRef("HasEncodedCmd"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var project = (ProjectNode)planned;
        var filter = (FilterNode)project.Input;
        Assert.IsInstanceOfType<ColumnRef>(filter.Predicate,
            "A column still consumed downstream must not be inlined into the filter");
        Assert.IsInstanceOfType<ExtendNode>(filter.Input);
    }

    [TestMethod]
    [Description("A computed column referenced by a sibling extension is not inlined/dropped")]
    public void FilterExtendInline_SiblingDependency_NotInlined()
    {
        var planner = new RelationalPlanner();

        // A is consumed only by the filter, but sibling B = upper(A) depends on it
        // and B is projected — dropping A would break B.
        var ext = new ExtendNode(
            new ScanNode("ProcessEvent"),
            [
                new ProjectionExpr("A", new FunctionCall("lower", [new ColumnRef("FileName")])),
                new ProjectionExpr("B", new FunctionCall("upper", [new ColumnRef("A")]))
            ]);
        RelNode node = new ProjectNode(
            new FilterNode(ext, new ColumnRef("A")),
            [new ProjectionExpr("B", new ColumnRef("B"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var project = (ProjectNode)planned;
        var filter = (FilterNode)project.Input;
        Assert.IsInstanceOfType<ColumnRef>(filter.Predicate,
            "A column a sibling extension depends on must not be inlined away");
        var kept = (ExtendNode)filter.Input;
        Assert.HasCount(2, kept.Extensions);
    }

    private static T? FindFirst<T>(RelNode node)
    where T : RelNode
    {
        if (node is T match)
        {
            return match;
        }

        return node switch
        {
            LimitNode n => FindFirst<T>(n.Input),
            SortNode n => FindFirst<T>(n.Input),
            ProjectNode n => FindFirst<T>(n.Input),
            FilterNode n => FindFirst<T>(n.Input),
            ExtendNode n => FindFirst<T>(n.Input),
            AggregateNode n => FindFirst<T>(n.Input),
            SampleNode n => FindFirst<T>(n.Input),
            JoinNode n => FindFirst<T>(n.Left) ?? FindFirst<T>(n.Right),
            LetBindingNode n => FindFirst<T>(n.Body),
            _ => null
        };
    }
}