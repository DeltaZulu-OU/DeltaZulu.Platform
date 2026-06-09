# Workbench Product UI Expansion

## 1. Purpose

This document extends the DeltaZulu design system for Detection Content Workbench. Workbench is a dense operational product surface for detection content editing, validation, review, acceptance, comparison, restore, and operator recovery. It is not a marketing surface and it is not a generic dashboard.

The expansion does not weaken the base DeltaZulu rules. Orange remains action-only. Product UI uses IBM Plex Sans and IBM Plex Mono. Status, metadata, filter, table, and comparison states must be explicit, inspectable, and calm under operational load.

## 2. Relationship to the core DeltaZulu design system

The core design system remains authoritative for brand primitives, typography, colour roles, spacing, radius tokens, shadows, and motion. This expansion defines how those tokens are applied in Workbench-specific product patterns.

| Rule | Workbench interpretation |
|---|---|
| Orange / accent | Reserved for primary user actions only. Do not use orange for filters, badges, version labels, tab counters, metadata chips, decorative icons, or passive selected state. |
| Slate | Structural emphasis. Use for active navigation, secondary actions, low-intensity selected state, metadata emphasis, and calm product affordances. |
| Ink | Primary text and dark structural surfaces. Use for app bar, strong headings, and high-contrast technical surfaces. |
| Paper | Application canvas. Use as the page background, not as a card substitute. |
| Product typography | IBM Plex Sans for all product UI. IBM Plex Mono only for code, file paths, identifiers, hashes, version strings, timestamps, telemetry-like values, and configuration snippets. |
| Newsreader | Marketing/display only. It must not appear in Workbench product UI. |
| Motion | Fast, calm, functional. No bounce, theatrical transition, or decorative animation. |
| Colour as signal | Colour must not be the only carrier of meaning. Pair status colour with text, icon, label, or position where the state is actionable. |

## 3. Radius policy

Workbench uses product-panel radius as an operational UI exception to the stricter structural-container rule in the base design system.

| Element | Radius |
|---|---|
| Application shell, app bar, drawer, page canvas | Sharp / no radius. |
| Standard product panel or card | `--radius-lg` unless the component has a stricter local rule. |
| Compact grouped surface inside a panel | `--radius-md` or `--radius-sm`. |
| Inputs and compact controls | MudBlazor default or `--radius-sm`, depending on component capability. |
| Chips and pill controls | `--radius-pill`. |
| Dialog surfaces | `--radius-lg`, with a consistent dialog component. |
| Code blocks and diff blocks | `--radius-sm` for embedded previews; `--radius-md` for standalone code panels. |

Do not apply panel radius globally to every `MudPaper`. Use named classes or wrapper components so cards, dialogs, tables, nested blocks, and utility surfaces can evolve independently.

## 4. MudBlazor colour role mapping

MudBlazor colour names are implementation tools, not design-system semantics. Workbench must avoid using `Color.Primary` as a generic highlight.

| MudBlazor colour | Allowed Workbench use |
|---|---|
| `Color.Primary` | True primary action only: create, save, accept, submit, or continue. Avoid on chips, tab badges, metadata, filters, and passive selected states. |
| `Color.Secondary` | Secondary structural action where MudBlazor colour is needed. Prefer design-system wrapper components for repeated use. |
| `Color.Info` | Informational status only, not generic blue decoration. |
| `Color.Success` | Successful validation, acceptance readiness, accepted state, repaired state. |
| `Color.Warning` | Stale content, missing gate, pending approval, recoverable operational issue. |
| `Color.Error` | Failed check, destructive action, rejection, unrecoverable error, blocking failure. |
| `Color.Default` | Neutral state where no semantic colour is needed. |

When a colour appears repeatedly for the same meaning, introduce a component variant or CSS class rather than repeating `Color.*` or inline styles.

## 5. Page anatomy

Every Workbench page should follow the same structure.

