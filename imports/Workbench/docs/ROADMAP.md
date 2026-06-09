# Roadmap

This roadmap covers the Workbench project within the broader DeltaZulu platform plan.
Workbench is one of three coordinated tracks: **Hunting** (detection/alert/candidate engine),
**Workbench** (triage/workflow surface), and **DeltaZulu.Platform** (composition host). The
repositories should not merge until Hunting and Workbench have clean domain boundaries.

## 1. Workbench identity

Workbench owns analyst workflow. Its job is to help humans review incident candidates, approve
incidents, reject false positives, assign work, comment, document decisions, and manage
investigation state. It does not own correlation, entity extraction, scoring, or candidate
generation — those belong to Hunting.

The current Workbench already owns detection content management (edit, validate, review, accept
into Git). That scope remains. The roadmap extends Workbench to consume security operations
objects produced by Hunting without becoming the owner of their generation logic.

### FIRST-aligned detection engineering guidance

FIRST Detection Engineering & Threat Hunting SIG guidance reinforces the current content workflow while shaping future roadmap work:

- **Outcomes and tuning reasons** should explain whether a change responds to a false positive, false negative, coverage gap, test failure, validated hunting finding, or other feedback source.
- **Tiering, fidelity, coverage, and precision** should be metadata and reporting dimensions around detections and accepted versions, not standalone navigation or required forms for starting a change.
- **Ecosystem feedback** from CTI, hunting, SOC analysis, incident response, offensive validation, and platform teams should enter Workbench as links, summaries, reasons, checks, reviews, and version history.
- **Scope boundaries** remain: Workbench should not become a CTI platform, threat-hunting notebook, case-management system, SIEM runtime, or taxonomy manager.

These items should be introduced only when they support concrete prioritization, reporting, or detection-quality feedback stories.

## 2. Phases

### W1: Audit existing issue/workflow model

Review the current Issue, Change, status, check, review, and comment models. Identify which
concepts are generic workflow (reusable) and which would need to become security-specific
(candidate triage, incident promotion, TaHiTI hunt investigations). ADR-0017 simplified Issues
to optional; ADR-0018 expanded the Issue domain for detection content; ADR-0020 keeps threat
hunting on a separate `HuntInvestigation` aggregate. This audit determines whether existing
models can support future SOC and hunt workflows or whether separate domains are needed.

**Exit criteria:** A decision table showing what can be reused, what must stay separate, and
what must not be bent into a shape it was not designed for. The key questions: can `Issue` or
`Change` host candidate decisions, and can either host TaHiTI hunt investigations? ADR-0020
answers the hunt question as no: hunts need their own aggregate.

### W2: Separate generic workflow from SOC workflow

Keep generic Workbench issues (detection content backlog per ADR-0018) separate from security
alert/candidate/incident objects. Workbench does not define what an incident candidate is — it
only displays and operates on candidate contracts provided by Hunting.

**Exit criteria:** Workbench does not own candidate generation, entity extraction, or scoring.
It consumes read models from Hunting contracts. Generic issue workflow and SOC triage workflow
have distinct domain objects even if they share UI patterns.

### W3: Add candidate triage UI

Build candidate list, candidate detail, score, severity, confidence, status, primary entity,
timeline, contributing alerts, related logs, and correlation explanation. This is a read-only
consumption layer over Hunting's candidate contracts.

**Exit criteria:** Analysts can inspect why a candidate exists before approving or rejecting
it. The UI shows contributing alerts, entities, temporal window, scoring factors, and
correlation rationale.

### W4: Add candidate decision actions

Approve as incident, reject as false positive, reject as benign, merge candidates, split
candidate, suppress pattern, assign, comment, and request more evidence. These are structured
actions, not free-text comments.

**Exit criteria:** Candidate decisions are recorded with type, reason, analyst, and timestamp.
Decisions feed back to Hunting for tuning and suppression.

### W5: Add incident approval workflow

