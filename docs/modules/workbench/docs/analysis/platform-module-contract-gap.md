# Platform module contract gap

Workbench has been imported into `DeltaZulu.Platform` as a module, not as the final
platform host. The previous standalone shell has been removed; `DeltaZulu.Platform.Web` now owns
provider, shell, route discovery, and shared static-asset composition.

This note freezes the Workbench-side expectations before broader candidate, incident, or hunt
feature work continues. It addresses three alignment gaps that must be settled with Hunting
before pages are mounted in a shared host: module naming, route/module-manifest ownership, and
shared security-operations contracts.

## Decision summary

1. `WorkbenchModule : IPlatformModule` is the Workbench module contract; `WorkbenchShell` has been removed.
2. Workbench pages remain module UI; they must not become `DeltaZulu.Platform.Web` host pages.
3. Route/navigation/static-asset contracts live in `DeltaZulu.Platform.Web.Abstractions`, not in Workbench.
4. Workbench consumes Hunting-produced candidate and hunt-evidence read models, but it records
   analyst workflow decisions locally.
5. Workbench must not define runtime alert generation, candidate generation, KQL execution, or
   hunt query-run persistence.

## Naming alignment target

Workbench documentation should converge on implementation-domain names rather than names that
imply every operational security object is owned by manual hunting. The recommended platform
names are:

```text
DeltaZulu.Platform.Web
DeltaZulu.Platform.Web.Abstractions
DeltaZulu.Blazor.Components
DeltaZulu.DetectionContent

DeltaZulu.Hunting.Querying
DeltaZulu.Hunting.Runtime
DeltaZulu.Hunting.Schema
DeltaZulu.Hunting.Render
DeltaZulu.Hunting.Web

DeltaZulu.Security.Alerts
DeltaZulu.Security.Correlation
DeltaZulu.Security.Cases

DeltaZulu.Workbench.DetectionContent
DeltaZulu.Workbench.Hunts
DeltaZulu.Workbench.Workflow
DeltaZulu.Workbench.Web
```

The important Workbench rule is that detection-content governance, hunt workflow, and analyst
workflow are Workbench module capabilities. Alerts, correlation, and cases may be security
operations contracts, but they should not be placed under a Workbench namespace unless
Workbench owns their lifecycle.

## Current Workbench seam

`WorkbenchModule` centralizes Workbench identity, route prefix, navigation entries, route groups,
and static assets. Platform import no longer mines a Workbench-owned `MainLayout.razor`, and
Workbench no longer owns host-level routes or chrome.

Workbench routes are prefixed under `/workbench` and composed by the central platform host so
Workbench and Hunting can be mounted together without route conflicts.

## Required platform web abstraction

The shared abstraction should be designed once and consumed by both Workbench and Hunting. It
should live in `DeltaZulu.Platform.Web.Abstractions` and include at least:

| Contract | Responsibility |
|---|---|
| `IPlatformModule` | Module registration entry point used by the central host. |
| `PlatformModuleDescriptor` | Stable module identity, display name, route prefix, and ordering metadata. |
| `PlatformRouteGroup` | Route prefix and assembly/page discovery metadata for module pages. |
| `PlatformNavItem` | Navigation label, icon, route, ordering, and permission metadata. |
| `PlatformStaticAssetDescriptor` | CSS, script, image, and RCL asset declarations loaded once by the host. |

The platform host, not Workbench, should own provider setup, root layout, application shell,
MudBlazor providers, theme selection, static-asset loading order, authentication/authorization
providers, and cross-module route conflict detection.

Workbench should contribute only module metadata and module UI. Workbench pages should keep
coordinating Workbench application services, checks, reviews, drafts, merge readiness, and local
workflow behavior.

## Route prefixing expectations

Before the standalone host is removed, Workbench routes should have an explicit platform route
plan. A reasonable import shape is:

| Current standalone route | Platform route group |
|---|---|
| `/` | `/workbench` or a platform home contribution, not the global root. |
| `/detections` | `/workbench/detections`. |
| `/changes` | `/workbench/changes`. |
| `/history` | `/workbench/history`. |
| `/settings` | `/workbench/settings` or a platform settings section. |

The final route shape may differ, but it must be expressed in a shared manifest. The platform
host should reject ambiguous root-level routes when multiple modules are present.

## Shared contract boundary

Workbench should pause broad candidate, incident, and hunt feature work until the shared
contract family is explicit. The boundary is:

| Capability | Producer/owner | Workbench responsibility |
|---|---|---|
| Atomic alert match | Hunting or future security alert module | Read/link only. |
| Incident candidate | Hunting or future correlation module | Consume as an immutable read model. |
| Candidate evidence | Hunting runtime/render/evidence layer | Display and link; do not recompute. |
| Candidate decision | Workbench | Record analyst decision, reason, analyst, timestamp, and feedback intent. |
| Incident promotion | Workbench/future cases module | Create only after explicit analyst approval. |
| Hunt investigation lifecycle | Workbench/future hunt workflow module | Own assignment, lifecycle, notes, decisions, metrics, and handover. |
| Hunt query runs and snapshots | Hunting | Link to Hunting-owned artifacts. |

Candidate and incident contracts should be shared DTOs or references, not Workbench aggregates
exported to Hunting and not Hunting persistence entities imported directly into Workbench.

## Detection-content execution read model proposal

`DeltaZulu.DetectionContent` currently supplies identity, path, file, and accepted-reference
contracts. Hunting will also need an execution-oriented accepted-content read model. That model
should be proposed in the shared package before runtime execution is wired to Workbench content.
It should describe, at minimum:

- accepted detection identity and accepted version identity;
- slug and canonical repository paths;
- query text and query language;
- enabled state;
- severity and confidence;
- schedule or execution cadence reference;
- entity mapping hints;
- suppression policy reference;
- test fixture references;
- metadata labels/tags;
- provenance back to the accepted content reference.

Workbench may propose this contract, but Workbench must not implement scheduled execution,
alert generation, or runtime candidate creation.

## Merge-readiness gates

The platform import should not remove standalone hosts until these gates are complete:

1. Module names are finalized across Workbench and Hunting documentation.
2. `DeltaZulu.Platform.Web.Abstractions` defines the shared module descriptor, route group,
   navigation item, static asset descriptor, and module registration contract.
3. Workbench and Hunting expose route metadata through that shared contract instead of local
   shell/router abstractions.
4. Workbench root routes have a platform prefixing plan.
5. Candidate, incident, hunt, and executable detection-content read models are placed on the
   correct shared boundary.
6. Shared library tests move to shared test projects after the monorepo import so
   `DeltaZulu.Blazor.Components` and `DeltaZulu.DetectionContent` do not appear owned by
   Workbench.
