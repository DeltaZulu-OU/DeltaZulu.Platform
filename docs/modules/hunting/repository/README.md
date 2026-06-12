# Imported Hunting repository notes

This directory preserves documentation imported from the original Hunting repository. It is no longer
the current repository root or a standalone application guide.

## Current status

Hunting has been consolidated into DeltaZulu Platform as the Analytics capability area:

- Web/routes: `src/DeltaZulu.Platform.Web/Analytics` under `/hunting`.
- Domain: `src/DeltaZulu.Platform.Domain/Analytics`.
- Application services: `src/DeltaZulu.Platform.Application/Analytics`.
- Data/runtime infrastructure: `src/DeltaZulu.Platform.Data`.
- Tests: `tests/DeltaZulu.Platform.Tests`.

Run the platform host from the real repository root:

```bash
dotnet run --project src/DeltaZulu.Platform.Web/DeltaZulu.Platform.Web.csproj
```

Use centralized documentation for current architecture and target state:

- [`../../../README.md`](../../../README.md)
- [`../../../ARCHITECTURE.md`](../../../ARCHITECTURE.md)
- [`../../../ROADMAP.md`](../../../ROADMAP.md)
