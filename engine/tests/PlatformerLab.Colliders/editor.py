from pathlib import Path
import os
import shutil
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import HERE, check

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
shutil.copyfile(HERE/'RoundTrip.inputreplay', out/'RoundTrip.inputreplay')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_COLLIDER_EDITOR=str(out),
           MONOON_COLLIDER_TRACE=str(out/'trace.tsv'), MONOON_USER_DATA=str(out/'profile'))
with (out/'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out/'error.txt').exists(): raise RuntimeError((out/'error.txt').read_text(encoding='utf-8'))
check(out)
