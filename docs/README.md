# DeltaZulu.Platform — documentation

Estate-wide architectural policy, Constraints, the current target and planning are maintained in [DeltaZulu-OU/docs](https://github.com/DeltaZulu-OU/docs). Local documents describe source, interfaces and historical reasoning; they do not override Accepted Decisions.

| Looking for | Go to |
|---|---|
| Current target | [NRT pipeline](https://github.com/DeltaZulu-OU/docs/blob/master/architecture/NRT-PIPELINE.md) |
| Governing Decisions | [Decision index](https://github.com/DeltaZulu-OU/docs/blob/master/architecture/GOVERNING-DECISIONS.md) |
| Constraints and qualifications | [Constraints](https://github.com/DeltaZulu-OU/docs/tree/master/constraints) |
| Active work | [Consolidated roadmap](https://github.com/DeltaZulu-OU/docs/blob/master/roadmaps/CONSISTENCY-CORRECTNESS-ROADMAP.md) (C03–C07, C10, C11, C16, C17) |
| Historical architecture | [Pipeline import](https://github.com/DeltaZulu-OU/docs/blob/master/architecture/PIPELINE.md) with [errata](https://github.com/DeltaZulu-OU/docs/blob/master/architecture/PIPELINE-ERRATA.md) |
| Historical ADRs | [Archive](https://github.com/DeltaZulu-OU/docs/tree/master/archive/DeltaZulu.Platform) |
| Dated verification evidence | [Reports](https://github.com/DeltaZulu-OU/docs/tree/master/reports) |

## Local scope

Local module, user-story, analytics, design and control-plane documents describe implementation or earlier plans. DEC-0033 replaces Proton and direct Agent lake/Proton sink plans with Platform collector, LocalStream/Query and DuckLake; DEC-0013 adopts ASIM-first Gold. Existing Proton code is legacy, not an active target. Historical source verification is dated evidence, not current build status. Use central work items for scheduling.

Only tested behavior is verified; a target architecture or historical report is not proof of current runtime conformance.
