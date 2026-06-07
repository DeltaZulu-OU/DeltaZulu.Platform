[CmdletBinding()]
param(
    [string] $Root = ".",
    [switch] $Strict
)

$ErrorActionPreference = "Stop"

$rootPath = Resolve-Path $Root
$webRoot = Join-Path $rootPath "src/Workbench.Web"

if (-not (Test-Path $webRoot)) {
    Write-Error "Could not find src/Workbench.Web under '$rootPath'. Run this script from the repository root or pass -Root."
}

$razorFiles = Get-ChildItem -Path $webRoot -Recurse -Filter "*.razor" |
    Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\"
    }

$allowedRawMudChipFiles = @(
    "src/Workbench.Web/Components/DesignSystem/DzMetaChip.razor",
    "src/Workbench.Web/Components/DesignSystem/DzFilterChip.razor",
    "src/Workbench.Web/Components/DesignSystem/DzStatusChip.razor",
    "src/Workbench.Web/Components/Shared/StatusChip.razor"
)

$allowedStyleFiles = @(
    "src/Workbench.Web/Components/App.razor"
)

$findings = New-Object System.Collections.Generic.List[object]

function Add-Finding {
    param(
        [string] $Rule,
        [string] $Severity,
        [string] $File,
        [int] $Line,
        [string] $Text,
        [string] $Guidance
    )

    $findings.Add([pscustomobject]@{
        Rule = $Rule
        Severity = $Severity
        File = $File
        Line = $Line
        Text = $Text.Trim()
        Guidance = $Guidance
    })
}

function Get-RelativePath {
    param([string] $Path)

    $relative = [System.IO.Path]::GetRelativePath($rootPath, $Path)
    return $relative.Replace('\', '/')
}

foreach ($file in $razorFiles) {
    $relative = Get-RelativePath $file.FullName
    $lines = Get-Content -Path $file.FullName

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $lineNumber = $i + 1

        if ($line -match '\bStyle\s*=' -and $allowedStyleFiles -notcontains $relative) {
            Add-Finding `
                -Rule "inline-style" `
                -Severity "warning" `
                -File $relative `
                -Line $lineNumber `
                -Text $line `
                -Guidance "Move repeated visual styling into Workbench-design-system.css or a Dz* component. Small one-off layout exceptions should be justified in review."
        }

        if ($line -match 'MudOverlay') {
            Add-Finding `
                -Rule "manual-overlay" `
                -Severity "error" `
                -File $relative `
                -Line $lineNumber `
                -Text $line `
                -Guidance "Use IDialogService and DzConfirmDialog for confirmation flows. Avoid page-local fixed overlays."
        }

        if ($line -match 'Color\.Primary' -and $line -notmatch 'MudButton') {
            Add-Finding `
                -Rule "primary-non-action" `
                -Severity "error" `
                -File $relative `
                -Line $lineNumber `
                -Text $line `
                -Guidance "Primary/accent is action-only. Use DzMetaChip, DzStatusChip, DzFilterChip, or a neutral/slate class for metadata and selected states."
        }

        if ($line -match '<MudChip\b' -and $allowedRawMudChipFiles -notcontains $relative) {
            Add-Finding `
                -Rule "raw-mudchip" `
                -Severity "warning" `
                -File $relative `
                -Line $lineNumber `
                -Text $line `
                -Guidance "Prefer DzMetaChip, DzStatusChip, DzFilterChip, DzComparisonStatusChip, or DzReviewDecisionChip unless the chip is inside a design-system component."
        }

        if ($line -match '<MudPaper\b' -and $relative -notmatch '/DesignSystem/') {
            Add-Finding `
                -Rule "raw-mudpaper" `
                -Severity "info" `
                -File $relative `
                -Line $lineNumber `
                -Text $line `
                -Guidance "Prefer DzPanel, DzEmptyState, dz-table-shell, or another scoped design-system surface for product UI panels."
        }
    }
}

if ($findings.Count -eq 0) {
    Write-Host "Design-system audit passed. No findings."
    exit 0
}

$findings |
    Sort-Object File, Line |
    Format-Table Severity, Rule, File, Line, Text -AutoSize -Wrap

Write-Host ""
Write-Host "Summary:"
$findings |
    Group-Object Severity, Rule |
    Sort-Object Name |
    ForEach-Object {
        Write-Host ("  {0}: {1}" -f $_.Name, $_.Count)
    }

Write-Host ""
Write-Host "Guidance:"
$findings |
    Sort-Object Rule -Unique |
    ForEach-Object {
        Write-Host ("  [{0}] {1}" -f $_.Rule, $_.Guidance)
    }

$hasErrors = $findings | Where-Object { $_.Severity -eq "error" }

if ($Strict -and $findings.Count -gt 0) {
    exit 1
}

if ($hasErrors) {
    exit 1
}

exit 0
