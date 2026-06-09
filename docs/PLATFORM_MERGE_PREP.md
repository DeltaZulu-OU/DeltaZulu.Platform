# Platform Merge Preparation Audit

This document records the current merge-preparation boundary for moving Workbench into a future
`DeltaZulu.Platform.Web` host while keeping Workbench domain/application/persistence/workflow
libraries separate.

## Reusable UI inventory

| Category | Current decision | Paths | Notes |
|---|---|---|---|
| Shell primitives | Shared now | `src/DeltaZulu.Blazor.Components/DzAppBar.razor`, `DzSideNav.razor`, `DzMainContent.razor`, `wwwroot/dz-shell.css` | These are host chrome primitives only. Product title, logo, route list, providers, and theme still come from the host. |
| Page scaffolding | Shared now | `DzPageHeader.razor`, `DzToolbar.razor`, `DzPanel.razor`, `DzMetricTile.razor` | Pages should compose these instead of creating local `MudPaper` card systems. |
| Panels and table shells | Shared now | `DzPanel.razor`, `DzTableShell.razor`, `DzTabBody.razor` | Reusable layout wrappers live in the RCL; table content and Workbench data columns remain page-owned. |
| Chips, badges, and filters | Shared now | `DzStatusChip.razor`, `DzMetaChip.razor`, `DzFilterChip.razor`, `DzFilterRow.razor` | Shared chips take generic labels/tone/kind only. |
| Workbench enum chips | Keep local | `src/Workbench.Web/Components/Shared/StatusChip.razor`, `TlpChip.razor`, `DzComparisonStatusChip.razor`, `DzReviewDecisionChip.razor` | These map Workbench domain/application enums to generic `DzStatusChip`; they must not move while they depend on Workbench types. |
| Dialog and confirmation flow | Shared now | `DzConfirmDialog.razor` | Confirmation shell is reusable; command-specific messages and callbacks remain Workbench pages/services. |
| Empty and loading states | Shared now | `DzEmptyState.razor`, `DzLoadingState.razor` | Keep copy and actions page-owned. |
| Code, diff, and file display | Shared now | `DzCodeBlock.razor`, `DzDiffPair.razor`, `DzFileHeader.razor` | File path rendering and side-by-side content previews are reusable primitives. |
| Markdown surfaces | Moved now | `DzMarkdownViewer.razor`, `DzMarkdownText.razor`, `DzMarkdownRenderer.cs`, `src/Workbench.Web/Components/Shared/MarkdownViewer.razor` | Generic Markdown rendering moved to the RCL; Workbench route mapping remains local for detection/investigation repository links. Raw HTML is escaped by default; this is not a complete sanitizer policy. |
| Detail-list/action helpers | Shared now | `DzDetailList.razor`, `DzDetailItem.razor`, `DzActionLink.razor` | These are generic operational UI helpers. |
| Workbench acceptance gates | Keep local | `src/Workbench.Web/Components/Shared/GateChecklist.razor` | Depends on `MergeReadiness`, gate codes, and Workbench acceptance callbacks. |
| Pages and workflow composites | Keep local | `src/Workbench.Web/Components/Pages/*.razor` | Pages coordinate application services, drafts, checks, review, merge/readiness, and Workbench-specific command flows. |
| Future host route composition | Later | `src/Workbench.Web/Components/Routes.razor`, `MainLayout.razor`, `WorkbenchShell.cs` | Workbench now centralizes module nav metadata, but final platform route discovery and provider ownership belong in `DeltaZulu.Platform.Web`. |

## Shared detection-content inventory

