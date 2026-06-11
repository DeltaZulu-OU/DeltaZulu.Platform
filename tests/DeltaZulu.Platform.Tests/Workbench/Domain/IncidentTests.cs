using DeltaZulu.Platform.Domain.Workbench.Issues;
using DeltaZulu.Platform.Domain.Workbench.Triage;

namespace DeltaZulu.Platform.Tests.Workbench.Domain;

[TestClass]
public sealed class IncidentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly UserId Owner = UserId.New();
    private static readonly Guid CandidateId = Guid.NewGuid();
    private static readonly CandidateDecisionId ApprovalId = CandidateDecisionId.New();

    private static Incident CreateIncident(int severity = 3) =>
        Incident.Promote(IncidentId.New(), "Suspicious lateral movement",
            CandidateId, ApprovalId, Owner, severity, Now);

    [TestMethod]
    public void Promote_DefaultsToOpen()
    {
        var incident = CreateIncident();

        Assert.AreEqual(IncidentStatus.Open, incident.Status);
        Assert.AreEqual("Suspicious lateral movement", incident.Title);
        Assert.AreEqual(3, incident.Severity);
        Assert.AreEqual(CandidateId, incident.SourceCandidateId);
        Assert.AreEqual(ApprovalId, incident.ApprovalDecisionId);
        Assert.AreEqual(Owner, incident.OwnerId);
    }

    [TestMethod]
    public void Promote_EmptyTitle_Throws()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            Incident.Promote(IncidentId.New(), "", CandidateId, ApprovalId, Owner, 3, Now));
    }

    [TestMethod]
    public void Promote_InvalidSeverity_Throws()
    {
        Assert.ThrowsExactly<DomainException>(() =>
            Incident.Promote(IncidentId.New(), "Test", CandidateId, ApprovalId, Owner, 0, Now));
        Assert.ThrowsExactly<DomainException>(() =>
            Incident.Promote(IncidentId.New(), "Test", CandidateId, ApprovalId, Owner, 6, Now));
    }

    [TestMethod]
    public void FullLifecycle_HappyPath_ReachesClosed()
    {
        var incident = CreateIncident();
        Assert.AreEqual(IncidentStatus.Open, incident.Status);

        incident.StartInvestigation(Now);
        Assert.AreEqual(IncidentStatus.Investigating, incident.Status);

        incident.MarkContained(Now);
        Assert.AreEqual(IncidentStatus.Contained, incident.Status);

        incident.Resolve(Now);
        Assert.AreEqual(IncidentStatus.Resolved, incident.Status);

        incident.Close("Threat neutralized, no data exfiltration confirmed.", Now);
        Assert.AreEqual(IncidentStatus.Closed, incident.Status);
        Assert.AreEqual("Threat neutralized, no data exfiltration confirmed.", incident.CloseReason);
    }

    [TestMethod]
    public void Resolve_FromInvestigating_Succeeds()
    {
        var incident = CreateIncident();
        incident.StartInvestigation(Now);
        incident.Resolve(Now);
        Assert.AreEqual(IncidentStatus.Resolved, incident.Status);
    }

    [TestMethod]
    public void StartInvestigation_FromContained_Throws()
    {
        var incident = CreateIncident();
        incident.StartInvestigation(Now);
        incident.MarkContained(Now);

        Assert.ThrowsExactly<DomainException>(() =>
            incident.StartInvestigation(Now));
    }

    [TestMethod]
    public void Close_RequiresReason()
    {
        var incident = CreateIncident();
        Assert.ThrowsExactly<DomainException>(() =>
            incident.Close("", Now));
    }

    [TestMethod]
    public void Close_FromAnyNonTerminal_Succeeds()
    {
        var incident = CreateIncident();
        incident.Close("Duplicate of INC-042.", Now);
        Assert.AreEqual(IncidentStatus.Closed, incident.Status);
    }

    [TestMethod]
    public void Mutation_AfterClose_Throws()
    {
        var incident = CreateIncident();
        incident.Close("Done.", Now);

        Assert.ThrowsExactly<DomainException>(() => incident.StartInvestigation(Now));
        Assert.ThrowsExactly<DomainException>(() => incident.Rename("New title", Now));
        Assert.ThrowsExactly<DomainException>(() => incident.OverrideSeverity(1, Now));
        Assert.ThrowsExactly<DomainException>(() => incident.Reassign(UserId.New(), Now));
    }

    [TestMethod]
    public void OverrideSeverity_ValidRange_Succeeds()
    {
        var incident = CreateIncident();
        incident.OverrideSeverity(1, Now);
        Assert.AreEqual(1, incident.Severity);
    }

    [TestMethod]
    public void OverrideSeverity_OutOfRange_Throws()
    {
        var incident = CreateIncident();
        Assert.ThrowsExactly<DomainException>(() => incident.OverrideSeverity(0, Now));
        Assert.ThrowsExactly<DomainException>(() => incident.OverrideSeverity(6, Now));
    }

    [TestMethod]
    public void Reassign_UpdatesOwner()
    {
        var incident = CreateIncident();
        var newOwner = UserId.New();
        incident.Reassign(newOwner, Now);
        Assert.AreEqual(newOwner, incident.OwnerId);
    }

    [TestMethod]
    public void LinkExternalCase_Succeeds()
    {
        var incident = CreateIncident();
        var caseRef = new ExternalCaseRef("FlowIntel", "CASE-123", "https://flow.example/CASE-123", ExternalSystemType.FlowIntel);
        incident.LinkExternalCase(caseRef, Now);

        Assert.IsNotNull(incident.ExternalCase);
        Assert.AreEqual("CASE-123", incident.ExternalCase.ExternalId);
    }

    [TestMethod]
    public void Classify_SetsTlp()
    {
        var incident = CreateIncident();
        incident.Classify(TlpLevel.Amber, Now);
        Assert.AreEqual(TlpLevel.Amber, incident.Tlp);
    }

    [TestMethod]
    public void Reconstitute_PreservesAllFields()
    {
        var id = IncidentId.New();
        var caseRef = new ExternalCaseRef("SIEM", "INC-001", null, ExternalSystemType.Generic);

        var incident = Incident.Reconstitute(
            id, "Reconstituted", IncidentStatus.Investigating,
            CandidateId, ApprovalId, Owner, 2, TlpLevel.Red, caseRef,
            null, Now, Now);

        Assert.AreEqual(id, incident.Id);
        Assert.AreEqual(IncidentStatus.Investigating, incident.Status);
        Assert.AreEqual(2, incident.Severity);
        Assert.AreEqual(TlpLevel.Red, incident.Tlp);
        Assert.IsNotNull(incident.ExternalCase);
    }
}