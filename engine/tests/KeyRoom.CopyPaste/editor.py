import os
from pathlib import Path
import subprocess
import sys

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_COPY_EDITOR=str(out),
           MONOON_COPY_TRACE=str(out / 'play-trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=1800, check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
print((out / 'editor.txt').read_text(encoding='utf-8'), end='')
