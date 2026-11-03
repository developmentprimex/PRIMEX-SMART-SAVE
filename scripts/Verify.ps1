[CmdletBinding()]
param([string]$OfflineFeed)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if ($OfflineFeed) { & dotnet restore PrimEx.SmartSave.sln --source $OfflineFeed -p:NuGetAudit=false }
    else { & dotnet restore PrimEx.SmartSave.sln }
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & dotnet build PrimEx.SmartSave.sln -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    foreach ($taskTests in @('tests/PrimEx.SmartSave.Tests','tests/PrimEx.SmartSave.Desktop.Tests')) {
        & dotnet run --project $taskTests -c Release --no-build
        if ($LASTEXITCODE -ne 0) { throw ('Tests failed: ' + $taskTests) }
    }
    & node --test tests/browser/policy.test.mjs tests/browser/worker.test.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Browser tests failed.' }
} finally { Pop-Location }
