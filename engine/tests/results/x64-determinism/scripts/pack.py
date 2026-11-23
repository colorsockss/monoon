"""Pack the x64 determinism guest run (cwd: worktree root, after build-players.sh, the CLI build and the probe
builds; no Linux editor package since 2026-11-23). Output: <scratch>/x64det/guest/x64det.zip. Nothing is downloaded."""
from pathlib import Path
import io, shutil, subprocess as sp, sys, zipfile
repo = Path.cwd(); b = Path(__file__).resolve().parent; t = repo / 'engine/tests'
probes = Path(sys.argv[1])  # folder with JointsProbe.dll, PathsProbe.dll and TileContactProbe.dll; argv[2]: codegen probe source
projects = b / 'projects'
if projects.exists(): shutil.rmtree(projects)
for harness, sample, dest in [('PlatformerLab.Joints', 'PlatformerLab', 'joints'), ('PlatformerLab.Paths', 'PlatformerLab', 'paths'),
                              ('BarrageLab.Lines', 'BarrageLab', 'lines'), ('PlatformerLab.TileCollision', 'PlatformerLab', 'tiles')]:
    (projects / dest).mkdir(parents=True)
    sp.run([sys.executable, t / harness / 'verify.py', 'prepare', repo / 'samples' / sample, projects / dest / sample], check=True)
home_nuget = Path.home() / '.nuget/packages'
feed = [home_nuget / f'{i}/{v}/{i}.{v}.nupkg' for i, v in [('ahwoo.brutal.box2d', '2026.10.2'), ('ahwoo.brutal.box2d.native', '2026.10.2'),
                                                           ('ahwoo.brutal.core', '2026.10.4'), ('ahwoo.brutal.numerics', '2026.10.2')]]
SKIP = {'.DS_Store', '.artifacts', 'bin', 'obj', '.monoon', '.vs', '.git', '__pycache__', '.monoon-compiled'}
# Source snapshot for engine/tests/InventoryLab.Replay/build_guest.py (it compiles its own template, kit and CLI).
SRC = ['engine/Directory.Build.props', 'engine/Directory.Build.targets', 'engine/native', 'engine/src/Engine.Core', 'engine/src/Engine.Desktop',
       'engine/src/Engine.Player', 'engine/src/Engine.Authoring', 'engine/src/Engine.Cli', 'engine/tests/desktop_players.py',
       'engine/tests/InventoryLab.Replay', 'engine/tests/fixtures/InventoryLab', 'samples/Directory.Build.props', 'samples/InventoryLab']
def snapshot_worktree():
    data = io.BytesIO()
    with zipfile.ZipFile(data, 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
        for s in SRC:
            p = repo / s
            for f in ([p] if p.is_file() else sorted(p.rglob('*'))):
                rel = f.relative_to(repo)
                if f.is_file() and not any(x in SKIP for x in rel.parts): z.write(f, rel.as_posix())
    return data.getvalue()
with zipfile.ZipFile(b / 'x64det.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=1) as z:
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
    for h in ['PlatformerLab.Joints', 'PlatformerLab.Paths', 'BarrageLab.Lines', 'PlatformerLab.TileCollision']:
        tree(t / h, f'qa/{h}', keep=lambda f: f.suffix not in {'.cs', '.csproj'})
    for n in ['JointsProbe.dll', 'PathsProbe.dll', 'TileContactProbe.dll']: z.write(probes / n, 'probes/' + n)
    for f in feed: z.write(f, 'feed/' + f.name, compress_type=zipfile.ZIP_STORED)
    z.write(b / 'guest.py', 'guest.py')
    z.writestr('src-after.zip', snapshot_worktree(), compress_type=zipfile.ZIP_STORED)
    for n in ['Program.cs', 'Probe.csproj']: z.write(Path(sys.argv[2]) / n, 'codegen/' + n)  # Affine2D codegen probe
print('packed', (b / 'x64det.zip').stat().st_size)
