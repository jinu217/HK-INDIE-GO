$ErrorActionPreference = 'Stop'
$runtimeRoot = Join-Path $PSScriptRoot 'Runtime'
$statusPath = Join-Path $runtimeRoot 'room-status.json'
if (-not (Test-Path -LiteralPath $statusPath)) { Write-Output 'No debug room has been started.'; return }
$status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
$process = Get-CimInstance Win32_Process -Filter "ProcessId = $($status.processId)" -ErrorAction SilentlyContinue
$hostProject = Join-Path $runtimeRoot 'HostProject'
$launchProjectFile = Join-Path $runtimeRoot 'launch-project.txt'
$launchProject = if (Test-Path -LiteralPath $launchProjectFile) { Get-Content -LiteralPath $launchProjectFile -Raw } else { $hostProject }
if ($null -eq $process -or -not ($process.CommandLine.Contains($hostProject) -or $process.CommandLine.Contains($launchProject))) {
    Write-Output 'The debug host is already stopped.'
    return
}
[System.IO.File]::WriteAllText((Join-Path $runtimeRoot 'stop.request'), 'stop')
Write-Output 'Requested a graceful shutdown of the debug room.'
$deadline = [DateTime]::UtcNow.AddSeconds(20)
while ((Get-Process -Id $status.processId -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $deadline) {
    Start-Sleep -Milliseconds 500
}
if (-not (Get-Process -Id $status.processId -ErrorAction SilentlyContinue)) {
    $driveFile = Join-Path $runtimeRoot 'drive-letter.txt'
    if (Test-Path -LiteralPath $driveFile) {
        $drive = (Get-Content -LiteralPath $driveFile -Raw).Trim()
        if ($drive -match '^[P-Z]:$') {
            $ownerFile = Join-Path ($drive + '\') 'owner-path.txt'
            if ((Test-Path -LiteralPath $ownerFile) -and ((Get-Content -LiteralPath $ownerFile -Raw) -eq $runtimeRoot)) {
                & subst.exe $drive /D
            }
        }
    }
    Write-Output 'Debug room stopped; temporary drive mapping released.'
}
