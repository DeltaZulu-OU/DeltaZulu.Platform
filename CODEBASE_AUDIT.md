# DeltaZulu.Platform — Codebase Audit Report

**Date:** 2026-06-14
**Scope:** Full repository — 419 C# files, 30,112 lines across 4 source projects + 1 test project

---

## Solution Structure

```
DeltaZulu.Platform.Domain       (net10.0) — no dependencies
DeltaZulu.Platform.Application  (net10.0) → Domain
DeltaZulu.Platform.Data         (net10.0) → Domain, Application ⚠️
DeltaZulu.Platform.Web          (net10.0) → Domain, Application, Data
DeltaZulu.Platform.Tests        (net10.0) → all projects
```

---

## Findings by Severity

### STRUCTURAL (affects architecture)

---

#### S1. Data → Application dependency violates layering

**Problem:** `DeltaZulu.Platform.Data.csproj` has a `<ProjectReference>` to `DeltaZulu.Platform.Application`. In Clean Architecture, Data (infrastructure) should depend only on Domain (and optionally Application abstractions defined in Domain). The current graph creates an upward dependency from infrastructure into application logic.

**Where:**
- `src/DeltaZulu.Platform.Data.DuckDb/DuckDb/QueryRuntime.cs` — `using DeltaZulu.Platform.Application.Analytics.Planning`
- `src/DeltaZulu.Platform.Data.DuckDb/DuckDb/QueryRuntime.DataOnly.cs` — `using DeltaZulu.Platform.Application.Analytics.Planning`, `.Translation`
- `src/DeltaZulu.Platform.Data/Seeding/SampleSavedQuerySeeder.cs` — `using DeltaZulu.Platform.Application.Analytics.Samples`

**Why it matters:** This creates a near-circular dependency (Web → Data → Application → Domain, and Web → Application → Domain). It prevents the Application layer from being tested without the Data layer, and couples infrastructure to application-level orchestration.

**Proposed resolution:** Move the contracts/interfaces that Data needs (e.g., `IRelationalPlanner`, `ApprovedViewCatalog`) into Domain. If Data needs to call Application-level orchestration (translation + planning + execution), that orchestration should be inverted: Application calls Data through an interface, not the other way around.

---

#### S2. RelationalPlanner namespace/folder mismatch — wrong layer

**Problem:** 7 files physically located in `src/DeltaZulu.Platform.Application/Analytics/Planning/` declare namespace `DeltaZulu.Platform.Domain.Analytics.Planning`. The code lives in Application but pretends to be Domain.

**Where:**
- `RelationalPlanner.cs` (548 lines)
- `RelationalPlanner.ProjectionPruningPass.cs`
- `RelationalPlanner.CommonScalarHoistPass.cs`
- `RelationalPlanner.FilterPushdownPass.cs`
- `RelationalPlanner.LookupOutputBindingPass.cs`
- `RelationalPlanner.FilterExtendInlinePass.cs`
- `RelationalPlanner.IdentityProjectionCollapsePass.cs`

**Why it matters:** Developers looking at the namespace assume this is Domain logic with no infrastructure dependencies. Developers looking at the folder assume it follows Application-layer rules. The mismatch makes dependency analysis unreliable and IDE navigation misleading.

**Proposed resolution:** Decide where this belongs. If it's pure domain logic (no infrastructure dependencies), move the files to `src/DeltaZulu.Platform.Domain/Analytics/Planning/`. If it requires Application-level dependencies, change the namespace to `DeltaZulu.Platform.Application.Analytics.Planning`.

---

#### S3. Duplicate `IDetectionRepository` interface — two different contracts with the same name

**Problem:** Two unrelated interfaces share the name `IDetectionRepository` in different namespaces:
- `Domain/Governance/Contracts/IDetectionRepository.cs` — CRUD for governed detection entities (`GetByIdAsync`, `GetBySlugAsync`, `ListAsync`, `Add`, `Save`)
- `Domain/Analytics/Detections/IDetectionRepository.cs` — analytics detection store (`EnsureInitializedAsync`, `GetLatestVersionAsync`, `SaveAsync`, `SetEnabledAsync`, `DeleteAsync`)