Promotion from candidate to incident creates an incident record while preserving candidate
provenance. Incident creation requires explicit analyst approval — no auto-promotion.

**Exit criteria:** Approved candidates become incidents with full lineage. Candidate history
(alerts, entities, score, evidence, decision) remains intact and auditable after promotion.

### W6: Add investigation/case workflow

Tasks, notes, checklists, owner, SLA tracking, severity override, status changes,
attachments/references, and final closure reason. This extends beyond the detection content
Change model into post-approval incident handling.

**Exit criteria:** Workbench supports post-approval incident investigation without
contaminating candidate generation or detection content workflow.

### W7: Add audit trail

Track status changes, decisions, comments, assignments, suppression decisions, and promotion
events across candidates, incidents, and detection changes.

**Exit criteria:** Every analyst action is attributable and timestamped. Audit records support
compliance reporting and post-incident review.

### W8: Add role/permission model

Define privileges for triage, approval, suppression, detection editing, incident management,
and administration.

**Exit criteria:** Analysts cannot accidentally approve, suppress, or edit detections without
proper permission. Permission boundaries are enforced in both UI and domain.

### W9: Align UI with design system

Candidate/incident screens must use the same layout, buttons, chips, density, toolbar, drawer,
and table standards as existing detection content screens. This is prerequisite work for the
eventual platform web host merge.

**Exit criteria:** No inconsistent patterns between detection content UI and SOC triage UI.
Shared components are extracted and documented.

### W10: Add workflow tests

Test candidate display, decision submission, promotion, rejection, merge/split, comments, and
permission restrictions. Tests must validate Workbench behaviour independently from Hunting
correlation logic.

**Exit criteria:** Workbench triage and incident workflow is covered by automated tests.
Changes to Hunting contracts break contract tests, not silent runtime failures.

## 3. Implementation priority

| Priority | Phase | Reason |
|---|---|---|
| 1 | W1 — Workflow/domain audit | Prevents generic issue concepts from distorting SOC objects. |
| 2 | W2 — Separate domains | Workbench must consume candidate data cleanly. |
| 3 | W3 — Candidate read model | Triage UI is the first visible SOC capability. |
| 4 | W4 — Triage decisions | Approval/rejection is the boundary between candidate and incident. |
| 5 | W5 — Incident promotion | Creates the real incident only after human approval. |
| 6 | W6 — Case workflow | Needed after promotion, not before. |
| 7 | W7–W8 — Permissions and audit | Required for operational trust. |
| 8 | W9 — UI consistency | Important before final platform web host merge. |
| 9 | W10 — Workflow tests | Validates the complete triage-to-incident lifecycle. |

## 4. Dependencies on Hunting

Workbench cannot build triage UI (W3) until Hunting provides stable candidate contracts.
The critical Hunting phases that gate Workbench progress:

| Hunting phase | What it provides | Gates Workbench phase |
|---|---|---|
| Detection content versioning | Alerts know which detection version fired | — |
| Detection runs | Auditable execution records | — |
| Alerts table | Atomic detection match records | — |
| Alert entities | Normalized entities (user, host, IP, process, etc.) | — |
| Candidate generation | Deterministic incident candidates from correlated alerts | W3 (triage UI) |
| Evidence builder | Timelines, related logs, scoring rationale | W3 (triage detail) |
| Feedback inputs | Decision storage for tuning | W4 (decision actions) |

The highest-risk dependency is entity quality. Production systems converge on shared-entity
correlation with temporal windows, and weak entity normalization is a root cause of bad
correlation. Hunting's entity extraction phase is critical path, not optional plumbing.

## 5. Dependencies on DeltaZulu.Platform

Platform work should remain mechanical until Hunting and Workbench boundaries are clean.

| Platform phase | What it provides | Gates Workbench phase |
|---|---|---|
| Shared contracts | Alert, candidate, incident, workflow DTOs | W2 (domain separation) |
| Design system consolidation | Shared UI components and theme | W9 (UI alignment) |
| Platform web host | Single composition shell | After W9 |