| Category | Current decision | Paths | Notes |
|---|---|---|---|
| Detection identity | Shared boundary | `src/DeltaZulu.DetectionContent/Identity/DetectionContentId.cs` | Cross-product identity shape that can be mapped from Workbench `DetectionId` without importing Workbench workflow types. |
| Stable version identity | Shared boundary | `DetectionContentVersionId.cs`, `References/AcceptedDetectionVersionRef.cs` | Captures accepted version identity, display version, sequence, and accepted timestamp. It does not expose reviews, approvals, or change status. |
| Slug/key conventions | Shared boundary | `Paths/DetectionSlug.cs` | Same validation shape as Workbench detection slugs; path-safe and URL-safe. |
| Logical content/file concepts | Shared boundary | `Paths/DetectionLogicalPath.cs`, `Paths/DetectionRepositoryPath.cs`, `Files/DetectionContentFile.cs` | Represents stable package file paths and payloads, with traversal-safe logical and repository path validation. Draft semantics remain Workbench-local. |
| Accepted canonical content references | Shared boundary | `References/AcceptedDetectionContentRef.cs` | Stable accepted-content lookup reference with optional current version and optional accepted-content commit reference. |
| Accepted-content path convention | Shared boundary | `Paths/DetectionContentPathResolver.cs` | Owns `detections/<slug>/<logical-path>` as the public path convention. Workbench `CanonicalPathResolver` is now a compatibility adapter. |
| Git-backed references | Shared, optional metadata | `AcceptedDetectionContentRef.cs`, `AcceptedDetectionVersionRef.cs` | Commit SHA is optional metadata for stores that are Git-backed; branch/ref/check-out details are not shared contracts. |
| Database lookup/link concepts | Shared boundary shape | Shared IDs and refs above | Operational systems can link by stable detection/version IDs without importing Workbench changes, drafts, or reviews. |
| Drafts and draft files | Workbench-specific | `src/Workbench.Domain/Changes/ChangeDraftFile.cs`, `ChangeRequest.cs` | Database-owned operational state. Do not move into shared detection-content contracts. |
| Checks, reviews, approvals | Workbench-specific | `CheckRun.cs`, `Review.cs`, `Workflow/MergeReadiness.cs` | Governance behavior and acceptance gates remain Workbench-local. |
| Change requests, issues, merge readiness | Workbench-specific | `Changes/*`, `Issues/*`, `Workflow/*` | Workflow/governance concepts are not part of the cross-product detection-content boundary. |
| Full domain ID migration | Later | `Workbench.Domain/Identifiers/*` | Workbench keeps current strongly typed IDs for compatibility. A later migration can map or replace them with shared IDs when platform contracts stabilize. |

## Host composition audit

| Host-only concern | Current Workbench state | Merge-prep outcome |
|---|---|---|
| App shell ownership | `MainLayout.razor` owns Mud providers, shell, drawer, app bar, and side nav. | Shell metadata/navigation moved into `WorkbenchShell.cs` so a central host can replace chrome without mining layout code. |
| Route ownership | `Routes.razor` discovers routable Workbench pages in the Web assembly. | Still host-owned today. Future `DeltaZulu.Platform.Web` should import Workbench route/module metadata rather than use Workbench's standalone router. |
| Mud provider ownership | `MainLayout.razor` owns `MudThemeProvider`, popover, dialog, and snackbar providers. | Still standalone-host responsibility. Final platform host must own one provider set. |
| Logo/navigation ownership | Logo and nav are centralized through `WorkbenchShell`. | Central host can replace platform branding and include Workbench module nav items. |
| Static asset loading | `App.razor` loads MudBlazor and `DeltaZulu.Blazor.Components` assets before `app.css`. | Correct for standalone Workbench. Final host should load shared assets once and keep product CSS scoped. |

## Remaining blockers before full `DeltaZulu.Platform.Web` integration

1. Workbench pages are still routable Blazor pages rather than a hostless module route manifest.
2. Mud providers and theme are still created by Workbench's standalone `MainLayout`; a platform host must own a single provider/theme stack.
3. Workbench static assets (`logo-*.png`, `app.css`) are still standalone-host assets and need platform branding/scoped product CSS treatment.
4. Workbench domain IDs and version projections are not yet migrated to the shared `DeltaZulu.DetectionContent` IDs; mapping is required before Hunting can consume the same contracts directly.
5. Accepted-content store ports still live in Workbench.Application and expose Workbench-oriented request/result names; only stable file/path/reference shapes have been extracted.
6. Workbench governance state remains intentionally local. Platform integration must compose it as Workbench module behavior, not as generic platform issue/workflow contracts.
