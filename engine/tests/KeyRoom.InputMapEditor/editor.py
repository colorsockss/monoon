"""KeyRoom Input Actions panel (16.12 editor fields): the probe types the KeyRoom.InputInteractions rules into the panel.

editor.py <MoNoon> <InputMapEditorProbe.dll> <new folder>
  copies samples/KeyRoom to <folder>/project and prepares the YAML-only reference with KeyRoom.InputInteractions/verify.py
  prepare in <folder>/reference; the probe saves through the panel and compares the GameSettings bytes (result.json).
"""
import json, os, shutil, subprocess, sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
sys.path.insert(0, str(HERE.parent / 'KeyRoom.InputInteractions'))
from verify import prepare  # noqa: E402

editor, hook, out = [Path(p).resolve() for p in sys.argv[1:4]]
out.mkdir(parents=True)
project, reference = out / 'project', out / 'reference'
shutil.copytree(REPO / 'samples/KeyRoom', project, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
prepare(REPO / 'samples/KeyRoom', reference)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_IME_OUT=str(out), MONOON_IME_PROJECT=str(project),
           MONOON_IME_REFERENCE=str(reference), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log,
                          timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
if (out / 'error.txt').exists(): raise SystemExit('FAIL ' + (out / 'error.txt').read_text(encoding='utf-8'))
if code != 0 or not (out / 'result.json').exists(): raise SystemExit(f'FAIL editor exited with {code}; see editor.log')
print(json.dumps(json.loads((out / 'result.json').read_text(encoding='utf-8')), indent=2))
print('PASS Input Actions panel: rules and vectors saved byte-identical to the YAML-only GameSettings, messages, one Undo, columns')
