"""Editor: transition dialog alignment, Animator preview driven tick by tick (preview.trace), then Scene Play of the same recording."""
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import recording, check, PRESSES, FRAMES

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
recording(out / 'guard.inputreplay')
(out / 'presses.txt').write_text(''.join(f'{f} {k}\n' for f, k in PRESSES.items()), encoding='utf-8')
(out / 'frames.txt').write_text(str(FRAMES), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_GUARD_EDITOR=str(out),
           MONOON_GUARD_TRACE=str(out / f'{out.name}.trace'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(out / 'preview.trace')
check(out / f'{out.name}.trace')
