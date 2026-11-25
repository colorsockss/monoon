"""Run the same three moves-showcase recordings as Scene Play sessions in the packaged Mac editor."""
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import RUNS, recording, check

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
for name, events in RUNS.items():
    recording(out / f'{name}.inputreplay', events)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project),
           MONOON_REBIND_EDITOR=str(out), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(out)