**Where:** Both in `src/DeltaZulu.Platform.Domain/`

**Why it matters:** Same type name in the same project creates DI registration ambiguity and developer confusion. `using` directives can silently resolve to the wrong one.

**Proposed resolution:** Rename to make intent explicit. E.g., `IGovernanceDetectionRepository` vs `IAnalyticsDetectionRepository`, or unify if they represent the same aggregate.

---

#### S4. Three unused interfaces — dead contracts

**Problem:** Three interfaces have zero implementations and zero usages in application code:
- `Domain/Governance/Contracts/ICandidateDecisionRepository.cs`
- `Domain/Governance/Contracts/ICandidateProvider.cs`
- `Domain/Governance/Contracts/IIncidentRepository.cs`

**Where:** `src/DeltaZulu.Platform.Domain/Governance/Contracts/`

**Why it matters:** Dead code increases cognitive load and creates false extension points. ROADMAP.md confirms these are "scaffolded" with no implementation.

**Proposed resolution:** Delete them. They can be re-created from version control when the feature is actually built.

---

#### S5. God class — `KustoQueryTranslator` (895 lines, 41 private methods)

**Problem:** Single class handles all KQL-to-RelNode translation: operator dispatch, scalar expression translation, function call mapping, type coercion, binary operators, projection binding, and let-statement handling.

**Where:** `src/DeltaZulu.Platform.Application/Analytics/Translation/KustoQueryTranslator.cs`

**Why it matters:** Any change to scalar expression handling risks breaking operator translation. The class has a single public entry point (`Translate`) but 41 private methods with implicit coupling through shared state (`DiagnosticBag`, `ApprovedViewCatalog`).

**Proposed resolution:** Extract sub-translators: `KustoScalarTranslator`, `KustoOperatorTranslator`, `KustoProjectionTranslator` (partially exists already). Each gets the shared dependencies via constructor injection. The main class becomes a coordinator.

---

#### S6. God class — `ChangeRequest` (436 lines, 13 public methods, 5 responsibilities)

**Problem:** Single domain entity manages: (1) draft content, (2) check execution pipeline, (3) review workflow, (4) merge readiness evaluation, (5) lifecycle state machine (Draft → ChecksRunning → ReviewRequired → ReadyToAccept → Merged → Published/Closed).

**Where:** `src/DeltaZulu.Platform.Domain/Governance/Changes/ChangeRequest.cs`

**Why it matters:** Five distinct reasons to change. Adding a new check gate requires modifying the same class as adding a new review policy. The merge readiness evaluation (`EvaluateMergeReadiness`) is interleaved with state transition logic.

**Proposed resolution:** Extract value objects: `CheckPipeline` (owns check lifecycle), `ReviewAggregate` (owns approval/rejection tracking), `MergeReadinessEvaluator` (pure function that takes checks + reviews → readiness). `ChangeRequest` becomes a coordinator that delegates to these.

---

### MODERATE (affects maintainability)

---

#### M1. EnsureInitializedAsync boilerplate duplicated across 12 repositories

**Problem:** Every Dapper repository implements the identical double-checked locking pattern:

```csharp
private readonly SemaphoreSlim _schemaSemaphore = new(1, 1);
private bool _initialized;

public async Task EnsureInitializedAsync(CancellationToken ct = default)
{
    if (_initialized) return;
    await _schemaSemaphore.WaitAsync(ct);
    try
    {
        if (_initialized) return;
        await using var conn = await _connectionFactory.OpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(CreateSchemaSql, cancellationToken: ct));
        _initialized = true;
    }
    finally { _schemaSemaphore.Release(); }
}
```

**Where:** All 12 `Dapper*Repository` classes in `src/DeltaZulu.Platform.Data/Sqlite/`

**Why it matters:** 12 copies of the same concurrency logic. A bug fix (e.g., handling `OperationCanceledException` during semaphore wait) requires 12 edits.

