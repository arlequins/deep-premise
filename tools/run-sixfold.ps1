param([switch]$FreshRun,[ValidateSet('ko','en')][string]$Language='ko')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT=Join-Path $projectRoot '.tools/dotnet'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
& (Join-Path $env:DOTNET_ROOT 'dotnet.exe') build (Join-Path $projectRoot 'DeepPremise.Godot.csproj') --nologo
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
$engine=Join-Path $projectRoot '.tools/mono/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64.exe'
$arguments=@('--path',('"'+$projectRoot+'"'),'res://game/type11.tscn','--',"--language=$Language")
if($FreshRun){$arguments+='--fresh-run'}
Start-Process -FilePath $engine -ArgumentList $arguments -WorkingDirectory $projectRoot
