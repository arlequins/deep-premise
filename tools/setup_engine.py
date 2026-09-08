"""Prepare checksum-pinned Godot .NET binaries in this repository's .tools directory."""
import argparse
import hashlib
import json
import pathlib
import platform
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--templates', action='store_true')
args = parser.parse_args()
if platform.system() != 'Windows':
    raise SystemExit('This preparation script currently targets Windows x64. The C# core is platform independent.')
manifest = json.loads((ROOT / 'tools/godot-dotnet-release.json').read_text(encoding='utf-8-sig'))
cache = ROOT / '.tools'
cache.mkdir(exist_ok=True)
for asset in manifest['assets']:
    template = asset['name'].endswith('.tpz')
    if template and not args.templates:
        continue
    archive = cache / asset['name']
    if not archive.exists():
        temporary = archive.with_suffix(archive.suffix + '.download')
        print('Downloading', asset['name'], flush=True)
        urllib.request.urlretrieve(asset['browser_download_url'], temporary)
        temporary.replace(archive)
    with archive.open('rb') as stream:
        digest = 'sha256:' + hashlib.file_digest(stream, 'sha256').hexdigest()
    if digest != asset['digest']:
        raise SystemExit('Checksum mismatch; refusing archive: ' + asset['name'])
    destination = cache / ('mono-templates' if template else 'mono')
    with zipfile.ZipFile(archive) as contents:
        for name in contents.namelist():
            if template and not ('windows' in name and 'x86_64' in name):
                continue
            target = (destination / name).resolve()
            if not target.is_relative_to(destination.resolve()):
                raise SystemExit('Unsafe archive path')
            contents.extract(name, destination)
    print('Verified', asset['name'], flush=True)
engine = next((cache / 'mono').glob('*/Godot*_mono_win64_console.exe'))
(cache / 'engine-path.txt').write_text(str(engine), encoding='utf-8')
print(engine)
