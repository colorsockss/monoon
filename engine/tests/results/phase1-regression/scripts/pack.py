"""Pack the phase1 Linux x64 guest run (cwd: worktree root) after build-players.sh, package-editor.py linux-x64 and keyroom, the CLI
build, probes.sh and the Mac --package builds in pkg/. Adapted from .test-env/work/ch11/pack.py and x64-determinism/scripts/pack.py.
Output: <this folder>/p1reg.zip. Nothing is downloaded: BRUTAL .nupkg files come from this Mac's NuGet cache."""
from pathlib import Path
import io, json, shutil, subprocess as sp, sys, zipfile
repo = Path.cwd(); b = Path(__file__).resolve().parent; t = repo / 'engine/tests'
home_nuget = Path.home() / '.nuget/packages'
projects = b / 'projects'
if projects.exists(): shutil.rmtree(projects)
prep = [('PlatformerLab.Joints', 'PlatformerLab', 'joints'), ('PlatformerLab.TileCollision', 'PlatformerLab', 'tiles'),
        ('PlatformerLab.Paths', 'PlatformerLab', 'paths'), ('KeyRoom.ScriptWaits', 'KeyRoom', 'waits'),
        ('KeyRoom.CopyPaste', 'KeyRoom', 'copy'), ('InventoryLab.RichText', 'InventoryLab', 'rich'),
        ('SpriteLab.Aseprite', 'SpriteLab', 'ase'), ('BarrageLab.Lines', 'BarrageLab', 'lines')]
for harness, sample, dest in prep:
    (projects / dest).mkdir(parents=True)
    sp.run([sys.executable, t / harness / 'verify.py', 'prepare', repo / 'samples' / sample, projects / dest / sample], check=True)
ignore = shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj')
for name in ['KeyRoom', 'CameraLab', 'CollisionLab', 'InventoryLab', 'AudioLab']:
    shutil.copytree(repo / 'samples' / name, projects / 'raw' / name, ignore=ignore)
for name in ['KeyRoom', 'PlatformerLab']:  # Samples.GameText copies the samples as they are (rsync --exclude .monoon)
    shutil.copytree(repo / 'samples' / name, projects / 'text' / name, ignore=shutil.ignore_patterns('.monoon'))
harnesses = ['PlatformerLab.Joints', 'PlatformerLab.TileCollision', 'PlatformerLab.Paths', 'BarrageLab.Lines', 'KeyRoom.ScriptWaits',
             'KeyRoom.ScriptReload', 'KeyRoom.EditorExtensions', 'KeyRoom.CopyPaste', 'InventoryLab.RichText', 'SpriteLab.Aseprite',
             'CameraLab.ScreenEffects', 'CollisionLab.Spatial', 'AudioLab.Vorbis', 'Samples.GameText']
assets = [json.loads(p.read_text()) for p in [repo / 'engine/.artifacts/obj/Engine.Player/project.assets.json'] if p.exists()]
ids = sorted({k for a in assets for k in a['libraries'] if k.lower().startswith('ahwoo.brutal.')})
assert len(ids) >= 15, ids
feed = [home_nuget / f'{i.split("/")[0].lower()}/{i.split("/")[1]}/{i.split("/")[0].lower()}.{i.split("/")[1]}.nupkg' for i in ids]
SKIP = {'.DS_Store', '.artifacts', 'bin', 'obj', '.monoon', '.vs', '.git', '__pycache__', '.monoon-compiled'}
SRC = ['engine/Directory.Build.props', 'engine/Directory.Build.targets', 'engine/native', 'engine/src/Engine.Core', 'engine/src/Engine.Desktop',
       'engine/src/Engine.Player', 'engine/src/Engine.Authoring', 'engine/src/Engine.Cli', 'engine/tests/desktop_players.py',
       'engine/tests/InventoryLab.Replay', 'engine/tests/fixtures/InventoryLab', 'samples/Directory.Build.props', 'samples/InventoryLab']
def snapshot():
    data = io.BytesIO()
    with zipfile.ZipFile(data, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
        for s in SRC:
            p = repo / s
            for f in ([p] if p.is_file() else sorted(p.rglob('*'))):
                rel = f.relative_to(repo)
                if f.is_file() and not any(x in SKIP for x in rel.parts): z.write(f, rel.as_posix())
    return data.getvalue()
with zipfile.ZipFile(b / 'p1reg.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
    def tree(src, dest, cli=False, keep=lambda f: True):
        for f in sorted(src.rglob('*')):
            rel = f.relative_to(src)
            if not f.is_file() or any(x in SKIP - {'.monoon-compiled'} for x in rel.parts): continue
            if cli and (f.suffix == '.pdb' or f.name == 'MoNoon.Cli' or (rel.parts[0] == 'runtimes' and rel.parts[1] != 'linux-x64')): continue
            if keep(f): z.write(f, dest + '/' + rel.as_posix())
    tree(repo / 'player-templates/linux-x64', 'templates/linux-x64')
    tree(repo / 'player-templates/native-aot', 'templates/native-aot')
    tree(repo / 'engine/.artifacts/bin/Engine.Cli/release', 'cli', True)
    tree(projects, 'projects')
    for h in harnesses: tree(t / h, f'qa/{h}', keep=lambda f: f.suffix not in {'.cs', '.csproj'})
    for n in ['verify.py', 'verify_editor.py']: z.write(t / 'KeyRoom.Desktop' / n, f'qa/KeyRoom.Desktop/{n}')
    z.write(t / 'desktop_players.py', 'qa/desktop_players.py')
    for n in ['playback.csv', 'playback-linux-x64.csv']: z.write(t / 'results/audio-mixer' / n, 'qa/results/audio-mixer/' + n)
    tree(b / 'probes', 'probes')
    for f in feed: z.write(f, 'feed/' + f.name, compress_type=zipfile.ZIP_STORED)
    z.write(t / 'results/editor-packaging/collected.save', 'fixtures/collected.save')
    for n in ['Herbalist.inputreplay', 'Herbalist.save']: z.write(t / 'fixtures/InventoryLab' / n, 'fixtures/' + n)
    z.write(repo / 'samples/dist/KeyRoom-sample.zip', 'KeyRoom-sample.zip', compress_type=zipfile.ZIP_STORED)
    for g in ['KeyRoom', 'InventoryLab']:
        z.write(b / f'pkg/out-{g}/{g}-linux-x64.tar.gz', f'packages/{g}-linux-x64.tar.gz', compress_type=zipfile.ZIP_STORED)
    z.write(b / 'guest.py', 'guest.py')
    z.write(repo / 'editor/MoNoon-linux-x64.tar.gz', 'editor.tar.gz', compress_type=zipfile.ZIP_STORED)
    z.writestr('src.zip', snapshot(), compress_type=zipfile.ZIP_STORED)
print('packed', (b / 'p1reg.zip').stat().st_size, 'feed', [f.name for f in feed])
