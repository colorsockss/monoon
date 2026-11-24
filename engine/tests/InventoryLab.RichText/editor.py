"""Replay the herbalist's rich text dialogue as a Scene Play session in the packaged Mac editor."""
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import CAPTURES, recording, check

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
recording(out / 'Herbalist-rich.inputreplay')
(out / 'captures.txt').write_text(','.join(map(str, CAPTURES)), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_RICH_TEXT_EDITOR=str(out),
           MONOON_REVEAL_TRACE=str(out / 'reveal.tsv'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(out)
