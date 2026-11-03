[CmdletBinding(SupportsShouldProcess=$true)]
param(
    [Parameter(Mandatory=$true)][string]$HostExecutable,
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-p]{32}$')][string[]]$ExtensionIds,
    [switch]$Unregister
)
$ErrorActionPreference = 'Stop'
$taskName = 'com.primex.smartsave'
$taskKeys = @(('HKCU:\Software\Google\Chrome\NativeMessagingHosts\' + $taskName), ('HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\' + $taskName))
if ($Unregister) {
    foreach ($taskKey in $taskKeys) { if ((Test-Path -LiteralPath $taskKey) -and $PSCmdlet.ShouldProcess($taskKey, 'Remove native host registration')) { Remove-Item -LiteralPath $taskKey } }
    return
}
$taskExe = (Resolve-Path -LiteralPath $HostExecutable).Path
if ([IO.Path]::GetFileName($taskExe) -ne 'PrimEx.SmartSave.NativeHost.exe') { throw 'Select the published PrimEx native host executable.' }
$taskManifestPath = Join-Path ([IO.Path]::GetDirectoryName($taskExe)) 'com.primex.smartsave.json'
$taskManifest = @{ name=$taskName; description='PrimEx Smart Save completed-download bridge'; path=$taskExe; type='stdio'; allowed_origins=@($ExtensionIds | Select-Object -Unique | ForEach-Object { 'chrome-extension://' + $_ + '/' }) }
if ($PSCmdlet.ShouldProcess($taskManifestPath, 'Write approved-extension manifest and register for current user')) {
    $taskManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $taskManifestPath -Encoding utf8
    foreach ($taskKey in $taskKeys) { New-Item -Path $taskKey -Force | Out-Null; Set-Item -LiteralPath $taskKey -Value $taskManifestPath }
}
