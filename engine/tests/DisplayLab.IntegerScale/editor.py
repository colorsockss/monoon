"""Scene Play of the integer-scale DisplayLab copy in the packaged Mac editor at three window sizes."""
import json
import os
from pathlib import Path
import subprocess
import sys

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_INTEGER_EDITOR=str(out), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
for row in json.loads((out / 'scene-play.json').read_text()):
    print('PASS Scene Play window', row['window'], 'shows', row['devicePixelsPerGamePixel'], 'device pixels per game pixel')
