"""Prepare a KeyRoom copy for the prefab structure check and verify both exported players."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
WORKSHOP = 'a01a07b1-5e1b-4754-b594-25e9a463225c'  # Scenes/PrefabWorkshop.scene
EXPECTED = ['210 1 0', '370 1 1', '530 1 0']

def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    workshop = WORKSHOP
    assert f'- id: {workshop}\n  path: Scenes/PrefabWorkshop.scene' in (dest / '.monoon-assets').read_text(encoding='utf-8')
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    # The workshop is an editor sample. Only this copy starts in it and exports it.
    text = text.replace(f'startScene: {PROJECT}', f'startScene: {workshop}').replace('includedScenes:\n', f'includedScenes:\n- {workshop}\n', 1)
    settings.write_text(text, encoding='utf-8')
    scene = dest / 'Scenes/PrefabWorkshop.scene'
    anchor = '  - type: camera2D\n'
    body = scene.read_text(encoding='utf-8')
    assert body.count(anchor) == 1
    scene.write_text(body.replace(anchor, '  - type: script\n    script: TraceChests\n' + anchor), encoding='utf-8')
    shutil.copyfile(HERE / 'TraceChests.source', dest / 'Scripts/TraceChests.cs')

def recording(path):
    frames = [166667] * 10
    text = f'version: 1\nproject: {PROJECT}\nstartScene: {WORKSHOP}\nframeTicks: [{", ".join(map(str, frames))}]\nevents: []\n'
    path.write_text(text, encoding='utf-8')

def check(out):
    rows = (out / 'trace.txt').read_text(encoding='utf-8').splitlines()
    assert rows == EXPECTED, rows
    result = dict(chests=[dict(zip(['x', 'locks', 'sound'], map(int, r.split()))) for r in rows])
    (out / 'result.json').write_bytes((json.dumps(result) + '\n').encode())
    print(result, flush=True)

def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    recording(out / 'Workshop.inputreplay')
    env = dict(os.environ, MONOON_CHEST_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'Workshop.inputreplay'), str(out / 'slot')], env=env, check=True, timeout=300)
    check(out)

if __name__ == '__main__':
    {'prepare': prepare, 'play': play, 'check': check}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
