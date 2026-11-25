"""Roadmap 13.8: InventoryLab's SupplyTuning (one field of each new kind) edited through the packaged editor's Inspector, saved,
reopened, and replayed headless in Scene Play, Release and Native AOT; the three field traces must be byte-identical and equal
EXPECTED (the values the edit phase typed).

prepare <sample> <dest>: copy InventoryLab and add the QA Supply object (SupplyTuning + FieldTrace) to the start scene.
editor <app> <project> <probe.dll> <out> <phase>: edit (Inspector edits, alignment captures, Undo/Redo, save) or reopen (saved
values in the Inspector, then the Scene Play trace). play <player> <out>: headless Release / Native AOT replay.
compare <out>...: the traces are byte-identical and equal EXPECTED. scan <build>...: no Inspector attribute names in game code.
"""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = 'ade09cc3-a769-56d7-9832-a2bea14c7e71'
SCENE = '0b0b8126-2ff1-5bbb-a7f0-5ef0af802bf8'  # Showcase.scene (the herb yard), the start scene
FRAMES = 30
LONG_KEY = 'an intentionally long supply crate dictionary key for trimming'
QA_OBJECT = '''- id: 0b000000-0000-4000-8000-000000000001
  name: QA Supply
  x: 0
  y: 0
  width: 16
  height: 16
  components:
  - type: script
    script: SupplyTuning
    values: {}
  - type: script
    script: FieldTrace
    values: {}
'''
EXPECTED = '\n'.join([
    'Seed=9000000000', 'SlotMask=4294967295', 'CrateTint=40102030', 'LabelColor=#ABCDEF', 'Tags=Gem, Key',
    'StackSizes=5,7,1,9', 'Weights=0.5,0.3', 'Names=moss,gem,key', 'Unlocked=True,True,True', 'Restock=2,4,6',
    f'Prices=herb:3,key:25,{LONG_KEY}:99', 'FirstWave=8,1.5,Epic,Evening', 'PriceOf(gem)=0']) + '\n'
ATTRIBUTES = [b'ColorUsageAttribute', b'TooltipAttribute', b'HeaderAttribute']


def recording(path):
    path.write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SCENE}\nframeTicks: [' + ', '.join(['166667'] * FRAMES) +
                    ']\nevents: []\n', encoding='utf-8')


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'FieldTrace.source', dest / 'Scripts/FieldTrace.cs')
    scene = dest / 'Scenes/Showcase.scene'
    text = scene.read_text(encoding='utf-8')
    assert text.count('\ndefaultCameraId:') == 1
    scene.write_text(text.replace('\ndefaultCameraId:', '\n' + QA_OBJECT.rstrip('\n') + '\ndefaultCameraId:', 1), encoding='utf-8')
    (dest / 'qa-long-key.txt').write_text(LONG_KEY, encoding='utf-8')


def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'fields.inputreplay')
    env = dict(os.environ, MONOON_FIELD_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'),
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (out / 'player.log').open('wb') as log:
        subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'fields.inputreplay'), str(out / 'slot')],
                       env=env, stdout=log, stderr=log, check=True, timeout=600)
    check(out)


def editor(app, project, hook, out, phase):
    out = out.resolve()
    out.mkdir(parents=True, exist_ok=True)
    recording(out / 'fields.inputreplay')
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook.resolve()), MONOON_SF_OUTPUT=str(out), MONOON_SF_PHASE=phase,
               MONOON_FIELD_TRACE=str(out / 'trace.txt') if phase == 'reopen' else '', MONOON_USER_DATA=str(out / 'profile'))
    with (out / f'{phase}-editor.log').open('wb') as log:
        code = subprocess.run([str(app.resolve()), str(project.resolve())], env=env, stdout=log, stderr=log,
                              timeout=600 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
    if (out / f'{phase}-error.txt').exists(): raise RuntimeError((out / f'{phase}-error.txt').read_text(encoding='utf-8'))
    if code != 0 or not (out / f'{phase}.json').exists(): raise RuntimeError(f'{phase}: editor exited with {code}; see {phase}-editor.log')
    print(f'PASS {phase}: ' + (out / f'{phase}.json').read_text(encoding='utf-8'), flush=True)
    if phase == 'reopen': check(out)


def check(out):
    trace = (out / 'trace.txt').read_text(encoding='utf-8')
    assert trace == EXPECTED, f'{out}: trace differs\n--- got\n{trace}--- want\n{EXPECTED}'
    result = {'lines': trace.count('\n'), 'sha256': hashlib.sha256(trace.encode('utf-8')).hexdigest()[:16]}
    (out / 'result.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


def compare(*outs):
    traces = [(Path(o) / 'trace.txt').read_bytes() for o in outs]
    for o, t in zip(outs[1:], traces[1:]):
        assert t == traces[0], f'{o} trace differs from {outs[0]}'
    assert traces[0].decode('utf-8') == EXPECTED
    lines = traces[0].count(b'\n')
    print(f'trace\t{lines} lines\tidentical in {len(outs)} runs\tsha256 {hashlib.sha256(traces[0]).hexdigest()[:16]}', flush=True)


def scan(*builds):
    """No Inspector attribute names in the game scripts (Release) or the Native AOT executable; [Conditional] leaves them out."""
    for build in map(Path, builds):
        files = [p for p in build.rglob('*') if p.is_file() and (p.name in ('MoNoon.GameScripts.dll', 'MoNoonPlayer'))]
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
    elif command == 'editor': editor(Path(args[0]), Path(args[1]), Path(args[2]), Path(args[3]), args[4])
    elif command == 'check': check(Path(args[0]))
    elif command == 'scan': scan(*args)
