param([switch]$SkipBuild, [switch]$FreshRun, [ValidateSet('en','ko')][string]$Language = 'ko')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$env:DOTNET_ROOT = Join-Path $projectRoot '.tools/dotnet'
$env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
$contextPath = Join-Path $projectRoot 'artifacts/live/type2-context.json'
if (Test-Path $contextPath) {
    $context = Get-Content $contextPath -Raw | ConvertFrom-Json
    $running = Get-Process -Id $context.ProcessId -ErrorAction SilentlyContinue
    if ($running -and $running.Path.StartsWith((Join-Path $projectRoot '.tools/mono'))) {
        Write-Host "Type2 is already running (PID $($running.Id))."
        exit 0
    }
}
if (-not $SkipBuild) {
    & (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build DeepPremise.Godot.csproj --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}
$engine = (Get-Content .tools/engine-path.txt -Raw).Trim().Replace('_console.exe','.exe')
$arguments = @('--path', ('"' + $projectRoot + '"'), 'res://game/type2.tscn', '--log-file', ('"' + (Join-Path $projectRoot 'artifacts/live/type2-game.log') + '"'), '--', ('--language=' + $Language))
if ($FreshRun) { $arguments += '--fresh-run' }
$game = Start-Process -FilePath $engine -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Normal -PassThru
Write-Host "Type2 started (PID $($game.Id)). Space pauses; F8 records feedback."
