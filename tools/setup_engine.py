"""Download checksum-pinned official Godot binaries into this project's .tools only."""
import argparse, hashlib, json, os, pathlib, platform, urllib.request, zipfile
ROOT = pathlib.Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--templates', action='store_true')
args = parser.parse_args()
manifest = json.loads((ROOT / 'tools/godot-release.json').read_text())
suffix = {'Darwin': 'macos.universal.zip', 'Windows': 'win64.exe.zip', 'Linux': 'linux.x86_64.zip'}[platform.system()]
cache = ROOT / '.tools'
cache.mkdir(exist_ok=True)
for asset in manifest['assets']:
    if not asset['name'].endswith(suffix) and not (args.templates and asset['name'].endswith('_export_templates.tpz')):
        continue
    archive = cache / asset['name']
    if not archive.exists():
        print('Downloading', asset['name'], flush=True)
        urllib.request.urlretrieve(asset['browser_download_url'], archive)
    actual = 'sha256:' + hashlib.sha256(archive.read_bytes()).hexdigest()
    if actual != asset['digest']:
        raise SystemExit('Checksum mismatch; refusing archive: ' + asset['name'])
    if asset['name'].endswith('.tpz'):
        with zipfile.ZipFile(archive) as z:
            for name in ['templates/windows_release_x86_64.exe', 'templates/windows_debug_x86_64.exe']:
                z.extract(name, cache)
    elif platform.system() == 'Darwin':
        import subprocess
        subprocess.run(['ditto', '-x', '-k', str(archive), str(cache)], check=True)
    else:
        with zipfile.ZipFile(archive) as z:
            z.extractall(cache)
    print('Verified', asset['name'], flush=True)
if platform.system() == 'Darwin':
    engine = cache / 'Godot.app/Contents/MacOS/Godot'
elif platform.system() == 'Windows':
    engine = next(cache.glob('Godot*_win64.exe'))
else:
    engine = next(cache.glob('Godot*_linux.x86_64'))
if platform.system() != 'Windows':
    engine.chmod(0o755)
(cache / 'engine-path.txt').write_text(str(engine))
print(engine)
