"""Editor: Walk state dialog alignment, Animator preview driven tick by tick (preview.trace), then Scene Play of the same recording."""
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import recording, check, inputs

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
recording(out / 'scout.inputreplay')
(out / 'inputs.txt').write_text(''.join(f'{x} {y} {s}\n' for x, y, s in inputs()), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_SCOUT_EDITOR=str(out),
           MONOON_SCOUT_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(out / 'preview.trace')
check(out / f'{out.name}.trace')
