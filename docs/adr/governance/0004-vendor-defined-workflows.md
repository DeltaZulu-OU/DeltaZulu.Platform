# ADR-0004: Use vendor-defined workflow templates, not user-authored workflow YAML

## Status

Accepted

## Context

The system needs selectable workflows for different governance levels. A lab user may use a no-approval workflow. A SOC may require passing checks and approval by another engineer. Users should not need to write workflows, and accepting user-authored YAML workflows would create security, support, and complexity problems similar to CI runner platforms.

## Decision

Workflows are vendor-defined product capabilities. Users select approved workflows and provide safe parameters. Users cannot create arbitrary workflow definitions, upload scripts, run shell commands, or define unrestricted automation.

Initial workflow profiles:

- `quick_lab`
- `solo_validated`
- `standard_review`
- `controlled_review`
- `emergency_fix`

The POC implements `quick_lab` and `controlled_review`.

## Consequences

### Positive

- Provides simple and advanced governance without arbitrary automation risk.
- Keeps UI understandable.
- Supports predictable testing and support.
- Allows predefined workflow templates to evolve by version.

### Negative

- Less flexible than GitHub Actions-style user-defined workflows.
- Advanced users may request custom workflows later.
- Requires a workflow catalog and versioning model.

## Implementation notes

The product concept is a workflow profile. Elsa/Wexflow workflow definitions are implementation details.

Store workflow template/profile/version used by each PR/change for auditability.
