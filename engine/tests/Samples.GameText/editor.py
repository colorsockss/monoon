"""Replay a GameText scenario as a Scene Play session in the packaged Mac editor and save the same frames."""
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import SCENARIOS, recording, check

name = sys.argv[1]
editor, project, hook, out = [Path(p).resolve() for p in sys.argv[2:]]
out.mkdir(parents=True)
recording(name, out / 'input.inputreplay')
(out / 'captures.txt').write_text(','.join(map(str, SCENARIOS[name]['captures'])), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_GAME_TEXT_EDITOR=str(out),
           MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(name, out)
