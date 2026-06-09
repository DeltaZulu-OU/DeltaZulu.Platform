using Workbench.Domain.Changes;

namespace Workbench.Tests.Domain;

[TestClass]
public sealed class ChangeRequestTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly UserId Author = UserId.New();
    private static readonly UserId OtherUser = UserId.New();
    private static readonly DetectionId Det = DetectionId.New();

    private static ChangeRequest OpenChange(WorkflowProfileId profile, VersionId? baseVersion = null)
        => ChangeRequest.Open(
            ChangeRequestId.New(),
            "CHG-001",
            "Tune sign-in detection",
            Det,
            Author,
            profile,
            baseVersion,
            T0);

    private static void SeedPackage(ChangeRequest c)
    {
        c.UpsertDraftFile(LogicalPath.Parse("detection.yaml"), DraftContentType.DetectionMetadata, "id: x", Author, T0);
        c.UpsertDraftFile(LogicalPath.Parse("rule.kql"), DraftContentType.HuntingQuery, "SigninLogs | take 1", Author, T0);
    }

    private static void RunPassingChecks(ChangeRequest c, params string[] names)
    {
        foreach (var name in names)
        {
            var run = c.QueueCheck(CheckRunId.New(), name, isBlocking: true, T0);
            run.MarkRunning(T0);
            run.Complete(CheckStatus.Passed, "ok", "{}", "", T0);
        }

        c.AfterCheckPipelineCompleted(T0);
    }

    // -- quick lab fast path -----------------------------------------------------------------

    [TestMethod]
    public void QuickLab_MergesWithoutChecksOrReviews()
    {
        var c = OpenChange(WorkflowProfileId.QuickLab);
        SeedPackage(c);

        var readiness = c.EvaluateMergeReadiness();
        Assert.IsTrue(readiness.IsReady,
            string.Join(", ", readiness.UnmetGates.Select(g => g.Code)));

        c.MarkMerged(VersionId.New(), T0);
        Assert.AreEqual(ChangeStatus.Merged, c.Status);
    }

    // -- controlled review: self-approval block ----------------------------------------------

    [TestMethod]
    public void ControlledReview_AuthorCannotApproveOwnChange()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);
        RunPassingChecks(c, "package-schema", "query-syntax");

        var ex = Assert.ThrowsExactly<DomainException>(() =>
            c.RecordReview(ReviewId.New(), Author, ReviewDecision.Approved, "lgtm", T0));

        Assert.AreEqual("review.self_approval_forbidden", ex.Code);
        Assert.DoesNotContain(r => r.Decision == ReviewDecision.Approved, c.Reviews);
    }

    [TestMethod]
    public void QuickLab_AuthorCanRecordOwnApproval_NoOpForGates()
    {
        var c = OpenChange(WorkflowProfileId.QuickLab);
        SeedPackage(c);

        var review = c.RecordReview(ReviewId.New(), Author, ReviewDecision.Approved, "lgtm", T0);
        Assert.AreEqual(ReviewDecision.Approved, review.Decision);
    }

    // -- controlled review: approval reset on content edit -----------------------------------

    [TestMethod]
    public void ControlledReview_EditingContentSupersedesPriorApproval_AndRevertsToDraft()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);
        RunPassingChecks(c, "package-schema", "query-syntax");

        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "lgtm", T0);
        Assert.AreEqual(ChangeStatus.ReadyToAccept, c.Status);
        Assert.HasCount(1, c.EffectiveApprovals);

        c.UpsertDraftFile(
            LogicalPath.Parse("rule.kql"),
            DraftContentType.HuntingQuery,
            "SigninLogs | where ResultType == 50074 | take 1",
            Author,
            T0.AddMinutes(5));

        Assert.IsEmpty(c.EffectiveApprovals);
        Assert.AreEqual(ChangeStatus.Draft, c.Status);

        foreach (var r in c.Reviews.Where(r => r.Decision == ReviewDecision.Approved))
        {
            Assert.IsTrue(r.IsSuperseded, $"Review {r.Id} should be superseded after content edit.");
        }
    }

    [TestMethod]
    public void QuickLab_EditingContent_DoesNotSupersedeReview()
    {
        var c = OpenChange(WorkflowProfileId.QuickLab);
        SeedPackage(c);
        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "lgtm", T0);

        c.UpsertDraftFile(
            LogicalPath.Parse("rule.kql"),
            DraftContentType.HuntingQuery,
            "SigninLogs | take 2",
            Author,
            T0.AddMinutes(5));

        Assert.HasCount(1, c.EffectiveApprovals);
        Assert.DoesNotContain(r => r.IsSuperseded, c.Reviews);
    }

    // -- controlled review: stale change blocks merge ----------------------------------------

    [TestMethod]
    public void ControlledReview_StaleChange_BlocksMerge()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview, baseVersion: VersionId.New());
        SeedPackage(c);
        RunPassingChecks(c, "package-schema", "query-syntax");
        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "lgtm", T0);
        Assert.AreEqual(ChangeStatus.ReadyToAccept, c.Status);

        c.MarkStale("Another change merged onto this detection.", T0.AddMinutes(10));

        var readiness = c.EvaluateMergeReadiness();
        Assert.IsFalse(readiness.IsReady);
        Assert.Contains(g => g.Code == "gate.stale", readiness.UnmetGates,
            "Expected 'gate.stale' among unmet gates.");

        var ex = Assert.ThrowsExactly<DomainException>(() => c.MarkMerged(VersionId.New(), T0.AddMinutes(11)));
        Assert.AreEqual("gate.stale", ex.Code);
    }

    [TestMethod]
    public void QuickLab_StaleChange_DoesNotBlockMerge()
    {
        var c = OpenChange(WorkflowProfileId.QuickLab, baseVersion: VersionId.New());
        SeedPackage(c);
        c.MarkStale("Another change merged.", T0);

        var readiness = c.EvaluateMergeReadiness();
        Assert.IsTrue(readiness.IsReady);
    }

    // -- controlled review: required checks ---------------------------------------------------

    [TestMethod]
    public void ControlledReview_FailedRequiredCheck_BlocksMerge()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);

        var run = c.QueueCheck(CheckRunId.New(), "query-syntax", isBlocking: true, T0);
        run.MarkRunning(T0);
        run.Complete(CheckStatus.Failed, "parse error at line 3", "{}", "", T0);
        c.AfterCheckPipelineCompleted(T0);

        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "lgtm anyway", T0.AddMinutes(1));

        var readiness = c.EvaluateMergeReadiness();
        Assert.IsFalse(readiness.IsReady);
        Assert.Contains(g => g.Code == "gate.checks_not_passed", readiness.UnmetGates,
            "Expected 'gate.checks_not_passed' among unmet gates.");
    }

    [TestMethod]
    public void ControlledReview_NoChecksRun_BlocksMerge()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);
        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "lgtm", T0);

        var readiness = c.EvaluateMergeReadiness();
        Assert.IsFalse(readiness.IsReady);
        Assert.Contains(g => g.Code == "gate.no_checks", readiness.UnmetGates,
            "Expected 'gate.no_checks' among unmet gates.");
    }


    [TestMethod]
    public void ControlledReview_MissingConfiguredRequiredCheck_BlocksMerge()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);
        RunPassingChecks(c, "package-schema");

        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "metadata only", T0.AddMinutes(1));

        var readiness = c.EvaluateMergeReadiness();
        Assert.IsFalse(readiness.IsReady);
        Assert.Contains(g => g.Code == "gate.checks_missing", readiness.UnmetGates,
            "Expected 'gate.checks_missing' when query-syntax never ran.");
        Assert.AreEqual(ChangeStatus.ReviewRequired, c.Status,
            "Approval can still be recorded, but merge readiness must remain blocked.");
    }

    [TestMethod]
    public void ControlledReview_SkippedRequiredCheck_RevertsToDraftAfterPipeline()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        c.UpsertDraftFile(LogicalPath.Parse("notes/context.md"), DraftContentType.InvestigationNote, "# Note", Author, T0);
        c.AfterCheckPipelineCompleted(T0);

        Assert.AreEqual(ChangeStatus.Draft, c.Status);
        var readiness = c.EvaluateMergeReadiness();
        Assert.IsFalse(readiness.IsReady);
        Assert.Contains(g => g.Code == "gate.no_checks", readiness.UnmetGates,
            "Expected 'gate.no_checks' when required checks were all skipped.");
    }

    // -- controlled review: happy path --------------------------------------------------------

    [TestMethod]
    public void ControlledReview_AllGatesSatisfied_MergeSucceeds()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);
        RunPassingChecks(c, "package-schema", "query-syntax", "fixture-parse");
        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Approved, "lgtm", T0.AddMinutes(1));

        Assert.AreEqual(ChangeStatus.ReadyToAccept, c.Status);
        Assert.IsTrue(c.EvaluateMergeReadiness().IsReady);

        var version = VersionId.New();
        c.MarkMerged(version, T0.AddMinutes(2));

        Assert.AreEqual(ChangeStatus.Merged, c.Status);
        Assert.AreEqual(version, c.ResultVersionId);
    }

    // -- terminal-state immutability ----------------------------------------------------------

    [TestMethod]
    public void MergedChange_CannotBeEdited()
    {
        var c = OpenChange(WorkflowProfileId.QuickLab);
        SeedPackage(c);
        c.MarkMerged(VersionId.New(), T0);

        var ex = Assert.ThrowsExactly<DomainException>(() =>
            c.UpsertDraftFile(LogicalPath.Parse("rule.kql"), DraftContentType.HuntingQuery, "x", Author, T0));

        Assert.AreEqual("change.immutable", ex.Code);
    }

    // -- workflow profile reselection ---------------------------------------------------------

    [TestMethod]
    public void SelectWorkflowProfile_AfterFirstReview_IsRejected()
    {
        var c = OpenChange(WorkflowProfileId.QuickLab);
        SeedPackage(c);
        c.RecordReview(ReviewId.New(), OtherUser, ReviewDecision.Commented, "I'll take a look", T0);

        var ex = Assert.ThrowsExactly<DomainException>(
            () => c.SelectWorkflowProfile(WorkflowProfileId.ControlledReview, T0));
        Assert.AreEqual("change.profile_locked", ex.Code);
    }

    // -- check clearing (P0-1 regression) ----------------------------------------------------

    [TestMethod]
    public void ClearChecksForNewRun_RemovesOldChecks_SecondRunNotPollutedByFirst()
    {
        var c = OpenChange(WorkflowProfileId.ControlledReview);
        SeedPackage(c);

        // First run: one required check fails.
        var fail = c.QueueCheck(CheckRunId.New(), "package-schema", isBlocking: true, T0);
        fail.MarkRunning(T0);
        fail.Complete(CheckStatus.Failed, "bad", "{}", "", T0);
        c.AfterCheckPipelineCompleted(T0);
        Assert.HasCount(1, c.Checks);

        // Clear for second run.
        c.ClearChecksForNewRun(T0.AddMinutes(1));
        Assert.IsEmpty(c.Checks);

        // Second run: all configured required checks pass.
        var schema = c.QueueCheck(CheckRunId.New(), "package-schema", isBlocking: true, T0.AddMinutes(1));
        schema.MarkRunning(T0.AddMinutes(1));
        schema.Complete(CheckStatus.Passed, "ok", "{}", "", T0.AddMinutes(1));

        var query = c.QueueCheck(CheckRunId.New(), "query-syntax", isBlocking: true, T0.AddMinutes(1));
        query.MarkRunning(T0.AddMinutes(1));
        query.Complete(CheckStatus.Passed, "ok", "{}", "", T0.AddMinutes(1));
        c.AfterCheckPipelineCompleted(T0.AddMinutes(1));

        // Only the second run's checks should exist. Merge readiness should not be blocked by
        // the first run's failure.
        Assert.HasCount(2, c.Checks);
        Assert.IsTrue(c.Checks.All(check => check.Status == CheckStatus.Passed));
        Assert.AreEqual(ChangeStatus.ReviewRequired, c.Status);
    }
}
