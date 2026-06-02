# Monaco Atom One Light Theme Integration

This package adds the uploaded `Atom One Light` editor theme to the Monaco editor integration.

## Files

```text
src/Hunting.Web/wwwroot/js/monaco-themes/atom-one-light.json
src/Hunting.Web/wwwroot/js/monaco-theme-loader.js
src/Hunting.Web/Pages/_Host.cshtml
```

## Design

The uploaded theme is a VS Code/TextMate theme. Monaco does not consume `tokenColors` directly through `defineTheme`, so `monaco-theme-loader.js` preserves the original JSON file and maps the generic TextMate scopes into Monaco token rules used by the local KQL Monarch tokenizer.

The loader patches `window.huntingMonaco.init` rather than replacing `monaco.js`. This keeps the existing language registration, autocomplete, editor preservation, and Shift+Enter behavior intact.

## Required script order

`monaco-theme-loader.js` must be loaded immediately after `monaco.js` and before Blazor starts:

```html
<script src="~/js/monaco.js" asp-append-version="true"></script>
<script src="~/js/monaco-theme-loader.js" asp-append-version="true"></script>
```

If you do not want to overwrite `_Host.cshtml`, add only the second script line after the existing `monaco.js` script reference.
