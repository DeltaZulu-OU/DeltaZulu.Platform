# UI Convergence Guide

Workbench is the reference UI host for the future shared shell. Hunting can keep product-specific layouts, but shared host readiness requires the same dependency baseline, token vocabulary, and CSS load order.

## Shared dependency baseline

- MudBlazor is pinned centrally in `Directory.Packages.props` and must match Hunting before route or component sharing.
- Package lock files are required for every project, and CI restores with `--locked-mode` so UI dependency changes are explicit.

## CSS load order

Future shared hosts should load CSS in this order:

```text
MudBlazor base CSS
DeltaZulu tokens
Shared MudBlazor overrides
Shared shell/layout CSS
Product CSS:
  workbench.*
  hunting.*
Page-specific CSS only where unavoidable
```

Workbench already follows the first-product version of that sequence: MudBlazor base CSS, `deltazulu-tokens.css`, and then `app.css` for DeltaZulu/MudBlazor overrides.

## Product CSS rules for Hunting

Hunting should not define a second global visual system in `:root`, `html`, or `body`. Instead, use DeltaZulu tokens directly or scoped aliases under a product prefix.

| Legacy Hunting token | Shared-host direction |
| --- | --- |
| `--bg-dark`, `--bg-panel`, `--bg-sidebar` | Use `--color-surface-*`, or define scoped `--hunt-surface-*` aliases from DeltaZulu tokens. |
| `--text-main`, `--text-dim`, `--text-head` | Use `--color-text-*`, or define scoped `--hunt-text-*` aliases. |
| `--accent`, `--accent-h` | Use `--color-cta-*` for actions and `--brand-*` or `--viz-*` for non-action emphasis. |
| `--font-ui`, `--font-mono` | Use `--font-family-sans` and `--font-family-mono`. |
| `--radius`, `--shadow` | Use `--radius-*` and `--shadow-*`. |

## Component guidance

- Prefer MudBlazor component theming and DeltaZulu tokens over raw color values.
- Keep product selectors scoped with prefixes such as `.workbench-*` and `.hunt-*` when a rule can affect a shared shell.
- Avoid global resets in product CSS. Shared shell CSS owns `html`, `body`, reset, and font smoothing rules.
- Page-specific CSS should be rare and must not redefine theme tokens globally.
