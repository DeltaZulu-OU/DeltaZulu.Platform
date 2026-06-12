# ADR-0015: Investigation notes as Markdown files in Git with static asset support

## Status

Accepted (Phase 1).

## Context

ADR-0014 delegated case management to external systems (FlowIntel, TheHive) but left a
gap: air-gapped SOCs with no access to an external system need a surface for investigation
context. Three options were evaluated:

1. **Knowledge graph** — dominated; reproduces FlowIntel's capability inside the workbench.
2. **Wiki** — dominated; a separate product surface requiring its own content lifecycle.
3. **Markdown files in Git** — rides the existing ChangeRequest → review → merge → Git
   lifecycle. Zero new infrastructure.

## Decision

### Investigation notes

Investigation notes are Markdown files with optional YAML frontmatter, stored inside the
detection's Git directory under a `notes/` subdirectory:

```text
detections/<slug>/
├── detection.yaml
├── rule.kql
├── tests/baseline.yaml
└── notes/
    └── 2026-01-15-initial-investigation.md
```

YAML frontmatter carries lightweight structure:

```yaml
---
external_case: { system: flowintel, id: "FI-42" }
tags: [apt28, credential-access, T1110]
observables:
  - { type: ip, value: "203.0.113.42" }
  - { type: user, value: "admin@contoso.com" }
---
```

Notes use `DraftContentType.InvestigationNote` and flow through the standard change request
lifecycle.

### Cross-detection investigation notes

Notes spanning multiple detections live under a top-level `investigations/` directory:

```text
investigations/
└── 2026-01-brute-force-campaign/
    └── notes.md
```

The frontmatter of a cross-detection note lists the detection slugs it references:

```yaml
---
detections: [anomalous-sign-in, brute-force-spray]
---
```

### Cross-document links

Links between documents inside the Git tree use **standard relative paths**:

```markdown
See also `brute-force-spray/detection.yaml`
```

Relative paths work in any Markdown renderer (GitHub, VS Code, local preview). The
workbench's Blazor Markdown viewer rewrites internal relative paths to Blazor routes at
render time using Markdig's AST pipeline. External URLs are rendered as standard links.

### Static assets

Images, diagrams, pcap excerpts, PDFs, and other binary files accompanying notes or
detections use `DraftContentType.StaticAsset`:

```text
detections/<slug>/
└── notes/
    ├── investigation.md
    └── assets/
        ├── auth-log-timeline.png
        └── packet-capture-excerpt.pcap
```

In the draft phase, static asset content is base64-encoded in `ChangeDraftFile.Content`.
The canonical writer decodes to binary before committing to Git. The check pipeline skips
text-based validation for `StaticAsset` files.

Notes reference assets with relative paths:

```markdown
`assets/auth-log-timeline.png`
```

### Markdown rendering

The workbench uses Markdig to render Markdown to HTML in a Blazor component. The rendering
pipeline includes a custom link rewriter that:

1. Identifies links whose `href` begins with a relative path (no scheme, no leading `/`).
2. Resolves the relative path against the current file's logical path in the Git tree.
3. Maps the resolved path to a Blazor route (e.g. `/detections/<slug>/notes/<file>`).
4. Leaves external URLs (`https://...`) unchanged.

### Path convention summary

| Content type          | Path pattern                                          |
|-----------------------|-------------------------------------------------------|
| Detection metadata    | `detections/<slug>/detection.yaml`                    |
| Hunting query         | `detections/<slug>/rule.<ext>`                        |
| Test definition       | `detections/<slug>/tests/<name>.yaml`                 |
| Test fixture          | `detections/<slug>/fixtures/<name>.<ext>`             |
| Investigation note    | `detections/<slug>/notes/<name>.md`                   |
| Note static asset     | `detections/<slug>/notes/assets/<filename>`            |
| Cross-detection note  | `investigations/<slug>/notes.md`                      |
| Cross-detection asset | `investigations/<slug>/assets/<filename>`              |

## Consequences

### Positive

- Zero new infrastructure — notes are files flowing through the existing lifecycle.
- Air-gap portable — `git clone` produces detections and investigation context.
- YAML frontmatter is parseable for future correlation/search without a graph engine.
- Relative links work outside the workbench (GitHub rendering, VS Code preview).
- Static assets live alongside their notes; no external blob store required for POC.

### Negative

- Base64-encoded binary in `ChangeDraftFile.Content` increases database row size during
  draft phase. Acceptable for POC; production may stream to a blob store.
- Cross-document link integrity is not validated until a check pipeline check is added.
- Markdown rendering fidelity depends on Markdig; complex formatting (Mermaid diagrams,
  LaTeX) requires Markdig extensions added incrementally.

## Re-evaluation triggers

- If air-gapped SOCs require structured observable auto-correlation, a lightweight
  structured store (SQLite FTS5 over frontmatter fields) should be evaluated.
- If static asset sizes routinely exceed 10 MB, a blob store split is warranted.
