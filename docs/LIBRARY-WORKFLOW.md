# Library Workflow

## Purpose

The Library page is the primary management surface for reusable hunting artifacts. It brings saved searches, visualizations, and dashboards into one place without changing project boundaries or persistence ownership.

The Query Library drawer remains useful for editor-local work, recent history, and quick save/open actions. The Library page is for artifact discovery, opening, deletion, dependency visibility, and later richer management.

## Artifact model

```text
Saved search
  -> query text without requiring a render clause

Visualization
  -> saved query reference
  -> visualization/render specification
  -> opens back into the editor as saved query + terminal | render clause

Dashboard
  -> dashboard definition
  -> widgets
  -> widgets may reference saved visualizations
```

The intended dependency chain is:

```text
Saved search
  -> Visualization
      -> Dashboard widget
```

Direct query-text dashboard widgets still exist for compatibility. They should not be removed until migration and UX coverage are explicitly planned.

## Current user stories

| User story | Current status |
|---|---|
| As an analyst, I can see saved searches, visualizations, and dashboards in one Library page. | Implemented |
| As an analyst, I can filter Library items by type. | Implemented |
| As an analyst, I can search Library items by name, description, dependency label, or type. | Implemented |
| As an analyst, I can open a saved search into the threat-hunting editor. | Implemented |
| As an analyst, I can open a saved visualization into the threat-hunting editor as query text plus terminal render clause. | Implemented |
| As an analyst, I can open a dashboard from the Library. | Implemented |
| As an analyst, I can create starter saved-search and visualization drafts from the Library. | Implemented as editor seeding |
| As an analyst, I can delete Library items through protected delete paths. | Implemented |
| As an analyst, I get a two-step confirmation before deletion. | Implemented |
| As an analyst, I can page through large Library result sets. | Implemented |

## Current lifecycle protection

Deletion is delegated through existing protected paths.

| Deleted artifact | Protection |
|---|---|
| Saved search | Blocked when saved visualizations reference it. |
| Visualization | Blocked when dashboard widgets reference it. |
| Dashboard | Uses the existing dashboard repository delete path. |

These checks stay in Web/application-facing services. The DAL should not know dashboard or visualization lifecycle policy.

## Current routing behavior

The Library page opens saved searches and visualizations by placing query text into `EditorBus`, then navigating to `/threat-hunting`.

`EditorBus` stores one pending insert when the threat-hunting page is not yet mounted. This avoids losing the requested query during navigation.

Dashboards open directly through `/dashboards/{id}`.

## Project boundary rule

The Library page is a Web-layer composition surface.

Do not introduce new project decoupling for this feature unless there is a separate architectural reason.

Current intended placement:

| Concern | Location |
|---|---|
| Library page UI | `Hunting.Web/Pages/Library.razor` |
| Library aggregate service | `Hunting.Web/Services/LibraryService.cs` |
| Saved query persistence contract | `Hunting.Application` |
| Visualization persistence contract | `Hunting.Application` |
| DAL implementations | `Hunting.Data` |
| Dashboard persistence | Current dashboard Web persistence |
| Render parsing/model | `Hunting.Render` |

## Near-term next steps

| Priority | Step | Notes |
|---|---|---|
| 1 | Manual UI test of the full Library workflow. | Create saved search, create visualization, create dashboard, add visualization to dashboard, open/delete through Library. |
| 1 | Fix any compile/test failures from the Library sequence. | Run full `dotnet test`. |
| 2 | Add clearer empty-state guidance per filter. | Saved searches, visualizations, and dashboards should each explain the next action. |
| 2 | Add dependency/status emphasis. | Missing dependencies should stand out; healthy `OK` status can be visually quieter. |
| 2 | Add “Create dashboard” direct creation from Library. | Current behavior navigates to the dashboards page. Direct creation can be added later by reusing existing dashboard creation logic. |
| 3 | Add duplicate/copy actions. | Useful for dashboards and visualizations, but not required for first workflow. |
| 3 | Add usage details. | Example: show which dashboards use a visualization and which visualizations use a saved search. |

## Non-goals for the current phase

Do not remove the Query Library drawer yet.

Do not remove direct query-text dashboard widgets yet.

Do not move dashboard persistence or Library orchestration into `Hunting.Data`.

Do not add a new storage abstraction solely for the Library page.

Do not convert this into a generic BI-style object model. The product remains a threat-hunting workbench.
