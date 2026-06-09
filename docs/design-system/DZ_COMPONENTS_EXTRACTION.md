# DeltaZulu Blazor Components Extraction

`DeltaZulu.Blazor.Components` is a reusable Razor Class Library for DeltaZulu product UI components built on MudBlazor.

The library owns reusable design-system primitives only:

- page headers
- panels
- toolbars
- metadata, filter, and status chips
- empty states
- code blocks
- diff pairs
- file headers
- markdown rendering surfaces
- confirmation dialogs
- DeltaZulu token CSS
- scoped DeltaZulu component CSS

Workbench-specific adapters remain in `Workbench.Web` when they depend on Workbench domain types. Examples include review-decision, TLP, generic status, or comparison-status adapters that map domain/application enums to generic DeltaZulu chips.

## Consumption

Apps must reference the library project or package and load its static assets after MudBlazor:

```html
<link rel="stylesheet" href="_content/MudBlazor/MudBlazor.min.css" />
<link rel="stylesheet" href="_content/DeltaZulu.Blazor.Components/deltazulu-tokens.css" />
<link rel="stylesheet" href="_content/DeltaZulu.Blazor.Components/dz-components.css" />
<link rel="stylesheet" href="_content/DeltaZulu.Blazor.Components/dz-shell.css" />
<link rel="stylesheet" href="app.css" />
```

Apps should add:

```razor
@using DeltaZulu.Blazor.Components
```

## Boundary rule

The component library must not reference Workbench domain, application, persistence, workflow, or validation projects. Domain-specific UI adapters should compose the generic components in the consuming app.

## Current inventory

See [`docs/PLATFORM_MERGE_PREP.md`](../PLATFORM_MERGE_PREP.md) for the current reusable-UI inventory, Workbench-local adapter list, and central-host blockers.

## Markdown trust boundary

`DzMarkdownText` treats Markdown as untrusted by default and escapes raw HTML before rendering. This is raw HTML escaping, not a complete HTML sanitizer; future untrusted rich-content scenarios should add an explicit sanitizer policy. Products can opt into raw HTML with `AllowRawHtml`, but should do so only for trusted system-authored content. App-specific link routing stays outside the RCL through the component link mapper; Workbench keeps detection/investigation route mapping in its local `MarkdownViewer` adapter.
