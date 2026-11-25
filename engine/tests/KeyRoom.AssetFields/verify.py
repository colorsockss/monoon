"""Roadmap 13.5, 13.6, 13.7, 13.9, 13.10, 13.15, 13.16 and 13.17 through the packaged Mac editor, and the saved choice in Scene Play, Release and Native AOT.

prepare <sample> <dest>: copy KeyRoom and add the QA assets (QaChime.wav, QaSparks.particles, a long-named PNG) and objects
  (QA Sparks emitter, QA Line, QA Light). finder <dir>: write FinderChime.wav outside any project.
editor <app> <project> <probe.dll> <out> <phase> [finder.wav]: one editor launch (edit, trace, field, curves, inspect).
play <player> <out>: headless Release / Native AOT replay with captures. compare <out>...: captures byte-identical.
differs <edited> <baseline>: the saved choice changed what is drawn. contents <build> <id>...: the asset IDs ship in the build.
"""
from pathlib import Path
import hashlib
import json
import math
import os
import shutil
import struct
import subprocess
import sys
import zlib

HERE = Path(__file__).resolve().parent
PROJECT = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
SCENE = 'dd96f9dd-668b-5df5-8786-252e6eb77e9e'  # Showcase.scene, the start scene
FRAMES = 90
CAPTURES = [30, 80]
CHIME = 'a5f1e000-0000-4000-8000-000000000001'
SPARKS = 'a5f1e000-0000-4000-8000-000000000002'
LONG = 'a5f1e000-0000-4000-8000-000000000003'
LONG_PNG = 'QaLongNameDoorMaskForInspectorTrimmingChecks0123456789.png'
QA_OBJECTS = f'''- id: a5f1e000-0000-4000-8000-0000000000a1
  name: QA Sparks
  x: 380
  y: 250
  width: 8
  height: 8
  components:
  - type: particleEmitter2D
    particles: {SPARKS}
    trigger: start
    seed: 7
- id: a5f1e000-0000-4000-8000-0000000000a2
  name: QA Line
  x: 420
  y: 330
  components:
  - type: line2D
    points:
    - {{x: 0, y: 0}}
    - {{x: 120, y: 0}}
    width: 6
    startColor: "#FFFFFF"
    endColor: "#FF3D8B"
- id: a5f1e000-0000-4000-8000-0000000000a3
  name: QA Light
  x: 600
  y: 300
  components:
  - type: pointLight2D
    radius: 60
'''


def wav(path, hz, seconds=.2, rate=22050):
    frames = b''.join(struct.pack('<h', int(9000 * math.sin(2 * math.pi * hz * i / rate))) for i in range(int(rate * seconds)))
    path.write_bytes(b'RIFF' + struct.pack('<I', 36 + len(frames)) + b'WAVEfmt ' + struct.pack('<IHHIIHH', 16, 1, 1, rate, rate * 2, 2, 16) +
                     b'data' + struct.pack('<I', len(frames)) + frames)


def png(path, w, h, rgba):
    rows = b''.join(b'\x00' + bytes(rgba) * w for _ in range(h))
    def chunk(kind, data): return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(rows)) + chunk(b'IEND', b''))


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    assets = dest / 'Assets'
    wav(assets / 'QaChime.wav', 880)
    (assets / 'QaChime.wav.meta').write_text(f'id: {CHIME}\n', encoding='utf-8')
    (assets / 'QaSparks.particles').write_text('rate: 30\nshape: Circle\nradius: 6\nspeed: {min: 20, max: 40}\nlifetime: {min: 1, max: 1.5}\n'
                                               'size: {min: 10, max: 14}\ncolor: {min: "#FFFFFF"}\n', encoding='utf-8')
    (assets / 'QaSparks.particles.meta').write_text(f'id: {SPARKS}\n', encoding='utf-8')
    png(assets / LONG_PNG, 8, 8, (255, 255, 255, 255))
    (assets / (LONG_PNG + '.meta')).write_text(f'id: {LONG}\n', encoding='utf-8')
    # inspect (13.5): a script number the WallWorkshop walls share; the probe attaches it with different values.
    (dest / 'Scripts/QaWall.cs').write_text('using Engine.Core;\n\npublic sealed class QaWall : GameBehaviour\n{\n'
                                            '    public int Strength = 1;\n    public bool Breakable;\n}\n', encoding='utf-8')
    scene = dest / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.endswith('\n') and 'defaultCameraId:' not in text
    scene.write_text(text + QA_OBJECTS, encoding='utf-8')


