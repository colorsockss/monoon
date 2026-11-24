"""Window captures of the Cave on the Metal renderer: the packaged editor's Scene view (edit-mode lighting preview) or a
built game's window. Run under engine/scripts/gui-lock.sh. MONOON_QA_WINDOW is the clip-qa window tool.

window.py editor <MoNoon> <project copy> <out.png>   |   window.py game <MoNoonPlayer> <out.png>
"""
from pathlib import Path
import os
import subprocess as sp
import sys
import time

tool = os.environ['MONOON_QA_WINDOW']
mode, exe, *rest = sys.argv[1:]
out = Path(rest[-1]).resolve()
env = dict(os.environ, MONOON_USER_DATA=str(out.parent / f'{out.stem}-profile'))
proc = sp.Popen([str(Path(exe).resolve())] + ([str(Path(rest[0]).resolve())] if mode == 'editor' else []), env=env,
                stdout=sp.DEVNULL, stderr=sp.DEVNULL)
try:
    time.sleep(25 if mode == 'editor' else 6)
    w, x, y, ww, hh = map(int, sp.check_output([tool, 'info', str(proc.pid)]).split())
    sp.run(['screencapture', '-x', '-o', '-l', str(w), str(out)], check=True)
finally:
    proc.terminate()
    proc.wait(timeout=20)
print(out)
