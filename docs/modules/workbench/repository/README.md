# Imported Workbench repository notes

This directory preserves documentation imported from the original Workbench repository. It is no
longer the current repository root or a standalone application guide.

## Current status

Workbench has been consolidated into DeltaZulu Platform as the Governance capability area:

- Web/routes: `src/DeltaZulu.Platform.Web/Governance` under `/workbench`.
- Domain: `src/DeltaZulu.Platform.Domain/Governance`.
- Application services: `src/DeltaZulu.Platform.Application/Governance`.
- Data/persistence infrastructure: `src/DeltaZulu.Platform.Data`.
- Tests: `tests/DeltaZulu.Platform.Tests`.

Run the platform host from the real repository root:

```bash
dotnet run --project src/DeltaZulu.Platform.Web/DeltaZulu.Platform.Web.csproj
```

Use centralized documentation for current architecture and target state:

- [`../../../README.md`](../../../README.md)
- [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- [`../../../ROADMAP.md`](../../../ROADMAP.md)
