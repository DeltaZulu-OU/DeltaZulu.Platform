namespace Hunting.Tests.Planning;

using Hunting.Core.Planning;
using Hunting.Core.QueryModel;

[TestClass]
public sealed class RelationalPlannerTests
{
    [TestMethod]
    public void Plan_Disabled_ReturnsInputUnchanged()
    {
        var planner = new RelationalPlanner();
        RelNode node = new ScanNode("DeviceProcessEvents");

        var planned = planner.Plan(node, new PlannerContext(Enabled: false));

        Assert.AreSame(node, planned);
    }

    [TestMethod]
    public void IdentityProjectOverProject_CollapsesToInnerProject()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("DeviceProcessEvents"),
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
        Assert.AreEqual(2, outer.Projections.Count);
    }


    [TestMethod]
    public void FilterOverPassthroughProject_PushesDown()
    {
        var planner = new RelationalPlanner();

        RelNode node = new FilterNode(
            new ProjectNode(
                new ScanNode("DeviceProcessEvents"),
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
                new ScanNode("DeviceProcessEvents"),
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
                new ScanNode("DeviceProcessEvents"),
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
                new ScanNode("DeviceProcessEvents"),
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
                new ScanNode("DeviceProcessEvents"),
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
                new ScanNode("DeviceProcessEvents"),
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
                new ScanNode("DeviceProcessEvents"),
                [new ProjectionExpr("DeviceName", new ColumnRef("DeviceName"))]),
            new BinaryScalar(new ColumnRef("DeviceName"), ScalarBinaryOp.Eq, new LiteralScalar("A", LiteralKind.String)));

        _ = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsNotNull(planner.LastRunStats);
        Assert.IsGreaterThanOrEqualTo(1, planner.LastRunStats!.Iterations);
        Assert.IsGreaterThanOrEqualTo(1, planner.LastRunStats.TotalRulesAttempted);
        Assert.AreEqual(4, planner.LastRunStats.PassStats.Count, "Expected four registered planner passes");
    }


    [TestMethod]
    public void ProjectionPruning_Removes_Unused_ProjectColumns()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("DeviceProcessEvents"),
                [
                    new ProjectionExpr("A", new ColumnRef("DeviceName")),
                    new ProjectionExpr("B", new ColumnRef("Timestamp"))
                ]),
            [new ProjectionExpr("A", new ColumnRef("A"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        var stats = planner.LastRunStats!;
        var prune = stats.PassStats.First(s => s.Name == "ProjectionPruningPass");
        Assert.IsTrue(prune.Applied > 0, "Projection pruning should apply at least one rewrite");
    }


    [TestMethod]
    public void ProjectionPruning_WithExtend_PreservesRequiredPassthroughInputColumns()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ExtendNode(
                new ProjectNode(
                    new ScanNode("DeviceProcessEvents"),
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

        Assert.IsTrue(innerProj.Projections.Any(p => p.Alias == "A"), "Required passthrough column A must be preserved");
    }

    [TestMethod]
    public void CommonScalarHoist_ReusesDuplicateExpressions()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ScanNode("DeviceProcessEvents"),
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
            new ScanNode("DeviceProcessEvents"),
            [
                new ProjectionExpr("X", new FunctionCall("tolower", [new ColumnRef("DeviceName")])) ,
                new ProjectionExpr("Y", new FunctionCall("tolower", [new ColumnRef("DeviceName")]))
            ]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ExtendNode>(planned);
        var ext = (ExtendNode)planned;
        Assert.AreEqual(2, ext.Extensions.Count);
        Assert.IsInstanceOfType<ColumnRef>(ext.Extensions[1].Expression);
        Assert.AreEqual("X", ((ColumnRef)ext.Extensions[1].Expression).Name);
    }

    [TestMethod]
    public void NonIdentityProject_IsPreserved()
    {
        var planner = new RelationalPlanner();

        RelNode node = new ProjectNode(
            new ProjectNode(
                new ScanNode("DeviceProcessEvents"),
                [new ProjectionExpr("Timestamp", new ColumnRef("Timestamp"))]),
            [new ProjectionExpr("Ts", new ColumnRef("Timestamp"))]);

        var planned = planner.Plan(node, new PlannerContext(Enabled: true));

        Assert.IsInstanceOfType<ProjectNode>(planned);
        var outer = (ProjectNode)planned;
        Assert.IsInstanceOfType<ProjectNode>(outer.Input);
    }
}
