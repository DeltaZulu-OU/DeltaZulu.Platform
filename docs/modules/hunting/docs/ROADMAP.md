# Analytics roadmap

Analytics roadmap ownership has moved to the centralized DeltaZulu Platform roadmap.

- Active roadmap: [`../../../ROADMAP.md`](../../../ROADMAP.md)
- Current architecture: [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- Documentation index: [`../../../README.md`](../../../README.md)

## Current analytics priorities

Analytics work should now be tracked as platform work:

1. Grow supported KQL coverage only when semantics can be translated safely.
2. Keep unsupported constructs diagnostic-first rather than silently approximated.
3. Preserve Golden-view-only analyst query surfaces.
4. Keep render/dashboard behavior above the query runtime without introducing a second query language.
5. Add tests to `tests/DeltaZulu.Platform.Tests`, not to a resurrected module-specific test project.

The detailed construct-level tracker remains
[`kql-syntax-coverage-checklist.md`](kql-syntax-coverage-checklist.md).