## 6. Cross-project sequencing

| Sequence | Project | Work |
|---|---|---|
| 1 | Hunting | Stabilize query/runtime and detection library semantics. |
| 2 | **Workbench** | **Audit issue/workflow model (W1).** |
| 3 | Hunting | Add detection content versioning and detection run records. |
| 4 | Hunting | Add alerts and alert entities. |
| 5 | **Workbench** | **Design candidate triage read model against Hunting contracts (W2–W3).** |
| 6 | Hunting | Add deterministic candidate generation and evidence builder. |
| 7 | **Workbench** | **Add candidate decisions and incident promotion (W4–W5).** |
| 8 | Both | Add tests around alert → candidate → incident lifecycle. |
| 9 | Platform | Create repository and import histories via subtree. |
| 10 | Platform | Move, rename, consolidate build, create single web host. |
| 11 | Platform | Integrate Hunting and Workbench modules. |
| 12 | Platform | Remove legacy hosts after platform host is stable. |

## 7. Pre-merge definition of done

Workbench must meet these criteria before merging into DeltaZulu.Platform:

- Generic issue workflow (ADR-0017, ADR-0018) is separated from SOC candidate/incident workflow.
- Candidate triage UI consumes Hunting contracts via read models, not direct internal coupling.
- Approval, rejection, merge, and split are structured domain actions.
- Incident promotion is explicit and requires analyst approval.
- Audit trail and permission model are defined and enforced.
- Detection content workflow (edit → validate → review → accept) remains intact and tested.
- TaHiTI hunt workflow remains a documented boundary until shared contracts are stable.

## 8. Suggested module names

| Current concern | Suggested future module |
|---|---|
| KQL execution and query runtime | `DeltaZulu.Hunting.Querying` |
| Detection content and tests | `DeltaZulu.Hunting.Detections` |
| Alert persistence and entity extraction | `DeltaZulu.Security.Alerts` |
| Candidate generation and evidence | `DeltaZulu.Security.Correlation` |
| Incident/case workflow contracts | `DeltaZulu.Security.Cases` |
| Detection content workflow (Workbench) | `DeltaZulu.Workbench` |
| Threat hunting lifecycle and handover | `DeltaZulu.Workbench.Hunts` |
| Shared UI/design primitives | `DeltaZulu.Platform.DesignSystem` |
| Single web host | `DeltaZulu.Platform.Web` |

Alerts, entities, and candidates should live under `DeltaZulu.Security.*` rather than
`DeltaZulu.Hunting.*` because they are security operations concepts broader than hunting.
Hunting can produce them, but the platform should not imply that all alerts and candidates
originate from manual hunting.

## 9. First Workbench sprint items

| Sprint item | Deliverable |
|---|---|
| Audit Workbench issue model | Reuse/avoidance decision table for Issue vs candidate domain |
| Draft candidate triage screen contract | Read-model DTO and UI wireframe |
| Define alert/candidate/incident contracts | Shared contract document and initial C# records/interfaces |
| Confirm platform module names | Final module naming table before subtree import |

## 10. Risks and open questions

**Main danger: premature reuse.** If existing Workbench Issue objects become the canonical
incident-candidate model, the platform inherits the wrong abstraction. Incident candidates
need membership, evidence, explanation, temporal bounds, score, and analyst decision state.
They are not tickets with severity.

**Key unknown:** Whether Workbench already has a clean workflow abstraction that can host
candidate decisions without becoming the owner of incident-correlation logic. The W1 audit
resolves this.

**Entity quality risk:** Candidate triage quality depends entirely on Hunting's entity
normalization. Poor entities produce noisy candidates, which makes Workbench triage painful
regardless of UI quality.

## 11. Current POC scope

The existing POC scope (detection content management) is documented in the archived roadmap
at `docs/archive/ROADMAP.md`. That scope remains valid and is not superseded by this roadmap.
The phases above extend Workbench beyond detection content into SOC triage and incident
workflow — work that begins after the POC core is stable.
