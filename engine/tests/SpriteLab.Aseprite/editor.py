import json
import os
from pathlib import Path
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import recording, check, durations
from aseprite_fixture import sample, read_png

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
recording(out / 'Frames.inputreplay', project / 'GameSettings')
(out / 'variant.aseprite').write_bytes(sample(project, (1000, 300), (255, 64, 64)))
# Scene Play's trace runs before the external save, so it describes the committed ArrowCycle.aseprite.
(out / 'played.aseprite').write_bytes((project / 'Assets/ArrowCycle.aseprite').read_bytes())
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project),
           MONOON_ASE_EDITOR=str(out), MONOON_FRAME_TRACE=str(out / 'trace.txt'), MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=900, check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
check(out, out / 'played.aseprite')

# The open editor regenerated the sheet and clip from the externally saved file.
report = json.loads((out / 'reimport.json').read_text(encoding='utf-8'))
clip = (project / 'Assets/ArrowCycle.clip').read_text(encoding='utf-8')
seconds = [float(line.split(':')[1]) for line in clip.splitlines() if line.strip().startswith('seconds:')]
assert seconds == [d / 1000 for d in durations(out / 'variant.aseprite')] == [1.0, 0.3], seconds
width, height, sheet = read_png(project / 'Assets/ArrowCycle.png')
dot = [sheet[(y * width + x) * 4:(y * width + x) * 4 + 4] for y in range(height) for x in range(16, 32)]
assert width == 32 and any(p[3] for p in dot) and all(p[:3] == b'\xff\x40\x40' for p in dot if p[3]), 'dot is not red'
report.update(clipSeconds=seconds, sceneTrace='result.json')
(out / 'reimport.json').write_text(json.dumps(report, indent=1) + '\n', encoding='utf-8')
print(report, flush=True)
