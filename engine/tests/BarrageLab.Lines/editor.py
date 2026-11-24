"""Replay BarrageLab's laser and comets as a Scene Play session in the packaged Mac editor (probe: PlatformerLab.Paths)."""
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import CAPTURES, recording, check

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
recording(out / 'Laser.inputreplay')
(out / 'captures.txt').write_text(','.join(map(str, CAPTURES)), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_LINES_EDITOR=str(out),
           MONOON_LINES_WAIT='Laser', MONOON_LINE_TRACE=str(out / 'trace.tsv'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(out)
