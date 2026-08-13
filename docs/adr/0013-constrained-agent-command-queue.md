# ADR 0013: Constrained agent command queue

## Status

Accepted.

## Context

The agent management roadmap (P1 Operations/Safety) requires centrally initiated remediation —
reload configuration, test output, flush buffers, collect diagnostics, restart the service —
without turning the agent into a general-purpose remote execution platform. The pull-based check-in
protocol (ADR 0012) already gives every agent a periodic, authenticated exchange with the platform.

## Decision

- **Closed allowlist.** Remote operations are the members of the `AgentCommandType` enum
  (`ReloadConfiguration`, `TestOutput`, `FlushBuffer`, `CollectDiagnostics`, `RestartService`).
  There is no parameterized shell, script, or query payload; adding an operation requires a new
  enum member and explicit agent support.
- **Pull delivery, never push.** Operators queue commands per agent; pending commands are
  delivered inside the next heartbeat response and marked `Delivered`. The agent posts the outcome
  to `POST /api/agent/v1/commands/{id}/result`, scoped by its own bearer identity — an agent can
  only complete its own commands.
- **Fully audited lifecycle.** Commands persist their requesting user, timestamps for
  requested/delivered/completed, structured result JSON or error, and terminal status
  (`Succeeded`/`Failed`/`Cancelled`/`Expired`). The history is append-only per command and shown on
  the agent detail page.
- **Bounded lifetime.** Every command carries a timeout; the periodic status sweep expires
  in-flight commands whose timeout has elapsed (measured from delivery when delivered, otherwise
  from request), so lost agents cannot accumulate an unbounded queue of stale intents.

## Consequences

- Command latency is bounded below by the heartbeat interval; the queue is for operational
  remediation, not interactive control.
- Advanced local query abstractions (roadmap P3) must arrive as their own constrained design; the
  result channel here transports only the outcome of allowlisted operations.
- Cancelling after delivery cannot un-run the operation on the agent; cancellation is a
  best-effort intent recorded in the audit trail.