**Proposed resolution:** Extract an abstract `DapperRepositoryBase` with a protected `CreateSchemaSql` property and the shared initialization logic. Or use a `SchemaInitializer` utility that repositories delegate to (one already exists at `SchemaInitializer.cs` but repositories don't use it).

---

#### M2. DateTime formatting helpers duplicated in 8 repositories despite centralized helper existing

**Problem:** `SqliteDateTimeHelpers.cs` provides `Format()`, `Parse()`, `NormalizeUtc()`. Only `DapperSavedQueryRepository` uses it (via `using static`). The other 8 Dapper repositories define private `FormatDateTime()`, `ParseDateTime()`, `NormalizeUtc()` methods with identical logic.

**Where:**
- `DapperDetectionRepository.cs` (lines 377–386)
- `DapperVisualizationRepository.cs` (lines 237–246)
- `DapperAlertRepository.cs`, `DapperCandidateEvidenceRepository.cs`, `DapperIncidentCandidateRepository.cs`, `DapperAlertEntityRepository.cs`, `DapperQueryHistoryRepository.cs`, `DapperDetectionRunRepository.cs`

**Why it matters:** A timezone handling bug fix would need to be applied in 9 places instead of 1.

**Proposed resolution:** Delete private DateTime methods from all 8 repositories. Add `using static DeltaZulu.Platform.Data.Sqlite.Analytics.SqliteDateTimeHelpers;` to each.

---

#### M3. Validation check boilerplate — repeated file-filtering and error-collection patterns

**Problem:** All 6 validation check classes repeat the same patterns:
1. Filter `context.DraftFiles` by `ContentType`, return `Skip` if empty
2. Collect errors into `List<string>`, return `Pass` or `Fail` based on count
3. YAML parsing boilerplate (3 checks: `PackageSchemaCheck`, `NoteFrontmatterCheck`, `TestDefinitionCheck`)

**Where:** `src/DeltaZulu.Platform.Application/Governance/Validation/Checks/*.cs`

**Why it matters:** Adding a new check requires copying 15–20 lines of boilerplate. The file-filtering logic is especially fragile — a change to `DraftContentType` enum could require updating 6 checks.

**Proposed resolution:** Create a `CheckBase` class with `FilterFiles(context, contentType)` and `CollectErrors(errors)` template methods. YAML checks get a `YamlCheckBase` that handles stream loading and root node extraction.

---

#### M4. Repetitive repository initialization chain

**Problem:** `ApplicationPersistenceServiceCollectionExtensions.cs` (lines 57–96) retrieves and initializes each repository individually with the same pattern repeated 10 times:

```csharp
var detections = services.GetRequiredService<IDetectionRepository>();
await detections.EnsureInitializedAsync(cancellationToken);
var runs = services.GetRequiredService<IDetectionRunRepository>();
await runs.EnsureInitializedAsync(cancellationToken);
// ... 8 more
```

**Where:** `src/DeltaZulu.Platform.Data/Sqlite/Analytics/ApplicationPersistenceServiceCollectionExtensions.cs`

**Why it matters:** Every new repository requires adding 2 more lines. Easy to forget, causing runtime `SqliteException` for missing tables.

**Proposed resolution:** Register all repositories that implement a marker interface (e.g., `IRequiresSchemaInit`). At startup, resolve all `IRequiresSchemaInit` from the container and initialize them in a loop.

---

#### M5. Stringly-typed time filter dispatch

**Problem:** Time filter selection uses magic strings `"none"`, `"custom"`, `"1h"`, `"6h"`, `"12h"`, `"24h"`, `"7d"` scattered across a 340-line class.

**Where:** `src/DeltaZulu.Platform.Web/Analytics/Services/QueryToolbarState.cs` (lines 19–26, 100–105)

**Why it matters:** A typo in a filter key silently produces wrong behavior. No compile-time exhaustiveness checking. Adding a new preset requires changes in multiple disconnected locations.

**Proposed resolution:** Define a `TimeFilterPreset` enum or a sealed record hierarchy. Replace string comparisons with pattern matching.

---

#### M6. Stringly-typed legend visibility

**Problem:** Legend visibility is determined by comparing a free-text string against 4 magic values (`"hidden"`, `"hide"`, `"none"`, `"off"`).

**Where:** `src/DeltaZulu.Platform.Web/Analytics/Rendering/EChartsRenderOptionsBuilder.cs` (lines 103–111)

**Why it matters:** No single source of truth for valid legend values. Content authors have no guidance on which string to use.

**Proposed resolution:** Define a `LegendVisibility` enum (`Visible`, `Hidden`). Parse the string once at the boundary (when loading render directives), then use the enum throughout.

---

#### M7. Bare catch block swallows all exceptions

**Problem:** `TryEstimateReferencedVolume()` catches all exceptions (including `OutOfMemoryException`, `ThreadAbortException`) without logging, typing, or context.

**Where:** `src/DeltaZulu.Platform.Data.DuckDb/DuckDb/QueryRuntime.cs` (lines 535–539)

```csharp
catch
{
    estimatedRows = 0;
    return false;
}
```

**Why it matters:** Makes production debugging impossible. Connection failures, SQL errors, and catastrophic exceptions are all silently swallowed.

**Proposed resolution:** Catch specific expected exceptions (e.g., `DuckDBException`). Log unexpected exceptions at Warning level before returning false.

---

#### M8. Obsolete API usage with pragma suppression

**Problem:** Calling deprecated Elsa 3.7 `TriggerWorkflowsAsync` method with `#pragma warning disable CS0618`.

**Where:** `src/DeltaZulu.Platform.Application/Governance/Workflow/ElsaWorkflowOrchestrator.cs` (lines 78–87)

**Why it matters:** Future Elsa upgrades will remove this API. The pragma hides the migration signal.

**Proposed resolution:** Track Elsa upgrade as a backlog item. Add a comment with the replacement API from Elsa docs. Consider wrapping in an adapter so the pragma is in one place.

---

#### M9. Namespace mismatch — `RenderDirectiveParser`

**Problem:** One additional namespace/folder mismatch beyond S2:
- `src/DeltaZulu.Platform.Application/Analytics/Rendering/Directives/RenderDirectiveParser.cs` — folder `Rendering.Directives`, namespace `Application.Analytics.Render.Directives` (missing "ing")

**Where:** Listed above

**Why it matters:** Breaks IDE "Go to file from namespace" navigation. Creates confusion about module boundaries.

**Proposed resolution:** Align namespaces to match folder paths.

---

#### M10. String-based AST node type matching

**Problem:** Kusto AST node type identified by string comparison instead of type pattern matching:

```csharp
var listNode = children
    .FirstOrDefault(c => c.GetType().Name == "ExpressionList")
```

**Where:** `src/DeltaZulu.Platform.Application/Analytics/Translation/KustoQueryTranslator.cs` (line 759)

**Why it matters:** If the Kusto.Language SDK renames internal types, this silently breaks without a compiler warning. No IDE refactoring support.

**Proposed resolution:** Use `is ExpressionList` pattern matching if the type is accessible. If it's internal to the SDK, document the constraint and add a runtime assertion with a clear error message.

---

### MINOR (cosmetic / naming)

---

#### N1. Redundant catch-and-rethrow

**Problem:** `catch (OperationCanceledException) { throw; }` — catch block that only re-throws is dead code.

**Where:** `src/DeltaZulu.Platform.Web/Analytics/Services/QueryService.cs` (line 87)

**Proposed resolution:** Remove the catch block.

---

#### N2. `SuppressMessage` for CA1720 on type enums

**Problem:** Both `KustoType.cs` and `DuckDbType.cs` suppress CA1720 ("Identifier contains type name") because enum members mirror external type systems.

**Where:**
- `src/DeltaZulu.Platform.Domain/Analytics/Schema/KustoType.cs` (line 6)
- `src/DeltaZulu.Platform.Domain/Analytics/Schema/DuckDbType.cs` (line 6)

**Why it matters:** Cosmetic. The suppression is justified and documented.

**Proposed resolution:** Keep as-is. The justification string is adequate.

---

#### N3. Function argument validator uses string dispatch

**Problem:** `KustoFunctionArgumentValidator` converts function names to lowercase and matches against hardcoded string literals in a switch statement. The function name is then passed as a string to `RequireCount()`.

**Where:** `src/DeltaZulu.Platform.Application/Analytics/Translation/KustoFunctionArgumentValidator.cs` (lines 29–41)

**Why it matters:** Low severity because this is at a system boundary (external KQL input). But typos in case labels won't be caught.

**Proposed resolution:** Define function names as constants in a static class. Use those constants in both the switch and `RequireCount()`.

---

#### N4. Manual null checks instead of `ArgumentNullException.ThrowIfNull()`

**Problem:** Constructor null-guard pattern `_x = x ?? throw new ArgumentNullException(nameof(x))` used throughout instead of the modern `ArgumentNullException.ThrowIfNull(x)`.

**Where:** Multiple files, e.g., `src/DeltaZulu.Platform.Web/Analytics/Library/LibraryPageController.cs` (lines 17–19)

**Why it matters:** Cosmetic. Both are correct. The modern form is slightly more concise.

**Proposed resolution:** Low priority. Adopt `ThrowIfNull` in new code. Don't churn existing files just for this.

---

## Test Coverage Map

| Module | Coverage | Quality | Key Gaps |
|--------|----------|---------|----------|
| Analytics/Translation | 95% | Excellent — behavior-driven, edge cases | `KustoFunctionArgumentValidator`, `KustoLiteralReader` not isolated |
| Analytics/Planning | 80% | Excellent — tree-shape assertions | Individual planner passes not tested in isolation |
| Domain/Governance | 90% | Excellent — full lifecycle/state machine | Value objects not isolated |
| Data/Sqlite Repos | 85% | Good — real in-memory SQLite | No unit tests; all integration |
| Data/DuckDb | 75% | Good — end-to-end SQL verification | `QueryRuntime.cs` (547 lines) has no direct unit test |
| Web/Analytics | 70% | Good — component + controller tests | `QueryToolbarState` (340 lines), `LanguageService` untested |
| Governance/Application | 65% | Fair — integration-focused | `ElsaWorkflowOrchestrator`, `DomainDrivenOrchestrator` untested |
| Data/Seeding | 0% | None | `SampleDetectionContentSeeder` (761 lines), `DemoSeeder` (489 lines) |

**Testing approach:** MSTest framework, no mocking library (hand-written fakes), behavior-driven assertions. Quality is high where tests exist — tests verify observable outcomes, not implementation details.

**Largest untested files:**
1. `KustoQueryTranslator.cs` (895 lines) — indirectly tested via integration
2. `DuckDbSqlShapeRewriter.cs` (817 lines) — no direct test
3. `SampleDetectionContentSeeder.cs` (761 lines) — no test
4. `DuckDbJoinEmitter.LookupProjection.cs` (716 lines) — no direct test
5. `DashboardPageController.cs` (560 lines) — partial coverage

---

## Summary: Top 10 Recommended Actions

| # | Finding | Severity | Effort |
|---|---------|----------|--------|
| 1 | **S1** — Remove Data → Application dependency; move contracts to Domain | Structural | High |
| 2 | **S2** — Fix RelationalPlanner namespace/folder mismatch | Structural | Low |
| 3 | **S3** — Rename duplicate `IDetectionRepository` interfaces | Structural | Medium |
| 4 | **S4** — Delete 3 unused interfaces | Structural | Trivial |
| 5 | **M1** — Extract `DapperRepositoryBase` for EnsureInitializedAsync | Moderate | Medium |
| 6 | **M2** — Replace duplicated DateTime helpers with centralized `SqliteDateTimeHelpers` | Moderate | Low |
| 7 | **M3** — Extract validation check base class | Moderate | Medium |
| 8 | **M5/M6** — Replace stringly-typed dispatch with enums | Moderate | Low |
| 9 | **M7** — Fix bare catch block in QueryRuntime | Moderate | Trivial |
| 10 | **M9** — Align remaining namespace/folder mismatches | Moderate | Trivial |

---

**End of Phase 1 report. Awaiting review and approval before proceeding to Phase 2 refactoring.**
