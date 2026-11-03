[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-p]{32}$')][string]$ChromeExtensionId,
    [ValidatePattern('^[a-p]{32}$')][string]$EdgeExtensionId,
    [ValidateSet('win-x64','win-arm64')][string]$Runtime = 'win-x64',
    [switch]$FrameworkDependent,
    [switch]$SkipMsi
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskPayload = Join-Path $taskRoot 'artifacts\installer-payload'
if (Test-Path -LiteralPath $taskPayload) { throw 'Use a clean checkout or move the existing artifacts/installer-payload aside before packaging.' }
New-Item -ItemType Directory -Path $taskPayload -Force | Out-Null
$taskSelfContained = if ($FrameworkDependent) { 'false' } else { 'true' }
foreach ($taskProject in @(@('Desktop','PrimEx.SmartSave.Desktop'),@('NativeHost','PrimEx.SmartSave.NativeHost'))) {
    & dotnet publish (Join-Path $taskRoot ('src\' + $taskProject[1] + '\' + $taskProject[1] + '.csproj')) -c Release -r $Runtime --self-contained $taskSelfContained -o (Join-Path $taskPayload $taskProject[0])
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
}
$taskIds = @($ChromeExtensionId)
if ($EdgeExtensionId) { $taskIds += $EdgeExtensionId }
$taskManifest = @{ name='com.primex.smartsave'; description='PrimEx Smart Save completed-download bridge'; path='NativeHost\PrimEx.SmartSave.NativeHost.exe'; type='stdio'; allowed_origins=@($taskIds | Select-Object -Unique | ForEach-Object { 'chrome-extension://' + $_ + '/' }) }
$taskManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $taskPayload 'native-host.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $taskRoot 'config\appsettings.example.json') -Destination (Join-Path $taskPayload 'appsettings.example.json')
if (-not $SkipMsi) {
    if (-not (Get-Command wix -ErrorAction SilentlyContinue)) { throw 'Install the WiX 6 build tool in your build environment, then rerun from a clean payload directory.' }
    $taskArch = if ($Runtime -eq 'win-arm64') { 'arm64' } else { 'x64' }
    & wix build (Join-Path $PSScriptRoot 'PrimEx.SmartSave.wxs') -arch $taskArch -d ('PayloadRoot=' + $taskPayload) -o (Join-Path $taskRoot 'artifacts\PrimEx.SmartSave-0.1.0.msi')
    if ($LASTEXITCODE -ne 0) { throw 'MSI build failed.' }
}
Write-Output ('Payload prepared at ' + $taskPayload)
