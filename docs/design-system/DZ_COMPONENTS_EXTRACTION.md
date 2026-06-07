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
- confirmation dialogs
- DeltaZulu token CSS
- scoped DeltaZulu component CSS

Workbench-specific adapters remain in `Workbench.Web` when they depend on Workbench domain types. Examples include review-decision or comparison-status adapters that map domain enums to generic DeltaZulu chips.

## Consumption

Apps must reference the library project or package and load its static assets after MudBlazor:

```html
<link rel="stylesheet" href="_content/MudBlazor/MudBlazor.min.css" />
<link rel="stylesheet" href="_content/DeltaZulu.Blazor.Components/deltazulu-tokens.css" />
<link rel="stylesheet" href="app.css" />
<link rel="stylesheet" href="_content/DeltaZulu.Blazor.Components/dz-components.css" />
```

Apps should add:

```razor
@using DeltaZulu.Blazor.Components
```

## Boundary rule

The component library must not reference Workbench domain, application, persistence, workflow, or validation projects. Domain-specific UI adapters should compose the generic components in the consuming app.
