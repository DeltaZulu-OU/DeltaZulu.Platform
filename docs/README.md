# DeltaZulu.Platform — documentation

Architecture, Decisions, Constraints and roadmaps for the DeltaZulu estate live
in **[`DeltaZulu-OU/docs`](https://github.com/DeltaZulu-OU/docs)**, not here.

| Looking for | Go to |
|---|---|
| Decisions governing this repository | [`architecture/GOVERNING-DECISIONS.md`](https://github.com/DeltaZulu-OU/docs/blob/main/architecture/GOVERNING-DECISIONS.md) |
| The estate-wide pipeline architecture | [`architecture/PIPELINE.md`](https://github.com/DeltaZulu-OU/docs/blob/main/architecture/PIPELINE.md) — read with `PIPELINE-ERRATA.md` |
| Facts the estate does not control | [`constraints/`](https://github.com/DeltaZulu-OU/docs/tree/main/constraints) |
| This repository's historical ADRs | [`archive/DeltaZulu.Platform/`](https://github.com/DeltaZulu-OU/docs/tree/main/archive/DeltaZulu.Platform) |
| Roadmaps | [`roadmaps/`](https://github.com/DeltaZulu-OU/docs/tree/main/roadmaps) |
| Verification evidence | [`reports/`](https://github.com/DeltaZulu-OU/docs/tree/main/reports) |

Decisions are numbered globally across the estate. The per-repository scheme this
replaced produced collisions that citations could not resolve — `DeltaZulu.Agent`
ADR 0014 and `DeltaZulu.Platform` ADR 0014 decide opposite things, and the Agent
carried two different ADR 0003 documents, so "ADR 0003" did not resolve even
within one repository.

## What remains here

`ARCHITECTURE.md`, `TARGET_USER_STORIES.md`, and the `analytics/`,
`architecture/`, `design/` and `reviews/` directories.

`ROADMAP.md` and `AGENT_MANAGEMENT_ROADMAP.md` have moved to the docs repository
and gained review triggers.

One amendment recorded against the archived Platform ADRs: **ADR 0007's clause
stating that agents do not map into Silver or enrichments is struck** by
`DEC-0005`, which makes agent extraction authoritative for Silver.

Before acting on §11.1 of `PIPELINE.md`, read
`reports/2026-08-16-schema-divergence-verification.md` in the docs repository.
Seven of its thirteen divergences are refuted against current code, so
implementing the list as written would mean changing correct code. The defect
that matters most is not on it: `ILogicalSchemaRegistry` has no implementation
anywhere under `src/`.
