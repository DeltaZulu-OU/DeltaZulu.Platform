using Workbench.Domain.Workflow;

namespace Workbench.Tests.Domain;

[TestClass]
public sealed class WorkflowProfileTests
{
    [TestMethod]
    public void QuickLab_HasNoGates()
    {
        var p = WorkflowProfile.For(WorkflowProfileId.QuickLab);

        Assert.AreEqual(WorkflowProfileId.QuickLab, p.Id);
        Assert.IsFalse(p.RequiresPassingChecks);
        Assert.IsFalse(p.RequiresApproval);
        Assert.IsFalse(p.RequiresNonAuthorApprover);
        Assert.IsFalse(p.ResetsApprovalOnContentEdit);
        Assert.IsFalse(p.BlocksStaleMerge);
        Assert.IsEmpty(p.RequiredBlockingCheckNames);
    }

    [TestMethod]
    public void ControlledReview_RequiresChecksApprovalNonAuthorAndBlocksStale()
    {
        var p = WorkflowProfile.For(WorkflowProfileId.ControlledReview);

        Assert.AreEqual(WorkflowProfileId.ControlledReview, p.Id);
        Assert.IsTrue(p.RequiresPassingChecks);
        Assert.IsTrue(p.RequiresApproval);
        Assert.IsTrue(p.RequiresNonAuthorApprover);
        Assert.IsTrue(p.ResetsApprovalOnContentEdit);
        Assert.IsTrue(p.BlocksStaleMerge);
        Assert.Contains("package-schema", p.RequiredBlockingCheckNames);
        Assert.Contains("query-syntax", p.RequiredBlockingCheckNames);
    }

    [TestMethod]
    [DataRow(WorkflowProfileId.SoloValidated)]
    [DataRow(WorkflowProfileId.StandardReview)]
    [DataRow(WorkflowProfileId.EmergencyFix)]
    public void ReservedProfiles_ThrowUntilImplemented(WorkflowProfileId id)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => WorkflowProfile.For(id));
    }
}
