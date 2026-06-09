# ADR 0017: Use Shared Platform Web Module Abstractions

## Status

Accepted

## Context

Hunting now exposes two useful pre-merge seams: `AddHuntingWebModule(...)` for service registration and
`HuntingModuleRouter` for standalone/module route rendering. These seams keep `Program.cs` thin and make
Hunting easier to mount in an early platform host, but they are intentionally repository-local.

Workbench has its own transitional shell contract. If `DeltaZulu.Platform` standardizes on both local
patterns, the platform host will need to support incompatible composition idioms, and route ownership will
remain ambiguous. Hunting also still has standalone absolute routes such as `/`, `/library`, `/dashboards`,
and `/settings`, which will conflict with a central host unless route ownership and prefixing are declared
through one shared model.

## Decision

`AddHuntingWebModule(...)` and `HuntingModuleRouter` are transition seams only. The final platform mounting
contract must live outside Hunting in `DeltaZulu.Platform.Web.Abstractions` or an equivalent shared package.
That shared package should define the route, navigation, module, and static-asset contract family before
Hunting pages are mounted into `DeltaZulu.Platform.Web`.

The target shared contract should include, at minimum:

- `PlatformModuleDescriptor` for module identity, display name, route base, required providers, and lifecycle metadata.
- `PlatformNavItem` for shell-owned navigation entries.
- `PlatformRouteGroup` for route bases, default pages, authorization metadata, and conflict detection.
- `PlatformStaticAssetDescriptor` for module-owned CSS, JavaScript, images, Monaco assets, and dashboard assets.
- `IPlatformModule` for module registration without adopting a module-local router or standalone shell.

Hunting service registration is split into narrower layers now:

- `AddHuntingRuntime(...)` registers DuckDB query/runtime/schema services.
- `AddHuntingApplicationState(...)` registers local application-state persistence and stateful services.
- `AddHuntingWebModule(...)` composes the current standalone-compatible web module bridge.

The split is a staging step, not the final platform contract.

## Consequences

This makes platform import less noisy because the host can depend on shared module descriptors rather than
on `HuntingModuleRouter` as a permanent abstraction. It also makes persistence ownership visible: platform
composition can replace path-based SQLite registration without disturbing DuckDB query/runtime services.

Before final platform hosting, Hunting still needs a mechanical route-prefixing decision for `/hunting`,
`/hunting/query`, `/hunting/library`, `/hunting/dashboards`, and `/hunting/settings` or another agreed base.
Until that exists, standalone absolute routes remain acceptable only inside the standalone host.
