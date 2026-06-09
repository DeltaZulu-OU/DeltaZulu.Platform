param(
    [string]$Root = "."
)

$ErrorActionPreference = "Stop"

Write-Host "Remaining primitive migration targets in Workbench page/component code:"
Write-Host ""

$targets = @(
    (Join-Path $Root "src/Workbench.Web/Components/Pages"),
    (Join-Path $Root "src/Workbench.Web/Components/Shared")
)

grep -RE --exclude-dir=.vs --exclude-dir=bin --exclude-dir=obj `
    --exclude="refactor-dz-primitives.ps1" `
    --exclude="apply-dz-remaining-primitives.ps1" `
    --exclude="finalize-dz-primitive-cleanup.ps1" `
    --exclude="repair-mixed-action-links.ps1" `
    --exclude="rollback-dz-complex-page-corruption.ps1" `
    "dz-detail-list|dz-tab-body|dz-link-button|<div class=`"dz-table-shell" `
    $targets

Write-Host ""
Write-Host "Note: complex detail pages are intentionally left for explicit manual refactors after a clean build."
