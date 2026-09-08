param([string]$Session = '', [string]$Output = '', [long]$Sequence = [long]::MaxValue)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$dotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if ($Session -eq '') {
    $contextFile = Join-Path $projectRoot 'artifacts/live/context.json'
    if (Test-Path $contextFile) {
        $context = Get-Content $contextFile -Raw | ConvertFrom-Json
        if ($context.PlaytestDirectory) { $Session = $context.PlaytestDirectory }
    }
    if ($Session -eq '') {
        $latest = Get-ChildItem (Join-Path $projectRoot 'artifacts/playtests') -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
        if ($latest) { $Session = $latest.FullName }
    }
}
if ($Session -eq '') { throw 'No playtest session found. Play the game first or pass -Session.' }
$tool = Join-Path $projectRoot 'tools/DeepPremise.Playtest/DeepPremise.Playtest.csproj'
if ($Output -ne '') {
    & $dotnet run --project $tool -- restore $Session $Output $Sequence
} else {
    & $dotnet run --project $tool -- review $Session
}
if ($LASTEXITCODE -ne 0) { throw 'Playtest review or restoration failed.' }
