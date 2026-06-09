# ADR-0012: Exclude SIEM runtime and SOAR from the POC

## Status

Accepted

## Context

The current asset is a KQL query editor and a design for detection content management. There is no SIEM runtime. Building ingestion, scheduling, alert generation, live execution, enrichment, and response automation would distract from validating the content/workflow architecture.

## Decision

The POC excludes SIEM runtime and SOAR automation. It focuses on content authoring, issue/case workflow, PR-like changes, checks, reviews, workflow profiles, merge into Git, and automatic version history.

## Consequences

### Positive

- Keeps POC focused.
- Validates the database/Git boundary.
- Avoids premature runtime architecture.
- Leaves room for future runtime or publisher adapters.

### Negative

- The POC will not prove live detection execution.
- Some validation may rely on stubs or fixture-only execution.
- Runtime integration remains a future design problem.

## Future

Runtime and SOAR-like automation may be added after the content workflow is proven. Future extensions should use vendor-neutral adapters and predefined workflow actions.
