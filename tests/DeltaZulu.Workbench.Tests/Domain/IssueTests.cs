using DeltaZulu.Workbench.Domain.Issues;

namespace DeltaZulu.Workbench.Tests.Domain;

[TestClass]
public sealed class IssueTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Issue CreateDetectionRequest() =>
        Issue.Create(IssueId.New(), "REQ-001", "Tune anomalous sign-in detection",
            IssueType.DetectionRequest, Now);

    [TestMethod]
    public void Create_DefaultsToNew()
    {
        var issue = CreateDetectionRequest();
        Assert.AreEqual(IssueStatus.New, issue.Status);
        Assert.IsNull(issue.ExternalCase);
    }

    [TestMethod]
    public void FullLifecycle_HappyPath_ReachesPublished()
    {
        var issue = CreateDetectionRequest();
        Assert.AreEqual(IssueStatus.New, issue.Status);

        issue.Triage(Now);
        Assert.AreEqual(IssueStatus.Triaged, issue.Status);

        issue.Accept(Now);
        Assert.AreEqual(IssueStatus.Backlog, issue.Status);

        issue.DefineAcceptanceCriteria("Detection must pass schema and query-syntax checks.", Now);
        Assert.AreEqual(IssueStatus.Ready, issue.Status);

        issue.StartWork(Now);
        Assert.AreEqual(IssueStatus.InProgress, issue.Status);

        issue.SubmitForReview(Now);
        Assert.AreEqual(IssueStatus.InReview, issue.Status);

        issue.Complete(Now);
        Assert.AreEqual(IssueStatus.Merged, issue.Status);

        issue.Publish(Now);
        Assert.AreEqual(IssueStatus.Published, issue.Status);
    }

    [TestMethod]
    public void NeedsInfo_Loop_ReturnToTriaged()
    {
        var issue = CreateDetectionRequest();
        issue.RequestInfo(Now);
        Assert.AreEqual(IssueStatus.NeedsInfo, issue.Status);

        issue.ProvideInfo(Now);
        Assert.AreEqual(IssueStatus.Triaged, issue.Status);
    }

    [TestMethod]
    public void SanitizationRequired_Loop_ReturnToTriaged()
    {
        var issue = CreateDetectionRequest();
        issue.Triage(Now);
        issue.RequireSanitization(Now);
        Assert.AreEqual(IssueStatus.SanitizationRequired, issue.Status);

        issue.Sanitize(Now);
        Assert.AreEqual(IssueStatus.Triaged, issue.Status);
    }

    [TestMethod]
    public void Blocked_Loop_ReturnToReady()
    {
        var issue = CreateDetectionRequest();
        issue.Triage(Now);
        issue.Accept(Now);
        issue.DefineAcceptanceCriteria("Must pass all required checks.", Now);
        Assert.AreEqual(IssueStatus.Ready, issue.Status);

        issue.Block(Now);
        Assert.AreEqual(IssueStatus.Blocked, issue.Status);

        issue.Unblock(Now);
        Assert.AreEqual(IssueStatus.Ready, issue.Status);
    }

    [TestMethod]
    public void ChangesRequested_Loop_ReturnToInProgress()
    {
        var issue = CreateDetectionRequest();
        issue.Triage(Now);
        issue.Accept(Now);
        issue.DefineAcceptanceCriteria("Must pass all required checks.", Now);
        issue.StartWork(Now);
        issue.SubmitForReview(Now);

        issue.RequestChanges(Now);
        Assert.AreEqual(IssueStatus.InProgress, issue.Status);
    }

    [TestMethod]
    public void Reject_FromNew_TerminatesIssue()
    {
        var issue = CreateDetectionRequest();
        issue.Reject(Now);
        Assert.AreEqual(IssueStatus.Rejected, issue.Status);

        var ex = Assert.ThrowsExactly<DomainException>(
            () => issue.Triage(Now));
        Assert.AreEqual("issue.triage_invalid", ex.Code);
    }

    [TestMethod]
    public void Close_FromAnyNonTerminalState_Succeeds()
    {
        var issue = CreateDetectionRequest();
        issue.Close(Now);
        Assert.AreEqual(IssueStatus.Closed, issue.Status);
    }

    [TestMethod]
    public void Close_AfterClosed_Throws()
    {
        var issue = CreateDetectionRequest();
        issue.Close(Now);

        var ex = Assert.ThrowsExactly<DomainException>(
            () => issue.Close(Now));
        Assert.AreEqual("issue.terminal", ex.Code);
    }

    [TestMethod]
    public void InvalidTransition_Throws()
    {
        var issue = CreateDetectionRequest();

        var ex = Assert.ThrowsExactly<DomainException>(
            () => issue.StartWork(Now));
        Assert.AreEqual("issue.start_work_invalid", ex.Code);
    }

    [TestMethod]
    public void LinkExternalCase_SetsReference()
    {
        var issue = Issue.Create(IssueId.New(), "CASE-001", "Investigate via FlowIntel",
            IssueType.Case, Now);

        var caseRef = new ExternalCaseRef("flowintel", "FI-42", "https://flowintel.local/case/42",
            ExternalSystemType.FlowIntel);
        issue.LinkExternalCase(caseRef, Now);

        Assert.IsNotNull(issue.ExternalCase);
        Assert.AreEqual("flowintel", issue.ExternalCase.System);
        Assert.AreEqual("FI-42", issue.ExternalCase.ExternalId);
        Assert.AreEqual("https://flowintel.local/case/42", issue.ExternalCase.Url);
        Assert.AreEqual(ExternalSystemType.FlowIntel, issue.ExternalCase.SystemType);
    }

    [TestMethod]
    public void LinkExternalCase_TheHive_SetsReference()
    {
        var issue = Issue.Create(IssueId.New(), "CASE-002", "Investigate via TheHive",
            IssueType.Case, Now);

        var caseRef = new ExternalCaseRef("thehive", "~123456",
            "https://thehive.local/cases/~123456/details", ExternalSystemType.TheHive);
        issue.LinkExternalCase(caseRef, Now);

        Assert.IsNotNull(issue.ExternalCase);
        Assert.AreEqual("thehive", issue.ExternalCase.System);
        Assert.AreEqual("~123456", issue.ExternalCase.ExternalId);
        Assert.AreEqual(ExternalSystemType.TheHive, issue.ExternalCase.SystemType);
    }

    [TestMethod]
    public void UnlinkExternalCase_ClearsReference()
    {
        var issue = CreateDetectionRequest();
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
        var issue = Issue.Create(IssueId.New(), "REQ-002", "FalsePositive from case",
            IssueType.FalsePositiveReport, Now);

        issue.LinkExternalCase(new ExternalCaseRef("flowintel", "FI-99"), Now);
        Assert.IsNotNull(issue.ExternalCase);
        Assert.AreEqual(IssueType.FalsePositiveReport, issue.Type);
    }

    [TestMethod]
    public void UpdateIntakeFields_SetsStructuredFields()
    {
        var issue = CreateDetectionRequest();
        issue.UpdateIntakeFields(
            description: "Detect lateral movement via WMI.",
            acceptanceCriteria: null,
            dataSource: "windows-security",
            platform: "windows",
            attackTechniqueId: "T1047",
            Now);

        Assert.AreEqual("windows-security", issue.DataSource);
        Assert.AreEqual("windows", issue.Platform);
        Assert.AreEqual("T1047", issue.AttackTechniqueId);
    }

    [TestMethod]
    public void Classify_SetsTlpAndLabels()
    {
        var issue = CreateDetectionRequest();
        Assert.IsNull(issue.Tlp);
        Assert.AreEqual(0, issue.Labels.Count);

        issue.Classify(TlpLevel.Amber, ["lateral-movement", "windows"], Now);

        Assert.AreEqual(TlpLevel.Amber, issue.Tlp);
        Assert.AreEqual(2, issue.Labels.Count);
        Assert.AreEqual("lateral-movement", issue.Labels[0]);
        Assert.AreEqual("windows", issue.Labels[1]);
    }

    [TestMethod]
    public void Classify_NullTlp_ClearsTlp()
    {
        var issue = CreateDetectionRequest();
        issue.Classify(TlpLevel.Red, [], Now);
        Assert.AreEqual(TlpLevel.Red, issue.Tlp);

        issue.Classify(null, [], Now);
        Assert.IsNull(issue.Tlp);
    }

    [TestMethod]
    public void Classify_TrimsBlanksFromLabels()
    {
        var issue = CreateDetectionRequest();
        issue.Classify(null, ["  windows  ", " lateral-movement "], Now);

        Assert.AreEqual("windows", issue.Labels[0]);
        Assert.AreEqual("lateral-movement", issue.Labels[1]);
    }

    [TestMethod]
    public void Classify_TooManyLabels_Throws()
    {
        var issue = CreateDetectionRequest();
        var labels = Enumerable.Range(1, 21).Select(i => $"label-{i}").ToList();

        var ex = Assert.ThrowsExactly<DomainException>(() => issue.Classify(null, labels, Now));
        Assert.AreEqual("issue.too_many_labels", ex.Code);
    }

    [TestMethod]
    public void Classify_EmptyLabel_Throws()
    {
        var issue = CreateDetectionRequest();

        var ex = Assert.ThrowsExactly<DomainException>(() => issue.Classify(null, ["valid", ""], Now));
        Assert.AreEqual("issue.label_empty", ex.Code);
    }
}
