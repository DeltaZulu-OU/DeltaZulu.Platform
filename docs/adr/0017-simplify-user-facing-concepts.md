# ADR-0017: Simplify User-Facing Concepts

## Status

Accepted

## Context

The workbench accumulated seven user-facing top-level concepts (Detections, Issues, Changes, Checks, Reviews, Versions, Settings) plus sub-concepts (workflow profiles, external case references, detection conception). A design review found that:

1. Users must understand the full domain model before they can act.
2. Issue is a required step before opening a Change, adding indirection.
3. Workflow profile selection asks users to choose governance levels they don't understand.
4. Checks and Reviews are standalone pages despite being state within a Change.
5. The documentation suite (~25 files) had become a consistency burden for a POC.

The product promise is simple: "Edit a detection, prove it's safe, accept it into history." The user-facing surface should match that simplicity.

## Decision

### Collapse to three user-facing concepts

| Before | After | Change |
|---|---|---|
| Detections | **Detections** | Kept. Catalog and version history. |
| Issues + Changes | **Changes** | Combined. Reason and investigation link become fields on the Change. Issues become optional, not a workflow step. |
| Checks | *Inline in Change* | No standalone page. Shown inside the Change workspace. |
| Reviews | *Inline in Change + Home queue* | No standalone page. Pending reviews surface on Home. |
| Versions | **History** | Renamed. Compare and restore. |
| Settings | Settings | Kept as operator-only. |

### Navigation reduces to five items

Home, Detections, Changes, History, Settings.

### Issue becomes optional

- The "reason for change" is a text field on the Change.
- The "related investigation" is a URL field on the Change.
- Issues remain in the domain model for teams that want a separate backlog, but they are not part of the core workflow and not a top-level navigation item.

### Governance is derived, not selected

- Users do not choose a workflow profile when opening a Change.
- The system derives governance from workspace configuration.
- The UI shows the effect ("requires approval") not the mechanism ("controlled_review").

### Detection conception is implicit

- Creating a Change for a new detection implicitly creates the detection identity.
- No separate "Conceive Detection" step in the UI.

### Documentation consolidated

- Product definition lives in `docs/DESIGN.md`.
- Analysis artifacts (GAP_ANALYSIS, USER_STORIES, UX_REDESIGN_ANALYSIS, UI_ACTIVITY_DIAGRAM, ROADMAP) are archived.
- README, ARCHITECTURE, and AGENTS are updated.

## Consequences

- First-time users can summarize the tool from its navigation alone.
- The core workflow requires fewer clicks and decisions.
- Issues and workflow profiles remain in the domain model for backward compatibility and team use, but they no longer define the UX.
- Standalone Checks and Reviews pages are removed from navigation; their content lives inside the Change workspace and Home queue.
- ADR-0007 (case-as-issue-type) and ADR-0008 (workflow profiles for governance) remain valid at the domain level but their UI expression changes.
- ADR-0014 (delegate case management) is simplified further: external case reference becomes a single URL field on the Change.

## Supersedes

This ADR modifies the UI expression of ADR-0007 and ADR-0008 without invalidating their domain-level decisions.
