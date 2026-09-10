param([switch]$SkipBuild, [switch]$FreshRun, [ValidateSet('en','ko','')][string]$Language = '', [ValidateSet('type0','type2','type3')][string]$Type = 'type3')
if ($Type -eq 'type3') {
    $gardenLanguage = if ($Language -eq '') { 'ko' } else { $Language }
    & (Join-Path $PSScriptRoot 'run-type3.ps1') -SkipBuild:$SkipBuild -FreshRun:$FreshRun -Language $gardenLanguage
    exit $LASTEXITCODE
}
if ($Type -eq 'type2') {
    $cityLanguage = if ($Language -eq '') { 'ko' } else { $Language }
    & (Join-Path $PSScriptRoot 'run-type2.ps1') -SkipBuild:$SkipBuild -FreshRun:$FreshRun -Language $cityLanguage
    exit $LASTEXITCODE
}
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$runtime = Join-Path $projectRoot '.tools/dotnet'
$env:DOTNET_ROOT = $runtime
$env:PATH = $runtime + ';' + $env:PATH
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$contextFile = Join-Path $projectRoot 'artifacts/live/context.json'
if (Test-Path $contextFile) {
    try {
        $context = Get-Content $contextFile -Raw | ConvertFrom-Json
        $existing = Get-Process -Id $context.ProcessId -ErrorAction SilentlyContinue
        if ($existing -and $existing.Path.StartsWith((Join-Path $projectRoot '.tools/mono'))) {
            Write-Host "A debug game is already running (PID $($existing.Id))."
            exit 0
        }
    } catch { Write-Verbose 'No active debug context.' }
}
if (-not $SkipBuild) {
    & (Join-Path $runtime 'dotnet.exe') build DeepPremise.Godot.csproj --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed. The game was not launched.' }
}
$consoleEngine = (Get-Content .tools/engine-path.txt -Raw).Trim()
$engine = $consoleEngine.Replace('_console.exe', '.exe')
$logDirectory = Join-Path $projectRoot 'artifacts/live'
New-Item -ItemType Directory -Force $logDirectory | Out-Null
$logPath = Join-Path $logDirectory 'game.log'
$gameArguments = @('--path', ('"' + $projectRoot + '"'), 'res://game/type0.tscn', '--log-file', ('"' + $logPath + '"'), '--', '--debug-session', '--start-paused')
if ($Language -ne '') { $gameArguments += '--language=' + $Language }
if ($FreshRun) { $gameArguments += '--fresh-run' }
$gameProcess = Start-Process -FilePath $engine -ArgumentList $gameArguments -WorkingDirectory $projectRoot -WindowStyle Normal -PassThru
Write-Host "Debug game started (PID $($gameProcess.Id))."
Write-Host "Feedback context: $contextFile"

Write-Host "People are already working. Drag a person to another place to change their work. Space pauses; F8 records feedback."
