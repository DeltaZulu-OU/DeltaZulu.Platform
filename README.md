# DeltaZulu.Platform

DeltaZulu.Platform is the consolidated repository for DeltaZulu Workbench, Hunting, shared Blazor components, detection-content contracts, and the unified platform web application.

The repository preserves the Git history of the original Hunting and Workbench repositories through history-preserving subtree imports.

## Web host status

`DeltaZulu.Platform.Web` is the only runnable Blazor web host in this repository. The Hunting and Workbench UI projects are Razor Class Library modules that contribute routable pages, services, and static assets to the platform host; they no longer contain standalone `Program.cs`, `App.razor`, host layouts, launch settings, or host appsettings files.

## Project layout

| Area | Projects |
|---|---|
| Unified host | `src/DeltaZulu.Platform.Web` |
| Platform contracts | `src/DeltaZulu.Platform.Web.Abstractions`, `src/DeltaZulu.DetectionContent` |
| Shared UI | `src/DeltaZulu.Blazor.Components` |
| Hunting module | `src/DeltaZulu.Hunting.*`, including the `src/DeltaZulu.Hunting.Web` Razor Class Library |
| Workbench module | `src/DeltaZulu.Workbench.*`, including the `src/DeltaZulu.Workbench.Web` Razor Class Library |
| Tests | `tests/DeltaZulu.*.Tests` |

## Build and run

Use the solution file for repository-wide validation:

```bash
dotnet build DeltaZulu.Platform.slnx
dotnet test DeltaZulu.Platform.slnx
```

Run the unified web application from the platform host project:

```bash
dotnet run --project src/DeltaZulu.Platform.Web/DeltaZulu.Platform.Web.csproj
```
