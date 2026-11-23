"""Prepare a KeyRoom copy for the copy/paste check and verify both exported players start in the pasted scene."""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
PROJECT = '64fb4cd6-2469-5cec-ba6d-9a49e61f42e7'
SECOND = '97ae2562-6e93-5e7a-be3b-45ec603896c6'  # Scenes/SecondRoom.scene receives the pasted objects
EXPECTED = 'copied 1 chest 1 outside 1'  # Outside repointed in the editor; builds refuse missing references
FORBIDDEN = ('Engine.Authoring', 'Engine.Editor', 'SkiaSharp', 'libSkiaSharp', 'Avalonia', 'MoNoon.dll', 'CopyPasteProbe')

def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    settings = dest / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    assert f'startScene: {PROJECT}' in text and f'- {SECOND}\n' in text
    # Only this copy starts in the room that receives the pasted objects.
    settings.write_text(text.replace(f'startScene: {PROJECT}', f'startScene: {SECOND}'), encoding='utf-8')
    shutil.copyfile(HERE / 'TraceCopies.source', dest / 'Scripts/TraceCopies.cs')

def forbidden(bundle):
    hits = [str(p) for p in Path(bundle).rglob('*') if p.is_file() and any(name in p.name for name in FORBIDDEN)]
    assert not hits, hits
    print('no editor, authoring, Avalonia or SkiaSharp files in', bundle, flush=True)

def play(exe, out):
    out = out.resolve()
    out.mkdir(parents=True)
    frames = [166667] * 10
    (out / 'Second.inputreplay').write_text(f'version: 1\nproject: {PROJECT}\nstartScene: {SECOND}\n'
                                            f'frameTicks: [{", ".join(map(str, frames))}]\nevents: []\n', encoding='utf-8')
    env = dict(os.environ, MONOON_COPY_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
    subprocess.run([str(exe.resolve()), '--replay-input', str(out / 'Second.inputreplay'), str(out / 'slot')], env=env, check=True, timeout=300)
    row = (out / 'trace.txt').read_text(encoding='utf-8').strip()
    assert row == EXPECTED, row
    (out / 'result.json').write_text(json.dumps(dict(trace=row)) + '\n', encoding='utf-8')
    print(row, flush=True)

if __name__ == '__main__':
    {'prepare': prepare, 'play': play, 'forbidden': forbidden}[sys.argv[1]](*(Path(p) for p in sys.argv[2:]))
