# ADR 0012: Treat Phase 1A as the medallion checkpoint

Date: 2026-05-31  
Status: Accepted

## Context

The project originally used a smaller vertical-slice schema path with plural event-family names and compatibility aliases. During Phase 1A, the schema direction moved to a medallion model with source-shaped Bronze tables, source/event-specific Silver parser views, and singular Golden event-family contracts.

## Decision

Phase 1A is the checkpoint where the active branch standardizes on the medallion schema surface.

The active Golden contracts are:

```text
golden.ProcessEvent
golden.NetworkSession
golden.Dns
```

The active Bronze tables are:

```text
bronze.windows_sysmon_event
bronze.windows_security_event
bronze.dns_server_event
```

The old vertical-slice names are removed from the active branch and should not be reintroduced accidentally.

## Consequences

The active branch has one public schema direction. Compatibility aliases are not part of Phase 1A. If compatibility is needed later, it must be implemented deliberately with explicit tests and documentation.

Phase 1A does not solve schema provenance, seed governance, tolerant casting, parser-spec modeling, or complete Golden semantic normalization. Those become Phase 1B–1E workstreams.

## Validation

The decision is enforced by tests covering active schema object names, absence of legacy names, active seeder entry points, sample-query catalog names, and end-to-end execution against active Golden contracts.
