"""User instruction (2026-11-24, mid-run): build on the Mac, only verify on Linux. For the harnesses still to run, this runs
their prepare steps and Release build steps here with --target linux-x64 (same inputs and profile; the CLI copy from
mac-build.sh), and packs each harness's Release products as macrel/<id>.tar.gz (paths relative to the harness work folder,
modes and the libSDL3.so link kept). guest.py extracts it in place of the Release build step; Native AOT is still built on
the guest (macOS cannot cross-build linux-x64 Native AOT).
  mac-release.py <scratch> <id>...      (cwd: worktree root)"""
from pathlib import Path
import json, os, re, shutil, subprocess as sp, sys, tarfile

S, ids = Path(sys.argv[1]), sys.argv[2:]
repo = Path.cwd()
cli = S / 'cli/MoNoon.Cli.dll'
manifest = {h['id']: h for h in json.loads((repo / 'engine/tests/harnesses.json').read_text())['harnesses']}
# The same project the guest uses for InventoryLab.ScriptFields: the copy the Mac editor saved (Inspector edits).
REPLACE = {'python3 $qa/verify.py prepare samples/InventoryLab "$work/InventoryLab"': f'cp -R "{S}/edz/mac-edited/InventoryLab" "$work/InventoryLab"'}
# KeyRoom.WorldCheckpoints and AudioLab.Vorbis build inside their ci.py; the guest runs their checks on these products.
PLAIN = {'KeyRoom.WorldCheckpoints': 'KeyRoom', 'AudioLab.Vorbis': 'AudioLab'}
env = dict(os.environ, MONOON_PLAYERS_DIR=str(repo / 'player-templates'))
(S / 'macrel').mkdir(exist_ok=True)
for hid in ids:
    work = S / 'macrel-work' / hid
    shutil.rmtree(work, ignore_errors=True); work.mkdir(parents=True)
    if hid in PLAIN:
        lines = [f'cp -R samples/{PLAIN[hid]} "$work/{PLAIN[hid]}" && rm -rf "$work/{PLAIN[hid]}/.monoon"',
                 f'./monoon build "$work/{PLAIN[hid]}" release --profile distribution --output "$work/builds"']
    else:
        lines = [s['run'] for s in manifest[hid]['steps'] if s['kind'] == 'prepare' or (s['kind'] == 'build' and s.get('modes') == ['release'])]
    script = ['set -e', f'cd "{repo}"', f'qa=engine/tests/{hid}', f'work="{work}"']
    for run in lines:
        for old, new in REPLACE.items(): run = run.replace(old, new)
        script.append(run.replace('./monoon build', f'dotnet "{cli}" build').replace('--profile distribution', '--profile distribution --target linux-x64'))
    (work / 'mac-release.sh').write_text('\n'.join(script) + '\n')
    with (work / 'mac-release.log').open('wb') as log:
        sp.run(['bash', str(work / 'mac-release.sh')], env=env, stdout=log, stderr=sp.STDOUT, check=True)
    products = sorted(d for d in work.rglob('*-linux-x64') if d.is_dir() and 'NativeAOT' not in d.parts and (d / 'MoNoonPlayer').exists())
    with tarfile.open(S / 'macrel' / f'{hid}.tar.gz', 'w:gz', compresslevel=1) as t:
        for d in products: t.add(d, d.relative_to(work).as_posix())
    print(hid, [d.relative_to(work).as_posix() for d in products], (S / 'macrel' / f'{hid}.tar.gz').stat().st_size, flush=True)
