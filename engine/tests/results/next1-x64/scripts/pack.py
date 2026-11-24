"""Pack the wip/next1 Linux x64 guest run (cwd: worktree root) after mac-build.sh. Output: $S/n1x64.zip.
repo/ is a git snapshot of HEAD (engine without result images, samples), the linux-x64 template and Native AOT kit, the Mac-built
CLI (other RIDs' native assets dropped) and monoon-guest as repo/monoon. Nothing is downloaded: BRUTAL .nupkg files come from
this Mac's NuGet cache. Adapted from phase1-regression/scripts/pack.py."""
from pathlib import Path
import io, json, subprocess as sp, sys, tarfile, zipfile
repo = Path.cwd(); here = Path(__file__).resolve().parent
S = Path(sys.argv[1])
CLI_KEEP = lambda rel: rel.name != 'MoNoon.Cli' and not (rel.parts[0] == 'runtimes' and rel.parts[1] != 'linux-x64')
def tree(z, src, dest, keep=lambda rel: True):
    for f in sorted(src.rglob('*')):
        rel = f.relative_to(src)
        if f.is_file() and keep(rel): z.write(f, dest + '/' + rel.as_posix())
if sys.argv[2:] == ['extra']:  # second transfer for aot-size-variants: the Size->Speed CLI and the base commit's CLI and templates
    with zipfile.ZipFile(S / 'n1x64-extra.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
        tree(z, S / 'cli-speed', 'cli-speed', CLI_KEEP)
        tree(z, S / 'base/cli', 'base/cli', CLI_KEEP)
        for t in ['linux-x64', 'native-aot']: tree(z, S / 'base/player-templates' / t, 'base/player-templates/' + t)
    sys.exit(print('packed', (S / 'n1x64-extra.zip').stat().st_size))
nuget = Path.home() / '.nuget/packages'
if sys.argv[2:] == ['buildhost']:  # build-host phase: win-x64 and osx-arm64 runtime packs (this Mac's cache) and templates
    packs = ['microsoft.netcore.app.runtime.win-x64/10.0.1', 'microsoft.netcore.app.host.win-x64/10.0.1', 'microsoft.netcore.app.runtime.osx-arm64/10.0.1',
             'microsoft.aspnetcore.app.runtime.win-x64/10.0.1', 'microsoft.aspnetcore.app.runtime.osx-arm64/10.0.1']
    with zipfile.ZipFile(S / 'n1x64-buildhost.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
        for i in packs:
            n, v = i.split('/'); z.write(nuget / n / v / f'{n}.{v}.nupkg', f'feed/{n}.{v}.nupkg', compress_type=zipfile.ZIP_STORED)
        for t in ['win-x64', 'osx-arm64']: tree(z, (repo / 'player-templates' / t).resolve(), 'repo/player-templates/' + t)
        z.write(here / 'guest.py', 'guest.py')
    sys.exit(print('packed', (S / 'n1x64-buildhost.zip').stat().st_size))
IMAGES = ('.png', '.jpg', '.gif', '.mp4', '.mov', '.f32', '.wav', '.ogg', '.zip', '.gz')
ids = sorted({k for p in ['Engine.Player', 'Engine.Cli'] for k, v in json.loads((repo / f'engine/.artifacts/obj/{p}/project.assets.json').read_text())['libraries'].items()
              if v['type'] == 'package' and k.lower().startswith('ahwoo.brutal.')})
feed = [nuget / i.split('/')[0].lower() / i.split('/')[1] / f'{i.split("/")[0].lower()}.{i.split("/")[1]}.nupkg' for i in ids]
snapshot = sp.run(['git', 'archive', '--format=tar', 'HEAD', 'engine', 'samples', 'packages', 'docs', 'monoon'], cwd=repo, check=True, capture_output=True).stdout
with zipfile.ZipFile(S / 'n1x64.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
    with tarfile.open(fileobj=io.BytesIO(snapshot)) as t:
        for m in t.getmembers():
            if not m.isfile(): continue
            if m.name.startswith('engine/tests/results/') and m.name.endswith(IMAGES): continue
            z.writestr('repo/' + (m.name + '.orig' if m.name == 'monoon' else m.name), t.extractfile(m).read())
    z.write(here / 'monoon-guest', 'repo/monoon')
    for t in ['linux-x64', 'native-aot']: tree(z, (repo / 'player-templates' / t).resolve(), 'repo/player-templates/' + t)
    tree(z, S / 'cli', 'repo/engine/.artifacts/bin/Engine.Cli/release', CLI_KEEP)
    for f in feed: z.write(f, 'feed/' + f.name, compress_type=zipfile.ZIP_STORED)
    for f in sorted((S / 'mac').glob('*-linux-x64.tar.gz')): z.write(f, 'mac/' + f.name, compress_type=zipfile.ZIP_STORED)
    z.write(here / 'sitecustomize.py', 'site/sitecustomize.py')
    z.write(here / 'guest.py', 'guest.py')
print('packed', (S / 'n1x64.zip').stat().st_size, 'feed', [f.name for f in feed])
