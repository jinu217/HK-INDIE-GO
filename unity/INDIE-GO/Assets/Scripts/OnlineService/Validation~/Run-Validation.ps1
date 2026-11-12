param(
    [string]$UnityEditorDirectory = 'C:/Program Files/Unity/Hub/Editor/6000.3.18f1/Editor'
)
$ErrorActionPreference = 'Stop'
$validationProject = Join-Path $PSScriptRoot 'OnlineService.Validation.proj'
& dotnet build $validationProject --nologo -v minimal "-p:UnityEditorDirectory=$UnityEditorDirectory"
if ($LASTEXITCODE -ne 0) { throw 'Online service compilation failed.' }
& dotnet (Join-Path $PSScriptRoot 'bin/Debug/net10.0/OnlineService.Validation.dll')
if ($LASTEXITCODE -ne 0) { throw 'Online service validation failed.' }
