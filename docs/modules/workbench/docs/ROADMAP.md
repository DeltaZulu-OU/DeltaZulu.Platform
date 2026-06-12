# Governance roadmap

Governance roadmap ownership has moved to the centralized DeltaZulu Platform roadmap.

- Active roadmap: [`../../../ROADMAP.md`](../../../ROADMAP.md)
- Current architecture: [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- Documentation index: [`../../../README.md`](../../../README.md)

## Current governance priorities

Governance work should now be tracked as platform work:

1. Preserve the core workflow: draft a detection change, run checks, review, accept, and inspect
   version history.
2. Keep operational state in SQLite-backed governance persistence and accepted content in Git.
3. Enforce base-version, stale-merge, controlled-review, self-approval, and restore-as-new-change
   safety rules.
4. Keep UI code behind application services; do not reach directly into SQLite or Git from pages.
5. Add tests to `tests/DeltaZulu.Platform.Tests`, not to a resurrected module-specific test project.
