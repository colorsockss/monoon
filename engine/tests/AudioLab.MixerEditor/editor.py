"""AudioLab Audio Mixer dialog (16.10 editor fields): the probe enters the sample's snapshot and ducking rule in the dialog.

editor.py <MoNoon> <MixerEditorProbe.dll> <new folder>
  copies samples/AudioLab to <folder>/project without audioSnapshots and audioDucking; the probe saves them through the
  dialog and compares the GameSettings bytes with samples/AudioLab/GameSettings saved by the engine (result.json).
"""
import json, os, shutil, subprocess, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[3]
SAMPLE = REPO / 'samples/AudioLab'

editor, hook, out = [Path(p).resolve() for p in sys.argv[1:4]]
out.mkdir(parents=True)
project = out / 'project'
shutil.copytree(SAMPLE, project, ignore=shutil.ignore_patterns('.monoon*', '.DS_Store', 'Builds'))
settings = project / 'GameSettings'
text = settings.read_text(encoding='utf-8')
cut = text.index('audioSnapshots:\n')
tail = text[cut:]
# The two keys are the end of the sample's file; nothing else is removed.
assert [l for l in tail.splitlines() if not l.startswith((' ', '-'))] == ['audioSnapshots:', 'audioDucking:'], tail
settings.write_text(text[:cut], encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_MXE_OUT=str(out), MONOON_MXE_PROJECT=str(project),
           MONOON_MXE_REFERENCE=str(SAMPLE), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log,
                          timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
if (out / 'error.txt').exists(): raise SystemExit('FAIL ' + (out / 'error.txt').read_text(encoding='utf-8'))
if code != 0 or not (out / 'result.json').exists(): raise SystemExit(f'FAIL editor exited with {code}; see editor.log')
print(json.dumps(json.loads((out / 'result.json').read_text(encoding='utf-8')), indent=2))
print('PASS Audio Mixer: snapshot and ducking saved byte-identical to the YAML-only GameSettings, messages, one Undo, columns, Play preview')
