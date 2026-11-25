"""Scene Play half of the CameraLab debug drawing check: the packaged Mac editor replays without and with G."""
import os
import re
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import recording

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
for run in ('plain', 'gizmos'):
    recording(out / f'{run}.inputreplay', run == 'gizmos')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_DEBUG_DRAW_OUT=str(out),
           MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
plain, gizmos = (out / 'scene-play.txt').read_text(encoding='utf-8').splitlines()
print(plain); print(gizmos)
def parse(line):
    """'<run> gold=<n> cyan=<n> text=<a;b>' -> (gold, cyan, debug texts); only "x, y" coordinates are gizmos (not HUD text)."""
    head, texts = line.split(' text=')
    fields = dict(f.split('=') for f in head.split()[1:])
    return int(fields['gold']), int(fields['cyan']), [t for t in texts.split(';') if re.fullmatch(r'-?\d+, -?\d+', t)]
ok = parse(plain) == (0, 0, []) and parse(gizmos)[0] > 0 and parse(gizmos)[1] > 0 and parse(gizmos)[2] == ['431, 526'] * parse(gizmos)[0]   # the hero's feet, waiting at the first stop
print(('PASS' if ok else 'FAIL') + ' Scene Play draws the gizmos after G and nothing before', flush=True)
if not ok: raise SystemExit(1)
