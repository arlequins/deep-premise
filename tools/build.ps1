param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$localSdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (Test-Path $localSdk) {
    $env:DOTNET_ROOT = Split-Path $localSdk
    $env:PATH = $env:DOTNET_ROOT + ';' + $env:PATH
    $dotnet = $localSdk
} else { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$engine = (Get-Content '.tools/engine-path.txt' -Raw).Trim()
if (-not (Test-Path $engine) -or $engine -notmatch 'mono') { throw 'Run python tools/setup_engine.py --templates first.' }
if (-not $SkipTests) {
    & $dotnet run --project tests/DeepPremise.Tests/DeepPremise.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
}
& $dotnet build DeepPremise.Godot.csproj --nologo
if ($LASTEXITCODE -ne 0) { throw 'Viewer build failed.' }
New-Item -ItemType Directory -Force dist/conversation | Out-Null
& $engine --headless --path . --export-release 'Windows Desktop' dist/conversation/Unseen-Order.exe *> .tools/export-csharp.log
if ($LASTEXITCODE -ne 0 -or (Select-String -Path .tools/export-csharp.log -Pattern '^ERROR:' -Quiet)) { throw 'Export failed; inspect .tools/export-csharp.log.' }
Copy-Item game/assets/GODOT-LICENSE.txt,game/assets/OFL.txt dist/conversation -Force
Copy-Item docs/PLAY.txt dist/conversation/PLAY.txt -Force
Compress-Archive -Path dist/conversation/* -DestinationPath dist/Unseen-Order-0.3.0-Windows-x64.zip -Force
Get-FileHash dist/Unseen-Order-0.3.0-Windows-x64.zip,dist/conversation/Unseen-Order.exe,dist/conversation/Unseen-Order.pck -Algorithm SHA256 |
    ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content -Encoding ascii dist/SHA256SUMS.txt
Write-Host 'Portable package: dist/Unseen-Order-0.3.0-Windows-x64.zip'
