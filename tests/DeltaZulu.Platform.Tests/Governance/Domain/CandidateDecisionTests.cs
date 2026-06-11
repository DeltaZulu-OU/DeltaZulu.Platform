using DeltaZulu.Platform.Domain.Governance.Triage;

namespace DeltaZulu.Platform.Tests.Governance.Domain;

[TestClass]
public sealed class CandidateDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserId Analyst = UserId.New();
    private static readonly Guid CandidateId = Guid.NewGuid();

    [TestMethod]
    public void Record_Approval_Succeeds()
    {
        var decision = CandidateDecision.Record(
            CandidateDecisionId.New(), CandidateId,
            CandidateDecisionType.Approve, Analyst, "Confirmed lateral movement", Now);

        Assert.AreEqual(CandidateDecisionType.Approve, decision.Type);
        Assert.AreEqual(CandidateId, decision.CandidateId);
        Assert.AreEqual(Analyst, decision.AnalystId);
        Assert.IsNull(decision.ResultingIncidentId);
    }

    [TestMethod]
    public void Record_RejectFalsePositive_Succeeds()
    {
        var decision = CandidateDecision.Record(
            CandidateDecisionId.New(), CandidateId,
            CandidateDecisionType.RejectFalsePositive, Analyst, "Known service account", Now);

        Assert.AreEqual(CandidateDecisionType.RejectFalsePositive, decision.Type);
    }

    [TestMethod]
    public void Record_ReasonTooLong_Throws() => Assert.ThrowsExactly<DomainException>(() =>
                                                          CandidateDecision.Record(
                                                              CandidateDecisionId.New(), CandidateId,
                                                              CandidateDecisionType.Approve, Analyst, new string('x', 4001), Now));

    [TestMethod]
    public void LinkIncident_OnApproval_Succeeds()
    {
        var decision = CandidateDecision.Record(
            CandidateDecisionId.New(), CandidateId,
            CandidateDecisionType.Approve, Analyst, "Confirmed", Now);

        var incidentId = IncidentId.New();
        decision.LinkIncident(incidentId);

        Assert.AreEqual(incidentId, decision.ResultingIncidentId);
    }

    [TestMethod]
    public void LinkIncident_OnRejection_Throws()
    {
        var decision = CandidateDecision.Record(
            CandidateDecisionId.New(), CandidateId,
            CandidateDecisionType.RejectFalsePositive, Analyst, "FP", Now);

        Assert.ThrowsExactly<DomainException>(() =>
            decision.LinkIncident(IncidentId.New()));
    }

    [TestMethod]
    public void LinkIncident_AlreadyLinked_Throws()
    {
        var decision = CandidateDecision.Record(
            CandidateDecisionId.New(), CandidateId,
            CandidateDecisionType.Approve, Analyst, "Confirmed", Now);

        decision.LinkIncident(IncidentId.New());

        Assert.ThrowsExactly<DomainException>(() =>
            decision.LinkIncident(IncidentId.New()));
    }

    [TestMethod]
    public void Reconstitute_PreservesAllFields()
    {
        var id = CandidateDecisionId.New();
        var incidentId = IncidentId.New();

        var decision = CandidateDecision.Reconstitute(
            id, CandidateId, CandidateDecisionType.Approve,
            Analyst, "Confirmed", incidentId, Now);

        Assert.AreEqual(id, decision.Id);
        Assert.AreEqual(CandidateId, decision.CandidateId);
        Assert.AreEqual(CandidateDecisionType.Approve, decision.Type);
        Assert.AreEqual(incidentId, decision.ResultingIncidentId);
    }
}