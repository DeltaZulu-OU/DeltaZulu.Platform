# ADR-0009: Use vendor-neutral domain language

## Status

Accepted

## Context

The product should not look like a wrapper around a specific SIEM, XDR, or cloud security platform. Vendor product names and schema names should not become core domain terms. The initial query language is KQL, but target systems and runtime integrations are future concerns.

## Decision

Use vendor-neutral terminology in core domain model, UI, workflows, schemas, and documentation.

Preferred terms:

- Detection.
- Hunting query.
- Scheduled detection.
- Normalized event view.
- Content pack.
- External detection platform.
- Local runtime.
- Source connector.
- Reference list.
- Workflow automation.

Vendor-specific adapters may exist later, but their terminology must remain below the core domain layer.

## Consequences

### Positive

- Preserves product independence.
- Avoids unnecessary coupling.
- Makes future target adapters cleaner.
- Keeps architecture vendor-neutral.

### Negative

- Some users may be familiar with vendor-specific names and need mapping in docs.
- Adapter implementation must translate neutral terms to target-specific APIs.

## Rule

No vendor product names in core entity names, workflow names, schema names, or primary UI labels.
