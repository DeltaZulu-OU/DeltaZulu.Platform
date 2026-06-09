# W1: Workflow Domain Audit — Decision Table

## Purpose

Determine which existing Workbench domain objects can be reused for SOC candidate/incident
triage, which must stay separate, and which must not be bent into a shape they were not
designed for.

## Existing domain objects assessed

### Issue (Issue.cs)

| Aspect | Current design | SOC candidate requirement | Compatible? |
|---|---|---|---|
| Identity | Key + Title, IssueType enum | Candidate ID from Hunting, linked alert set | No. Issue identity is user-created; candidate identity is system-generated from correlation. |
| Lifecycle | 13-state content workflow (New → Triaged → Backlog → Ready → InProgress → InReview → Merged → Published) | Pending → Decided (Approved/Rejected/Merged/Split/Suppressed) | No. Issue lifecycle tracks content development. Candidate lifecycle tracks analyst triage decisions. |
| Data model | Description, acceptance criteria, data source, platform, ATT&CK ID | Alerts, entities, temporal window, risk score, severity, confidence, evidence summary, correlation rationale | No. Issue intake fields are human-authored. Candidate data is machine-generated from alerts. |
| Ownership | Workbench creates and mutates | Hunting creates; Workbench only records decisions | No. Issue is a Workbench aggregate. Candidate is a Hunting read model. |
| Relationships | Links to ChangeRequest, ExternalCaseRef | Links to alerts, entities, detection runs, evidence, and eventually to Incident | No. Different relationship graph. |

**Verdict: Do not reuse Issue for incident candidates.** The abstraction mismatch is
fundamental — Issues are human-authored content backlog items; candidates are system-generated
alert groupings awaiting human decision.

### ChangeRequest (ChangeRequest.cs)

| Aspect | Current design | SOC candidate requirement | Compatible? |
|---|---|---|---|
| Purpose | Edit detection content (draft → checks → review → accept → Git) | Decide on a candidate (inspect → decide → promote or reject) | No. Change is about content editing. Triage is about operational decisions. |
| Content | DraftFiles (YAML, KQL, NDJSON) | Alert summaries, entity lists, timelines, evidence | No. Different content model entirely. |
| Workflow | WorkflowProfile gates, check pipeline, review cycle | Single decision action per analyst | No. Change workflow is multi-step with quality gates. Candidate decision is a single action. |

**Verdict: Do not reuse ChangeRequest for candidate triage.**

### ExternalCaseRef (ExternalCaseRef.cs)

| Aspect | Current design | SOC incident requirement | Compatible? |
|---|---|---|---|
| Purpose | Link an Issue to an external case management system | Link an Incident to external systems | Yes. Same pattern, different parent. |
| Fields | SystemType, System, ExternalId, Url | Same fields needed | Yes. |

**Verdict: Reuse ExternalCaseRef as a value object on Incident.**

### Review (Review.cs)

| Aspect | Current design | Candidate decision requirement | Compatible? |
|---|---|---|---|
| Purpose | Approve/reject a content change | Approve/reject an incident candidate | Partially. Same decision recording pattern. |
| Fields | ReviewerId, Decision (Approved/ChangesRequested/Commented), Comment, IsSuperseded | AnalystId, DecisionType (Approve/RejectFP/RejectBenign/Suppress/...), Reason | No. Different decision types, no supersession logic for triage. |

**Verdict: Do not reuse Review directly. Adopt the pattern (entity with decision enum,
analyst, timestamp, reason) but create a distinct CandidateDecision type with SOC-specific
decision types.**

### DetectionVersion (DetectionVersion.cs)

| Aspect | Current design | SOC requirement | Compatible? |
|---|---|---|---|
| Purpose | Immutable projection of a Git commit | Not applicable to triage | Not applicable. |

**Verdict: No relationship to triage. Ignore.**

## Reusable patterns (not objects)

| Pattern | Where it exists | How to reuse |
|---|---|---|
| Entity<TId> base class | Common/Entity.cs | Use for Incident aggregate |
| Identifier record structs | Identifiers/ | Follow same pattern for IncidentId, CandidateDecisionId |
| Named state transitions | Issue.cs, ChangeRequest.cs | Follow same pattern for Incident lifecycle |
| DomainException for invariants | Common/DomainException.cs | Use for triage domain validation |
| ExternalCaseRef value object | Issues/ExternalCaseRef.cs | Attach to Incident for external system links |
| Timestamp + UserId attribution | All aggregates | Use for decision and incident audit |

## Reusable objects

| Object | Reuse as | Notes |
|---|---|---|
| ExternalCaseRef | Value object on Incident | No changes needed. Already system-agnostic. |
| ExternalSystemType | Enum on Incident case refs | Already includes SocIncident and Generic. |
| UserId | Analyst identifier in decisions and incidents | Already a generic user handle. |
| TlpLevel | Classification on Incident | Already defined for Issues; applicable to incidents. |

## New domain objects required

| Object | Type | Owner | Purpose |
|---|---|---|---|
| IncidentCandidate | Read model (record) | Hunting (consumed by Workbench) | Represents a correlated alert group for analyst review |
| CandidateDecision | Entity | Workbench | Records analyst triage decision on a candidate |
| CandidateDecisionType | Enum | Workbench | Approve, RejectFalsePositive, RejectBenign, Suppress, RequestEvidence |
| CandidateStatus | Enum | Workbench | Pending, Approved, Rejected, Suppressed |
| Incident | Aggregate | Workbench | Promoted from approved candidate; owns investigation lifecycle |
| IncidentStatus | Enum | Workbench | Open, Investigating, Contained, Resolved, Closed |
| IncidentId | Identifier | Workbench | Standard GUID identifier |
| CandidateDecisionId | Identifier | Workbench | Standard GUID identifier |
| ICandidateProvider | Port (interface) | Workbench.Application | Hunting implements; Workbench consumes |
| ICandidateDecisionRepository | Repository | Workbench.Application | Persist analyst decisions |
| IIncidentRepository | Repository | Workbench.Application | Persist promoted incidents |

## Decision summary

| Domain concept | Decision | Rationale |
|---|---|---|
| Issue | **Keep separate** | Content backlog item. Not a candidate. Not an incident. |
| ChangeRequest | **Keep separate** | Content editing workflow. Not triage. |
| Review | **New type (CandidateDecision)** | Same pattern, different semantics and decision types. |
| ExternalCaseRef | **Reuse directly** | Value object; already system-agnostic. |
| DetectionVersion | **Ignore** | Not related to triage. |
| IncidentCandidate | **New read model** | Hunting produces; Workbench consumes. |
| Incident | **New aggregate** | Workbench owns post-approval investigation. |

## Key constraint

Workbench must never own candidate generation, entity extraction, or scoring logic. The
`ICandidateProvider` port defines what Workbench expects from Hunting. Hunting implements
that contract. If Hunting is not available, Workbench shows no candidates — it does not
fall back to generating its own.
