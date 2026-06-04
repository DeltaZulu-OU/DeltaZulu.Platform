using Workbench.Domain.Issues;

namespace Workbench.Tests.Domain;

[TestClass]
public sealed class IssueTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Issue CreateRegular() =>
        Issue.Create(IssueId.New(), "ISS-001", "Tune anomalous sign-in detection",
            IssueType.Tuning, Priority.Normal, Now);

    [TestMethod]
    public void Create_DefaultsToOpen()
    {
        var issue = CreateRegular();
        Assert.AreEqual(IssueStatus.Open, issue.Status);
        Assert.IsNull(issue.AssigneeId);
        Assert.IsNull(issue.ExternalCase);
    }

    [TestMethod]
    public void TransitionStatus_Closed_CannotReopen()
    {
        var issue = CreateRegular();
        issue.TransitionStatus(IssueStatus.Closed, Now);

        var ex = Assert.ThrowsExactly<DomainException>(
            () => issue.TransitionStatus(IssueStatus.Open, Now));
        Assert.AreEqual("issue.reopen_forbidden", ex.Code);
    }

    [TestMethod]
    public void LinkExternalCase_SetsReference()
    {
        var issue = Issue.Create(IssueId.New(), "ISS-002", "Investigate via FlowIntel",
            IssueType.Case, Priority.High, Now);

        var caseRef = new ExternalCaseRef("flowintel", "FI-42", "https://flowintel.local/case/42");
        issue.LinkExternalCase(caseRef, Now);

        Assert.IsNotNull(issue.ExternalCase);
        Assert.AreEqual("flowintel", issue.ExternalCase.System);
        Assert.AreEqual("FI-42", issue.ExternalCase.ExternalId);
        Assert.AreEqual("https://flowintel.local/case/42", issue.ExternalCase.Url);
    }

    [TestMethod]
    public void LinkExternalCase_TheHive_SetsReference()
    {
        var issue = Issue.Create(IssueId.New(), "ISS-003", "Investigate via TheHive",
            IssueType.Case, Priority.Critical, Now);

        var caseRef = new ExternalCaseRef("thehive", "~123456", "https://thehive.local/cases/~123456/details");
        issue.LinkExternalCase(caseRef, Now);

        Assert.IsNotNull(issue.ExternalCase);
        Assert.AreEqual("thehive", issue.ExternalCase.System);
        Assert.AreEqual("~123456", issue.ExternalCase.ExternalId);
    }

    [TestMethod]
    public void UnlinkExternalCase_ClearsReference()
    {
        var issue = CreateRegular();
        issue.LinkExternalCase(new ExternalCaseRef("flowintel", "FI-1"), Now);
        Assert.IsNotNull(issue.ExternalCase);

        issue.UnlinkExternalCase(Now);
        Assert.IsNull(issue.ExternalCase);
    }

    [TestMethod]
    public void ExternalCaseRef_RejectsEmptySystem()
    {
        var ex = Assert.ThrowsExactly<DomainException>(
            () => new ExternalCaseRef("", "id-1"));
        Assert.AreEqual("external_case.system_empty", ex.Code);
    }

    [TestMethod]
    public void ExternalCaseRef_RejectsEmptyId()
    {
        var ex = Assert.ThrowsExactly<DomainException>(
            () => new ExternalCaseRef("flowintel", ""));
        Assert.AreEqual("external_case.id_empty", ex.Code);
    }

    [TestMethod]
    public void AnyIssueType_CanCarryExternalCaseRef()
    {
        // External case refs are not gated by issue type — a tuning issue can link to an external case.
        var tuning = Issue.Create(IssueId.New(), "ISS-004", "Tuning from case",
            IssueType.Tuning, Priority.Normal, Now);

        tuning.LinkExternalCase(new ExternalCaseRef("flowintel", "FI-99"), Now);
        Assert.IsNotNull(tuning.ExternalCase);
        Assert.AreEqual(IssueType.Tuning, tuning.Type);
    }
}