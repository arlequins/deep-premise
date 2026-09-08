param([switch]$SkipBuild, [ValidateSet('en','ko','')][string]$Language = '')
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
$gameArguments = @('--path', ('"' + $projectRoot + '"'), '--log-file', ('"' + $logPath + '"'), '--', '--debug-session', '--start-paused')
if ($Language -ne '') { $gameArguments += '--language=' + $Language }
$gameProcess = Start-Process -FilePath $engine -ArgumentList $gameArguments -WorkingDirectory $projectRoot -WindowStyle Normal -PassThru
Write-Host "Debug game started (PID $($gameProcess.Id))."
Write-Host "Feedback context: $contextFile"

Write-Host "The opening scene is paused. Press Space to start; F8 marks playtest feedback."
