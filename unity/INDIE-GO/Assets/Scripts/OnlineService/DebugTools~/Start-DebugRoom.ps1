param(
    [string]$RoomName = '디버그 참가 테스트',
    [string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor/Unity.exe'
)
$ErrorActionPreference = 'Stop'
$serviceRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $serviceRoot '../../..'))
$branch = & git -C $projectRoot branch --show-current
if ($branch -ne 'dev/PhotonFusion') { throw 'Run the debug host on dev/PhotonFusion.' }
if (-not (Test-Path -LiteralPath $UnityEditorPath)) { throw 'Unity Editor was not found.' }
if ([string]::IsNullOrWhiteSpace($RoomName) -or $RoomName.Length -gt 40) { throw 'RoomName must contain 1 to 40 characters.' }

$runtimeRoot = Join-Path $PSScriptRoot 'Runtime'
$hostProject = Join-Path $runtimeRoot 'HostProject'
$statusPath = Join-Path $runtimeRoot 'room-status.json'
$launchProjectFile = Join-Path $runtimeRoot 'launch-project.txt'
$previousLaunchProject = if (Test-Path -LiteralPath $launchProjectFile) { Get-Content -LiteralPath $launchProjectFile -Raw } else { $hostProject }
if (Test-Path -LiteralPath $statusPath) {
    try {
        $previous = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
        $existing = Get-CimInstance Win32_Process -Filter "ProcessId = $($previous.processId)" -ErrorAction SilentlyContinue
        if ($null -ne $existing -and ($existing.CommandLine.Contains($hostProject) -or $existing.CommandLine.Contains($previousLaunchProject))) {
            $previous | ConvertTo-Json
            return
        }
    } catch { }
}

$assets = Join-Path $hostProject 'Assets'
$common = Join-Path $assets 'Common'
$runtime = Join-Path $assets 'OnlineRuntime'
$editor = Join-Path $assets 'Editor'
$settings = Join-Path $hostProject 'ProjectSettings'
$packages = Join-Path $hostProject 'Packages'
foreach ($directory in @($common, $runtime, $editor, $settings, $packages)) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}
# Reuse the installed SDK and package versions in an isolated, ignored project.
# The main project's scenes, assets, packages and Editor session are untouched.
$photonSource = Join-Path $serviceRoot 'ThirdParty/Photon'
$photonTarget = Join-Path $assets 'Photon'
if (-not (Test-Path -LiteralPath $photonTarget)) {
    Copy-Item -LiteralPath $photonSource -Destination $photonTarget -Recurse
}
Get-ChildItem -LiteralPath (Join-Path $serviceRoot 'Runtime') -File | Copy-Item -Destination $runtime
foreach ($file in @('GameStartSettings.cs', 'GameRuleDefine.cs', 'PlayerDefine.cs', 'RoomSettingsData.cs')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "Assets/Scripts/Common/$file") -Destination $common
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DebugRoomHost.cs') -Destination $editor
foreach ($file in @('ProjectVersion.txt', 'ProjectSettings.asset', 'EditorSettings.asset')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "ProjectSettings/$file") -Destination $settings
}
foreach ($file in @('manifest.json', 'packages-lock.json')) {
    Copy-Item -LiteralPath (Join-Path $projectRoot "Packages/$file") -Destination $packages
}
# Refresh the cloud settings even when reusing the isolated SDK installation.
Copy-Item -LiteralPath (Join-Path $photonSource 'Fusion/Resources/PhotonAppSettings.asset') -Destination (Join-Path $photonTarget 'Fusion/Resources/PhotonAppSettings.asset')
[System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'room-name.txt'), $RoomName)
$stopRequest = Join-Path $runtimeRoot 'stop.request'
if (Test-Path -LiteralPath $stopRequest) { Remove-Item -LiteralPath $stopRequest }
$logPath = Join-Path $runtimeRoot 'host.log'

# Keep all real files under OnlineService while avoiding Windows' long-path limits.
$driveFile = Join-Path $runtimeRoot 'drive-letter.txt'
$drive = if (Test-Path -LiteralPath $driveFile) { (Get-Content -LiteralPath $driveFile -Raw).Trim() } else { '' }
$ownedDrive = $false
if ($drive -match '^[P-Z]:$') {
    $ownerFile = [System.IO.Path]::Combine(($drive + '\'), 'owner-path.txt')
    $ownedDrive = (Test-Path -LiteralPath $ownerFile) -and ((Get-Content -LiteralPath $ownerFile -Raw) -eq $runtimeRoot)
}
if (-not $ownedDrive) {
    $used = [System.IO.DriveInfo]::GetDrives().Name
    $drive = @('R:', 'S:', 'T:', 'U:', 'V:', 'W:', 'X:', 'Y:', 'Z:', 'Q:', 'P:') |
        Where-Object { ($_ + '\') -notin $used } | Select-Object -First 1
    if (-not $drive) { throw 'No unused drive letter is available for the isolated host.' }
    [System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'owner-path.txt'), $runtimeRoot)
    & subst.exe $drive $runtimeRoot
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the temporary debug-host drive mapping.' }
    [System.IO.File]::WriteAllText($driveFile, $drive)
}
$launchProject = Join-Path ($drive + '\') 'HostProject'
[System.IO.File]::WriteAllText($launchProjectFile, $launchProject)

# unity run always supplies -quit. A persistent host needs an Editor without -quit.
$arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $launchProject + '"'),
    '-executeMethod', 'YutArena.Online.DebugTools.DebugRoomHost.Launch',
    '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
[ordered]@{
    phase = 'Importing'; roomName = $RoomName; processId = $process.Id
    hostProject = $hostProject; launchProject = $launchProject; logPath = $logPath; sessionId = ''
} | ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding utf8
Write-Output "Debug host starting. PID: $($process.Id)"
Write-Output "Status: $statusPath"
