[CmdletBinding()]
param(
    [ValidatePattern('^[a-p]{32}$')][string]$ChromeExtensionId,
    [ValidatePattern('^[a-p]{32}$')][string]$EdgeExtensionId,
    [string]$WixPath = 'wix',
    [Parameter(Mandatory=$true)][string]$WixUiExtension,
    [string]$ProductVersion = '0.2.5',
    [string]$RuntimeVersion = '10.0.8',
    [string]$OutputDirectory,
    [string]$OfflineFeed
)
$ErrorActionPreference = 'Stop'
$taskArguments = @((Join-Path $PSScriptRoot 'build_staff.py'), '--wix', $WixPath, '--wix-ui', $WixUiExtension, '--version', $ProductVersion, '--runtime-version', $RuntimeVersion)
if ($OutputDirectory) { $taskArguments += @('--output', $OutputDirectory) }
if ($OfflineFeed) { $taskArguments += @('--offline-feed', $OfflineFeed) }
foreach ($taskId in @($ChromeExtensionId, $EdgeExtensionId)) { if ($taskId) { $taskArguments += @('--extension-id', $taskId) } }
& python @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Staff MSI build failed. See build output.' }
