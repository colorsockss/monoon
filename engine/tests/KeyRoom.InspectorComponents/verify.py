"""Roadmap 13.2 runtime: an inactive coin (with a child) that a script turns on, a turned-off collider that a script turns on, a
turned-off renderer and a turned-off script, replayed headless in Release / Native AOT and in Scene Play; the per-tick traces and
captures must be byte-identical.

prepare <sample> <dest>: copy KeyRoom and add the QA objects and trace scripts.
play <player> <out>: headless Release / Native AOT replay. editor <app> <project> <probe.dll> <out>: Scene Play.
compare <out>...: traces and captures must be byte-identical. scan <build>...: no Inspector attribute names in shipped game code.
"""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
SCENE = 'dd96f9dd-668b-5df5-8786-252e6eb77e9e'  # Showcase.scene, the start scene
CAMERA = 'ce171431-9744-52c4-b68b-dc4375b02817'  # its Main Camera
FRAMES = 120
CAPTURES = [20, 50, 100]
QA_OBJECTS = '''- id: 0a000000-0000-4000-8000-000000000001
  name: QA Coin
  tag: QaCoin
  active: false
  x: 420
  y: 240
  width: 24
  height: 24
  components:
  - type: spriteRenderer
    squareColor: "#FFD54A"
  - type: boxCollider2D
    isTrigger: true
- id: 0a000000-0000-4000-8000-000000000002
  parentId: 0a000000-0000-4000-8000-000000000001
  name: QA Coin Glint
  tag: QaCoinChild
  x: 4
  y: 4
  width: 8
  height: 8
  components:
  - type: spriteRenderer
    squareColor: "#FFFFFF"
  - type: script
    script: CoinChildTicker
    values: {}
- id: 0a000000-0000-4000-8000-000000000003
  name: QA Gate
  tag: QaGate
  x: 600
  y: 300
  width: 30
  height: 30
  components:
  - type: boxCollider2D
    enabled: false
  - type: script
    script: DisabledTicker
    enabled: false
    values: {}
  - type: spriteRenderer
    squareColor: "#55AAFF"
- id: 0a000000-0000-4000-8000-000000000004
  name: QA Ghost
  tag: QaGhost
  x: 650
  y: 300
  components:
  - type: spriteRenderer
    squareColor: "#FF4444"
    enabled: false
'''
ATTRIBUTES = [b'TooltipAttribute', b'RangeAttribute', b'HeaderAttribute', b'SpaceAttribute', b'MultilineAttribute',
              b'HideInInspectorAttribute', b'SerializeFieldAttribute', b'MinAttribute']


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents: []\n', encoding='utf-8')


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    for name in ('ComponentTrace', 'CoinChildTicker', 'DisabledTicker'):
        shutil.copyfile(HERE / f'{name}.source', dest / f'Scripts/{name}.cs')
    scene = dest / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    camera = '  name: Main Camera\n'
    head = text.index(f'- id: {CAMERA}\n')
    comps = text.index('  components:\n', head) + len('  components:\n')
    text = text[:comps] + '  - type: script\n    script: ComponentTrace\n    values: {}\n' + text[comps:]
    assert camera in text and text.endswith('\n') and 'defaultCameraId:' not in text
    text += QA_OBJECTS
    scene.write_text(text, encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'components.inputreplay')
    env = dict(os.environ, MONOON_COMPONENT_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'),
               MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'components.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=600)
    check(out)


def editor(app, project, hook, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'components.inputreplay')
    (out / 'captures.txt').write_text(','.join(map(str, CAPTURES)), encoding='utf-8')
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook.resolve()), MONOON_IC_OUTPUT=str(out), MONOON_IC_PHASE='trace',
               MONOON_COMPONENT_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'))
    with (out / 'editor.log').open('wb') as log:
        subprocess.run([str(app.resolve()), str(project.resolve())], env=env, stdout=log, stderr=log,
                       timeout=600 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
    if (out / 'trace-error.txt').exists(): raise RuntimeError((out / 'trace-error.txt').read_text(encoding='utf-8'))
    check(out)


def check(out):
    rows = [list(map(int, line.split(' '))) for line in (out / 'trace.tsv').read_text(encoding='utf-8').splitlines()]
    assert [r[0] for r in rows] == list(range(len(rows))) and len(rows) >= FRAMES - 2, len(rows)
    for t, coin, glint, ticks, enables, gate, hits, ghost, disabled in rows:
        on = 30 <= t < 90  # SetActive(coin) at 30 and back off at 90, applied before the row is written
        assert (coin, glint) == ((1, 1) if on else (0, 0)), (t, coin, glint)
        assert enables == (0 if t < 30 else 1), (t, enables)
        assert gate == (1 if t >= 60 else 0) and hits == gate, (t, gate, hits)
        assert ghost == 0 and disabled == 0, (t, ghost, disabled)
    ticks = [r[3] for r in rows]
    assert ticks[29] == 0 and 55 <= ticks[89] <= 61 and ticks[-1] == ticks[91], ticks[25:95]
    for frame in CAPTURES:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame
    result = {'rows': len(rows), 'childTicks': ticks[-1], 'sha256': hashlib.sha256((out / 'trace.tsv').read_bytes()).hexdigest()[:16]}
    (out / 'result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


def compare(*outs):
    outs = [Path(o) for o in outs]
    traces = [(o / 'trace.tsv').read_bytes() for o in outs]
    for o, t in zip(outs[1:], traces[1:]):
        assert t == traces[0], f'{o} trace differs from {outs[0]}'
    rows = traces[0].count(b"\n")
    lines = [f"trace\t{rows} rows\tsha256 {hashlib.sha256(traces[0]).hexdigest()[:16]}"]
    shots = sorted(p.name for p in (outs[0] / 'captures').glob('*.png'))
    for shot in shots:
        hashes = [hashlib.sha256((o / 'captures' / shot).read_bytes()).hexdigest() for o in outs]
        assert len(set(hashes)) == 1, f'captures differ at {shot}: {hashes}'
        lines.append(f'{shot}\tidentical\tsha256 {hashes[0][:16]}')
    first = {s: hashlib.sha256((outs[0] / 'captures' / s).read_bytes()).hexdigest() for s in shots}
    assert first['0020.png'] != first['0050.png'], 'the coin turned on at tick 30 is drawn at 50'
    print('\n'.join(lines), flush=True)


def scan(*builds):
    """No Inspector attribute type names in the game scripts (Release) or the Native AOT executable."""
    for build in map(Path, builds):
        files = [p for p in build.rglob('*') if p.is_file() and (p.name in ('MoNoon.GameScripts.dll', 'MoNoonPlayer') or p.suffix == '.dll' and 'GameScripts' in p.name)]
        assert files, f'no game code in {build}'
        for path in files:
            data = path.read_bytes()
            found = [a.decode() for a in ATTRIBUTES if a in data]
            print(f'{path.relative_to(build)}\t{len(data)} B\tattribute names: {found or "none"}', flush=True)
            assert not found, (path, found)


if __name__ == '__main__':
    command, *args = sys.argv[1:]
    if command == 'compare': compare(*args)
    elif command == 'prepare': prepare(Path(args[0]), Path(args[1]))
    elif command == 'play': play(Path(args[0]), Path(args[1]))
    elif command == 'editor': editor(Path(args[0]), Path(args[1]), Path(args[2]), Path(args[3]))
    elif command == 'check': check(Path(args[0]))
    elif command == 'scan': scan(*args)
