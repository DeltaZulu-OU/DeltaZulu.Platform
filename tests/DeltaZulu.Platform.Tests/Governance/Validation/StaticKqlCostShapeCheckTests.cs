using DeltaZulu.Platform.Application.Governance.Validation.Checks;
using DeltaZulu.Platform.Domain.Governance.Contracts;
using DeltaZulu.Platform.Domain.Governance.Enums;
using DeltaZulu.Platform.Domain.Governance.Identifiers;

namespace DeltaZulu.Platform.Tests.Governance.Validation;

[TestClass]
public sealed class StaticKqlCostShapeCheckTests
{
    [TestMethod]
    public async Task BoundedQuery_PassesWithoutClusterAccess()
    {
        var outcome = await RunAsync("Events | where Timestamp > ago(1h) | sort by Timestamp | take 100");

        Assert.AreEqual(CheckStatus.Passed, outcome.Status);
    }

    [TestMethod]
    public async Task ExpensiveShapes_ReportStableRuleIds()
    {
        var outcome = await RunAsync("union Events* | mv-expand Items | sort by Timestamp");

        Assert.AreEqual(CheckStatus.Failed, outcome.Status);
        StringAssert.Contains(outcome.LogsExcerpt, "KQL001");
        StringAssert.Contains(outcome.LogsExcerpt, "KQL003");
        StringAssert.Contains(outcome.LogsExcerpt, "KQL004");
        StringAssert.Contains(outcome.LogsExcerpt, "KQL005");
    }

    [TestMethod]
    public async Task InlineDisable_SuppressesOnlyNamedRule()
    {
        var outcome = await RunAsync("// disable KQL005\nEvents | where Timestamp > ago(1h) | sort by Timestamp");

        Assert.AreEqual(CheckStatus.Passed, outcome.Status);
    }

    private static Task<CheckOutcome> RunAsync(string query)
    {
        var context = new CheckContext(
            ChangeRequestId.New(),
            "test-rule",
            WorkflowProfileId.StandardReview,
            [new DraftFileSnapshot("rule.kql", DraftContentType.AnalyticsQuery, query)]);
        return new StaticKqlCostShapeCheck().RunAsync(context);
    }
}
