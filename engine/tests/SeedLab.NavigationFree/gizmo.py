"""Scene edit capture: opens a SeedLab copy on the Crowd yard, selects the Yard region and saves the window with the baked
nav-area shading plus the Inspector's Top-down row. Run under the GUI lock with MONOON_QA_WINDOW set to the clip-qa window tool.

gizmo.py <MoNoon> <SeedLab> <CrowdProbe.dll> <out>"""
import os
from pathlib import Path
import shutil
import subprocess
import sys

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
project = out / 'SeedLab'
shutil.copytree(sample, project, ignore=shutil.ignore_patterns('.monoon', '.DS_Store', 'bin', 'obj'))
settings = project / 'GameSettings'
text = settings.read_text(encoding='utf-8')
assert text.count('startScene: cdfa4adc-9fd0-58dc-bdff-271497eca1f0') == 1  # the showcase crowd yard is not the start scene
settings.write_text(text.replace('startScene: cdfa4adc-9fd0-58dc-bdff-271497eca1f0', 'startScene: 578c4bc5-e685-5420-b1ac-42e255eb5bea'), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_CROWD_EDITOR=str(out), MONOON_CROWD_GIZMO='1',
           MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists(): raise RuntimeError((out / 'error.txt').read_text(encoding='utf-8'))
print((out / 'inspector.txt').read_text(encoding='utf-8').strip())
