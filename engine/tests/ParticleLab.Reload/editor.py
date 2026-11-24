"""Roadmap 14.8 in the packaged editor: edit a .particles asset in the Particle panel during Scene Play and check that the
running emitters of both prefabs that use it reload (kept or restarted per the documented rule), one Undo record per change."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

MODULES, PRESETS = 'b702dcb2-01f9-5904-ad61-e62f4a348991', '5b0f3c1e-8a2d-4f6b-9c3e-7d1a2b4c6e80'
editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:5]]
out.mkdir(parents=True)
project = out / 'ParticleLab'
shutil.copytree(sample, project, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
settings = project / 'GameSettings'
text = settings.read_text(encoding='utf-8')
assert f'startScene: {MODULES}' in text
settings.write_text(text.replace(f'startScene: {MODULES}', f'startScene: {PRESETS}', 1), encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_PARTICLE_RELOAD=str(out),
           MONOON_USER_DATA=str(out / 'profile'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists():
    raise SystemExit('FAIL editor: ' + (out / 'error.txt').read_text(encoding='utf-8'))
r = json.loads((out / 'result.json').read_text(encoding='utf-8'))
failures = []


def check(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message, flush=True)
    if not ok:
        failures.append(message)


n = r['preset']
check(n['created_records'] == 1 and n['same_as_preset'], f"Project + → New Particle Effect → Dust writes the preset as Assets/Dust.particles, one Undo record {n}")
check(n['undo_removed'], 'Undo removes the new asset and its .meta')
check(n['heading_center_offset'] <= 1 and n['button_inside'], f"the panel heading keeps title and preset button on one centre line {n}")
p = r['panel_during_play']
check(p['enabled'] and p['playing'] and p['isolated_ticks'] > 10, f"the Particle panel stays enabled during Play and its preview runs {p}")
green, restart = r['edits']
for e in r['edits']:
    check(e['records'] == 1 and e['playing'], f"{e['edit']}: one Undo record while Play keeps running")
    check(e['expected'], f"{e['edit']}: {e['log']}")
# The game keeps ticking until the file watch sees the save, so counts move; Engine.Checks compares the kept particles exactly.
check(all(c >= 10 for c in green['counts_after']), f"the colour change kept both prefabs' live embers {green['counts_before']} → {green['counts_after']}")
check(r['colours_after'] == [['00FF00'], ['00FF00']], f"after one lifetime every ember of Torch and Brazier is the new colour {r['colours_after']}")
check(all(c < 10 for c in restart['counts_after']), f"a new Max particles restarted both emitters {restart['counts_before']} → {restart['counts_after']}")
check(r['file_max_particles'] == 80, 'the panel saved the asset')
check(r['undo_restores_file'], 'after Stop, two Undo steps restore the asset bytes')
if failures:
    raise SystemExit(f'FAIL: {len(failures)} check(s)')
print('PASS particle reload in', out)
