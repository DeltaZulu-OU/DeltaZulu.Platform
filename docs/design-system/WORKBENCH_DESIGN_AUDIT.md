# Workbench Design-System Audit

This audit is a lightweight local guardrail for the Workbench UI. It does not replace review, but it makes common design-system drift visible before it spreads across pages.

Run from the repository root:

```powershell
pwsh ./scripts/design-audit.ps1
```

For stricter CI-style behaviour where any finding fails the run:

```powershell
pwsh ./scripts/design-audit.ps1 -Strict
```

To also scan `DeltaZulu.Blazor.Components` for shared-library guardrails such as accidental Workbench references:

```powershell
pwsh ./scripts/design-audit.ps1 -IncludeShared
```

## What it checks

| Rule | Default severity | Reason |
|---|---:|---|
| `inline-style` | Warning | Repeated visual styling should live in `DeltaZulu.Blazor.Components` CSS or a `Dz*` component. |
| `manual-overlay` | Error | Page-local overlays should not replace `IDialogService` and `DzConfirmDialog`. |
| `primary-non-action` | Error | Orange/primary is action-only and should not mark filters, metadata, badges, or generic selected state. |
| `raw-mudchip` | Warning | Chips should normally use shared `DzMetaChip`, `DzStatusChip`, `DzFilterChip`, or local Workbench enum adapters such as `DzComparisonStatusChip` and `DzReviewDecisionChip`. |
| `raw-mudpaper` | Info | Product panels should normally use `DzPanel`, `DzEmptyState`, or scoped table surfaces. |
| `shared-workbench-reference` | Error | Shared RCL code must not reference Workbench domain, application, persistence, infrastructure, workflow, validation, or web namespaces. |

## Intended use

Use this script before opening a PR that changes `src/Workbench.Web`. The goal is not to ban MudBlazor primitives entirely. The goal is to stop each page from inventing local colour, spacing, chip, panel, and dialog rules.

The script intentionally keeps `raw-mudpaper` as informational because some MudBlazor composition may still be legitimate. `manual-overlay` and non-action `Color.Primary` are treated as errors because they directly conflict with the Workbench product UI rules.

## Ownership guardrail

`DeltaZulu.Blazor.Components` owns reusable, domain-light primitives and its static assets. `Workbench.Web` owns adapters that translate Workbench domain/application enums, acceptance gates, route mappings, and page command flows into those primitives. The audit script intentionally scans `src/Workbench.Web` for drift because the shared RCL is the preferred destination for repeated generic UI.