def finder(folder):
    folder.mkdir(parents=True, exist_ok=True)
    wav(folder / 'FinderChime.wav', 660)
    print(folder / 'FinderChime.wav')


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents: []\n', encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'fields.inputreplay')
    env = dict(os.environ, MONOON_USER_DATA=str(out / 'profile'), MONOON_REPLAY_SNAPSHOTS=f'{out / "captures"};{",".join(map(str, CAPTURES))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'fields.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=600)
    check(out)


def editor(app, project, hook, out, phase, finder_file=None):
    out = out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    if phase == 'trace':
        recording(out / 'fields.inputreplay')
        (out / 'captures.txt').write_text(','.join(map(str, CAPTURES)), encoding='utf-8')
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook.resolve()), MONOON_AF_OUTPUT=str(out), MONOON_AF_PHASE=phase,
               MONOON_AF_FINDER=str(finder_file or ''), MONOON_USER_DATA=str(out / f'profile-{phase}'))
    limit = {'edit': 900, 'trace': 600, 'field': 900, 'curves': 600, 'inspect': 900}[phase] * float(os.environ.get('MONOON_QA_SLOW') or 1)
    with (out / f'{phase}-editor.log').open('wb') as log:
        code = subprocess.run([str(app.resolve()), str(project.resolve())], env=env, stdout=log, stderr=log, timeout=limit).returncode
    if (out / f'{phase}-error.txt').exists(): raise SystemExit(f'FAIL {phase}: ' + (out / f'{phase}-error.txt').read_text(encoding='utf-8'))
    if code != 0 or not (out / f'{phase}.json').exists(): raise SystemExit(f'FAIL {phase}: editor exited with {code}; see {phase}-editor.log')
    if phase == 'trace': check(out)
    print(f'PASS {phase}: ' + (out / f'{phase}.json').read_text(encoding='utf-8'), flush=True)


def check(out):
    for frame in CAPTURES:
        assert (out / 'captures' / f'{frame:04}.png').stat().st_size > 1000, frame


def shots(out): return {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((Path(out) / 'captures').glob('*.png'))}


def compare(*outs):
    first = shots(outs[0])
    assert sorted(first) == [f'{f:04}.png' for f in CAPTURES], first
    for o in outs[1:]:
        assert shots(o) == first, f'{o} captures differ from {outs[0]}: {shots(o)} vs {first}'
    print('\n'.join(f'{k}\tidentical in {len(outs)} runs\tsha256 {v[:16]}' for k, v in first.items()), flush=True)


def differs(edited, baseline):
    a, b = shots(edited), shots(baseline)
    changed = [k for k in a if a[k] != b.get(k)]
    assert changed == list(a), f'the saved Inspector choice did not change every capture: {changed}'
    print(f'edited captures differ from the unedited project at {changed}', flush=True)


def contents(build, *ids):
    """Each asset ID chosen in the Inspector is referenced by the shipped content (packed scene / asset table)."""
    blobs = [p.read_bytes() for p in Path(build).rglob('*') if p.is_file() and p.stat().st_size < 64 << 20]
    for asset in ids:
        found = any(asset.encode() in b or bytes.fromhex(asset.replace('-', '')) in b or _guid_bytes(asset) in b for b in blobs)
        print(f'{asset}\t{"shipped" if found else "MISSING"}', flush=True)
        assert found, asset


def _guid_bytes(text):
    import uuid
    return uuid.UUID(text).bytes_le


if __name__ == '__main__':
    command, *args = sys.argv[1:]
    if command == 'prepare': prepare(Path(args[0]), Path(args[1]))
    elif command == 'finder': finder(Path(args[0]))
    elif command == 'editor': editor(Path(args[0]), Path(args[1]), Path(args[2]), Path(args[3]), args[4], Path(args[5]) if len(args) > 5 else None)
    elif command == 'play': play(Path(args[0]), Path(args[1]))
    elif command == 'compare': compare(*args)
    elif command == 'differs': differs(*args)
    elif command == 'contents': contents(args[0], *args[1:])