| Region | Required behaviour |
|---|---|
| Page title | Uses a standard page-header component. One title only. |
| Description | One short explanatory sentence. Avoid implementation details unless the page is operator-only. |
| Primary action | At most one primary/orange action in the header or toolbar. |
| Secondary actions | Use neutral or slate treatment. Do not compete with the primary action. |
| Toolbar | Search, filters, and refresh actions live in a consistent toolbar row. |
| Main content | Tables, panels, or workspaces. Avoid loose unwrapped content blocks. |
| Loading state | Linear progress or skeleton inside the content area. |
| Empty state | Standard empty-state component with optional next action. |
| Error/blocking state | Standard alert or gate checklist with human-readable action guidance. |

## 6. Workbench shell

The shell contains the dark app bar, logo, product name, POC/operator indicator, side navigation, and page container.

| Shell element | Rule |
|---|---|
| App bar | Dark ink surface. It should not carry page-specific actions except global navigation controls. |
| Logo | Use the light logo on dark app bar surfaces. Keep opacity restrained. |
| Product name | Secondary inverse text. Do not over-emphasise with large typography. |
| POC indicator | Small uppercase metadata. It must not look like a warning or primary action. |
| Drawer | White or primary surface with a subtle divider. Active navigation uses slate/neutral, not orange. |
| Page container | Central constrained width by default. Dense workspaces may opt into wider layouts when documented. |

## 7. Operational tables

Workbench tables are dense operational objects. They must remain readable under many rows and many technical identifiers.

| Table rule | Requirement |
|---|---|
| Header | Uppercase or compact metadata styling, slate text, clear divider. |
| Row density | Dense tables are allowed for operational lists. Do not reduce legibility below 14px body text. |
| Row click | Clickable rows must have pointer cursor and hover state. Actions inside a row must stop propagation. |
| Action column | Keep row actions on the right. Avoid multiple high-contrast buttons per row. |
| Technical values | Use mono for keys, slugs, IDs, hashes, paths, versions, and timestamps when needed. |
| Empty state | Use a standard empty state, not only a bare alert. |
| Pagination | Add when row count can exceed a short operational list. |
| Mobile | Preserve `DataLabel` values and ensure long code/paths wrap safely. |

## 8. Chips and badges

Chips must be typed by meaning. Do not treat every chip as a status chip.

| Chip type | Use | Colour rule |
|---|---|---|
| Status chip | Change, check, issue, lifecycle state. | Semantic success/warning/error/info/neutral tokens. |
| Metadata chip | Actor, governance effect, file type, workflow effect, profile-like metadata. | Slate or neutral. Never orange. |
| Filter chip | Table/list filter selection. | Neutral default, slate selected. Never orange. |
| Version chip | Accepted version or display version. | Neutral/slate or success only when it explicitly means accepted state. Never orange. |
| Count badge | Tab/list count. | Neutral/slate. Use warning/error only when count represents a warning/error. |
| Severity chip | Security severity or operational severity. | Use approved severity/status tokens and include text. |

## 9. Change workspace pattern

A Change workspace is the primary operational surface. It should expose context, gates, content, validation, review, and acceptance without forcing the user through separate pages.

| Section | Rule |
|---|---|
| Header | Show change key, title, status, governance effect, actor context if relevant, and primary available action. |
| Readiness | Gate checklist appears near the top before tabs. It explains blockers and gives direct actions. |
| Content | Draft files are grouped by content type. File paths use mono. Editing controls are secondary unless saving. |
| Draft vs accepted | Use a standard comparison surface with accepted and draft clearly labelled. Do not rely only on background colour. |
| Validation | Show summary first, then check table. Failures must include what failed and what to fix when available. |
| Review | Show approval requirement and review history. Self-approval blocking must be explicit and non-technical. |
| Acceptance | Accept is primary only when gates are satisfied. Blocked acceptance uses gate guidance, not a disabled button alone. |
| Destructive actions | Close/delete/deprecate require confirmation. Use standard dialog component. |

## 10. Gate checklist pattern

The gate checklist explains why a change cannot be accepted or confirms that it is ready.

| Gate state | Display rule |
|---|---|
| Ready | Success state, clear confirmation, one primary accept action. |
| Checks missing | Warning row, action to run checks. |
| Checks failed | Error or warning row depending on blocking severity, action to show or rerun checks. |
| Review missing | Warning row, action to request or record review. |
| Self-approval blocked | Warning row, no misleading action for the author. |
| Approval stale | Warning row, action to request review again. |
| Base stale | Warning row, explain that a newer accepted version exists. |
| Terminal | Neutral state; do not present active gate actions. |

