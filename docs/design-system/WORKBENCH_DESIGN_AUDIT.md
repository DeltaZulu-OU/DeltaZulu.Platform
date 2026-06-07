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

## What it checks

| Rule | Default severity | Reason |
|---|---:|---|
| `inline-style` | Warning | Repeated visual styling should live in `workbench-design-system.css` or a `Dz*` component. |
| `manual-overlay` | Error | Page-local overlays should not replace `IDialogService` and `DzConfirmDialog`. |
| `primary-non-action` | Error | Orange/primary is action-only and should not mark filters, metadata, badges, or generic selected state. |
| `raw-mudchip` | Warning | Chips should normally use `DzMetaChip`, `DzStatusChip`, `DzFilterChip`, `DzComparisonStatusChip`, or `DzReviewDecisionChip`. |
| `raw-mudpaper` | Info | Product panels should normally use `DzPanel`, `DzEmptyState`, or scoped table surfaces. |

## Intended use

Use this script before opening a PR that changes `src/Workbench.Web`. The goal is not to ban MudBlazor primitives entirely. The goal is to stop each page from inventing local colour, spacing, chip, panel, and dialog rules.

The script intentionally keeps `raw-mudpaper` as informational because some MudBlazor composition may still be legitimate. `manual-overlay` and non-action `Color.Primary` are treated as errors because they directly conflict with the Workbench product UI rules.
