# ADR 0010: Treat Phase 1A as the medallion checkpoint

Date: 2026-05-31  
Status: Accepted

## Context

The project originally used a smaller vertical-slice schema path with names such as `ProcessEvents`, `NetworkSessions`, and `windows_event_json`. During Phase 1A, the schema direction changed to a medallion model with source-shaped Bronze tables, source-specific Silver parser views, and singular Golden event-family contracts.

The change created a risk of mixed architecture: some code and tests could continue to reference old vertical-slice names while the active runtime and UI move to the medallion surface.

## Decision

Phase 1A is the checkpoint where the active branch standardizes on the medallion schema surface.

The active user-facing Golden contracts are:

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

The old vertical-slice names are removed from the active branch and should not be reintroduced accidentally:

```text
golden.ProcessEvents
golden.NetworkSessions
golden.DeviceProcessEvents
golden.DeviceNetworkEvents
bronze.windows_event_json
silver.v_process_sysmon_create
```

The development seeder should use only active Bronze source-family tables. UI sample queries should use only active Golden names.

## Consequences

The active branch becomes simpler to reason about because there is one public schema direction.

Legacy compatibility aliases are not part of Phase 1A. If compatibility is needed later, it must be implemented as a deliberate compatibility feature with explicit tests and documentation.

Phase 1A does not solve schema provenance, seed governance, tolerant casting, parser-spec modeling, or complete Golden semantic normalization. These become Phase 1D hardening workstreams.

## Validation

The decision is enforced by tests covering:

```text
active schema object names
absence of legacy names
active seeder entry points
sample query catalog names
end-to-end execution against active Golden contracts
```
