# Executable Detection Content Boundary

<!-- Retention note: Retained boundary analysis. Central platform docs and newer ADRs supersede old project-location references in this file. -->

## Purpose

This note records what Hunting needs from the future shared `DeltaZulu.DetectionContent` package before it
can execute accepted governed detections. It is documentation only: Hunting must not create a local accepted
content contract or implement Workbench governance workflows before the shared package exists.

## Ownership rule

| Area | Owner | Boundary |
|---|---|---|
| Draft, review, approval, accepted canonical content | Workbench / shared detection-content package | Hunting consumes accepted read models only. |
| Query execution, diagnostics, detection runs, alerts | Hunting | Hunting executes accepted KQL and records runtime outcomes. |
| Candidate triage decisions | Workbench / future security operations contracts | Hunting may produce candidates; Workbench records analyst decisions. |
| Incident/case lifecycle | Future operations/cases modules | Alerts and candidates do not become incidents by changing local Hunting state. |

## Minimum executable read model needed by Hunting

The shared package should eventually provide an immutable accepted-content read model that can be imported
or projected into Hunting runtime scheduling. Hunting needs these fields, but should not define them locally
as canonical accepted-content concepts:

| Field family | Runtime need |
|---|---|
| Stable identity | Accepted detection id, version id, slug/path, source reference, and content hash for auditability. |
| Query body | KQL text, target schema/table hints, parameter defaults, and compatibility metadata. |
| Runtime enablement | Enabled/disabled state, schedule intent, lookback/window settings, and maximum result limits. |
| Severity/confidence/risk | Alert scoring inputs that remain tied to the accepted detection version that fired. |
| Entity mapping hints | Field-to-entity mappings for user, host, IP, process, file, hash, URL/domain, cloud resource, registry key, and session identifiers. |
| Suppression policy | Suppression keys, time windows, exception references, and duplicate-handling expectations. |
| Test references | Fixture ids, expected match behavior, parser/schema prerequisites, and validation metadata. |
| Provenance metadata | Author/reviewer references, accepted timestamp, repository path, tags, MITRE labels, and compatibility notes. |

## Candidate, incident, and hunt boundary

Hunting can later create deterministic alerts and incident candidates from accepted detection executions, but
candidate triage decisions and incident/case lifecycle state must be shared or Workbench-owned contracts.
Likewise, `HuntInvestigation` workflow belongs outside the query runtime; Hunting should expose query runs,
result snapshots, evidence pointers, visualizations, pivots, and lineage for a hunt workflow to reference.

## Import sequence

1. Consume shared identity/path/reference types from `DeltaZulu.DetectionContent`.
2. Add a shared executable accepted-detection read model in that package or a closely related contracts package.
3. Project accepted detections into Hunting runtime scheduling/import state without creating local governance state.
4. Store detection runs and alerts with accepted id/version/hash references.
5. Define shared candidate/incident/hunt handover contracts before adding broad triage UI or workflow features.
