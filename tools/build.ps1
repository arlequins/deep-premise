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
$versionMatch = [regex]::Match((Get-Content project.godot -Raw), 'config/version="([0-9]+\.[0-9]+\.[0-9]+)"')
if (-not $versionMatch.Success) { throw 'Missing project release version.' }
$version = $versionMatch.Groups[1].Value
$packageDirectory = "dist/Unseen-Order-$version-Windows-x64"
if (Test-Path $packageDirectory) { throw "Package directory already exists: $packageDirectory. Preserve it or choose a new version." }
New-Item -ItemType Directory -Force $packageDirectory | Out-Null
$previousErrorPreference = $ErrorActionPreference
$ErrorActionPreference = 'Continue' # Godot writes harmless warnings to stderr on Windows PowerShell.
& $engine --headless --path . --export-release 'Windows Desktop' "$packageDirectory/Unseen-Order.exe" *> .tools/export-csharp.log
$ErrorActionPreference = $previousErrorPreference
if ($LASTEXITCODE -ne 0 -or (Select-String -Path .tools/export-csharp.log -Pattern '^ERROR:' -Quiet)) { throw 'Export failed; inspect .tools/export-csharp.log.' }
Copy-Item game/assets/GODOT-LICENSE.txt,game/assets/OFL.txt $packageDirectory
Copy-Item docs/PLAY.txt "$packageDirectory/PLAY.txt"
Compress-Archive -Path "$packageDirectory/*" -DestinationPath "$packageDirectory.zip"
Get-FileHash "$packageDirectory.zip","$packageDirectory/Unseen-Order.exe","$packageDirectory/Unseen-Order.pck" -Algorithm SHA256 |
    ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + (Split-Path $_.Path -Leaf) } | Set-Content -Encoding ascii "dist/SHA256SUMS-$version.txt"
Write-Host "Portable package: $packageDirectory.zip"
