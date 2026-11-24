"""Roadmap 13.11: Play Inspector edits of bool, enum, Vector2, string and colour script fields and of built-in values (sprite tint,
collider on/off) in the packaged Mac editor; Apply selected keeps the checked ones, Apply all the rest, Stop discards an unapplied
edit, and two Undo steps give back the scene text from before Play.

prepare <sample> <dest>: copy KeyRoom and attach PlayKnobs to the Player.
editor <app> <project> <probe.dll> <out>: run the probe (GUI lock required) and check play.json.
"""
from pathlib import Path
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
    shutil.copyfile(HERE / 'PlayKnobs.source', dest / 'Scripts/PlayKnobs.cs')
    scene = dest / 'Scenes/KeyRoom.scene'
    text = scene.read_text(encoding='utf-8')
    head = text.index('  name: Player\n')
    comps = text.index('  components:\n', head) + len('  components:\n')
    scene.write_text(text[:comps] + '  - type: script\n    script: PlayKnobs\n    values: {}\n' + text[comps:], encoding='utf-8')


def editor(app, project, hook, out):
    out = out.resolve()
    out.mkdir(parents=True)
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook.resolve()), MONOON_PF_OUTPUT=str(out), MONOON_USER_DATA=str(out / 'profile'))
    with (out / 'editor.log').open('wb') as log:
        code = subprocess.run([str(app.resolve()), str(project.resolve())], env=env, stdout=log, stderr=log,
                              timeout=600 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
    if (out / 'error.txt').exists(): raise SystemExit('FAIL ' + (out / 'error.txt').read_text(encoding='utf-8'))
    if code != 0 or not (out / 'play.json').exists(): raise SystemExit(f'FAIL editor exited with {code}; see editor.log')
    result = json.loads((out / 'play.json').read_text(encoding='utf-8'))
    print(json.dumps(result, indent=2), flush=True)
    print('PASS 13.11 Play edits, Apply selected / Apply all, Stop discards, Undo restores', flush=True)


if __name__ == '__main__':
    command, *args = sys.argv[1:]
    if command == 'prepare': prepare(Path(args[0]), Path(args[1]))
    elif command == 'editor': editor(*map(Path, args[:4]))
