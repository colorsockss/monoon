"""Scene Play half of the CameraLab split-screen check: the packaged Mac editor replays the same recording into the same trace,
after recording where the Inspector's split rows sit (label and field columns must match the rows above them)."""
import os, re
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import recording, check, expect

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
recording(out / 'split.inputreplay')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_SPLIT_OUT=str(out),
           MONOON_SPLIT_TRACE=str(out / 'split.trace'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
for line in (out / 'inspector.txt').read_text(encoding='utf-8').splitlines():
    rows = re.findall(r'([^|:]+): label x=([\d.]+) field x=([\d.]+) w=([\d.]+)', line.split(': ', 1)[1])
    names = [r[0].strip() for r in rows]
    expect({'Split with', 'Merge at', 'Split at', 'Blend', 'Half-life'} <= set(names) and len({r[1:] for r in rows}) == 1,
           f'{line.split(":")[0]}: {", ".join(names)} share one label x, field x and field width')
check(out)