Each gate row must include a readable message and, where useful, a single action. Avoid icon-only gates.

## 11. Code, markdown, and diff surfaces

Workbench contains technical content. Technical surfaces need dedicated patterns.

| Surface | Rule |
|---|---|
| Inline code | Mono, compact background, small radius, no excessive contrast. |
| Code block | Mono, controlled line height, safe wrapping option, max height for embedded previews. |
| File preview | Shows logical path, content type, size, and content preview. |
| Markdown preview | Uses product typography, not marketing typography. Links and blockquotes use slate/neutral treatment. |
| Accepted vs draft comparison | Two-column layout on desktop, stacked on mobile. Labels must be text, not only colour. |
| Diff state | Added/changed/removed/unchanged must use explicit labels and compatible colours. |

## 12. Dialogs and destructive actions

Do not build fixed-position modal surfaces directly in page markup. Use a reusable confirmation dialog or MudBlazor dialog service.

| Dialog rule | Requirement |
|---|---|
| Title | Short and specific. |
| Message | Explain consequence, not implementation detail. |
| Required reason | Use for closing changes or rejecting work where audit context matters. |
| Primary confirm | Use error colour only for destructive confirmation. Use primary only for constructive confirmation. |
| Cancel | Always available and visually secondary. |
| Focus | Initial focus should not accidentally confirm destructive action. |
| Escape/click-away | Must be deliberate according to action severity. |

## 13. Admin and operator surfaces

Settings and recovery views are operator surfaces. They may expose infrastructure state, but they still need product-level clarity.

| Pattern | Rule |
|---|---|
| Metric tile | One metric, one label, optional icon. Avoid decorative colour. |
| Configuration panel | Plain current value, explanation, and safe action if applicable. |
| Recovery table | Shows state, evidence, guidance, recommended action, and repair action. |
| POC controls | Clearly labelled as POC/development controls. Do not mix with normal user workflow. |
| Internal paths/IDs | Mono and wrapped safely. |

## 14. Accessibility and responsiveness

Workbench must remain usable under keyboard navigation, small screens, long identifiers, and colour-vision constraints.

| Requirement | Rule |
|---|---|
| Focus state | Use the design-system focus ring. Do not suppress focus without replacement. |
| Touch targets | Interactive controls should remain practically clickable, even in dense tables. |
| Colour independence | Status meaning must appear in text or icon as well as colour. |
| Long values | Paths, hashes, IDs, and URLs must wrap or truncate intentionally. |
| Mobile tables | Use responsive labels and avoid hidden critical actions. |
| Reduced motion | Respect `prefers-reduced-motion`. |

## 15. Implementation checklist for UI changes

Use this checklist for every Workbench UI pull request.

| Check | Pass condition |
|---|---|
| Orange use | `Color.Primary`, `--brand-accent`, and CTA tokens appear only on true primary actions. |
| Inline styles | No new inline styles unless there is a documented one-off technical constraint. Prefer component variants or CSS classes. |
| Page header | Page uses the standard page-header pattern. |
| Tables | Tables follow toolbar, empty-state, row-click, and action-column conventions. |
| Chips | Chips use status, metadata, filter, version, or count semantics. |
| Dialogs | Confirmation/destructive flows use the standard dialog pattern. |
| Code/diff | Code, file, markdown, and comparison surfaces use standard technical-content patterns. |
| Typography | Product UI uses IBM Plex Sans; mono is limited to technical values. |
| Accessibility | Focus, keyboard, non-colour signal, and responsive behaviour are preserved. |
| Terminology | UI shows user concepts and governance effects, not implementation mechanisms. |

## 16. Initial migration priorities

1. Remove orange from non-action chips, badges, tab counters, and selected filters.
2. Replace global paper/button/chip overrides with scoped classes or wrapper components.
3. Introduce standard page header, panel, toolbar, chip, empty-state, code-block, and confirm-dialog components.
4. Refactor list pages before the Change workspace.
5. Break Change Detail into smaller workspace components after primitives are stable.
6. Add lightweight style-regression checks for inline styles, orange misuse, manual overlays, and raw status chips.
