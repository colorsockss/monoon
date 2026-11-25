"""Particle panel probe: open a ParticleLab copy in the workshop showcase, edit Flame.particles through the panel controls, check the file,
Undo/Redo bytes, the preview against the committed Scene Play/Release/AOT trace, and row alignment."""
import json
import re
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[2]
CAMP, WORKSHOP = 'b8f2ab84-5869-5a85-a9cc-f8245e12d7c1', '20bac3a9-6b46-52fc-8c71-ee549360abb8'

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
project = out / 'ParticleLab'
shutil.copytree(sample, project, ignore=shutil.ignore_patterns('.monoon', '.monoon-compiled', '.DS_Store', 'bin', 'obj'))
settings = project / 'GameSettings'
text = settings.read_text(encoding='utf-8')
assert f'startScene: {CAMP}' in text
settings.write_text(text.replace(f'startScene: {CAMP}', f'startScene: {WORKSHOP}', 1), encoding='utf-8')
# Keep only the workshop's still Curves emitters (Bubbles, Flame, Pinwheels): the panel's Play all previews every emitter, and
# the preview steps them in place, so path followers and the other stations would not match the committed Curves trace.
scene = project / 'Scenes/ShowcaseWorkshop.scene'
head, body = scene.read_text(encoding='utf-8').split('objects:\n', 1)
blocks = re.split(r'(?m)^(?=- id: )', body)
tail = blocks[-1].split('\ndefaultCameraId', 1)
blocks[-1] = tail[0] + '\n'
kept = [b for b in blocks if b.startswith('- id: ') and ('particleEmitter2D' not in b or '  - Curves\n' in b)]
scene.write_text(head + 'objects:\n' + ''.join(kept) + 'defaultCameraId' + tail[1], encoding='utf-8')
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_PARTICLE_EDITOR=str(out),
           MONOON_USER_DATA=str(out / 'profile'),
           MONOON_PARTICLE_TRACE_REF=str(REPO / 'engine/tests/results/particles/osx-arm64/curves-trace.tsv'))
with (out / 'editor.log').open('wb') as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / 'error.txt').exists():
    raise SystemExit((out / 'error.txt').read_text(encoding='utf-8'))
r = json.loads((out / 'result.json').read_text(encoding='utf-8'))
failures = []


def check(ok, message):
    print(('PASS ' if ok else 'FAIL ') + message)
    if not ok:
        failures.append(message)


check(r['selected_owners'] == 1 and r['viewport_preview'], f"Scene view previews only the selected emitter ({r['selected_owners']})")
check(r['all_owners'] == 3, f"Play all previews every Curves emitter ({r['all_owners']})")
for t in r['trace']:
    check(t['count'] == t['trace_count'] and t['hash'] == t['trace_hash'],
          f"preview tick {t['tick']}: {t['count']} particles {t['hash']} = trace {t['trace_count']} {t['trace_hash']}")
s = r['scrub']
check(s['tick'] == 90 and s['scene_same'] and s['isolated_same'] and s['isolated_count'] > 0, f"scrub to 1.5 s equals playing 90 ticks {s}")
p = r['playback']
check(p['ticks_advanced'] > 60 or p['wrapped'], f"preview plays at the fixed tick {p}")
check(p['files_unchanged'], "two seconds of preview playback change no scene or asset file")
for e in r['edits']:
    check(e['records'] == 1, f"{e['edit']}: one Undo record ({e['records']})")
    check(e['undo_bytes'] and e['redo_bytes'] and e['undo_again_bytes'], f"{e['edit']}: Undo restores the asset bytes, Redo reapplies them")
curve, gradient, color = r['edits']
check(abs(curve['key_time'] - .3) < 1e-6 and abs(curve['key_value'] - 1.25) < 1e-6, f"curve key written {curve['key_time']}, {curve['key_value']}")
check(curve['lut_same'] and gradient['lut_same'], "curve and gradient editors draw the runtime LUT")
check(curve['panel_reloaded'], "the panel shows the restored asset after Undo")
check(abs(gradient['key_time'] - .5) < 1e-6, f"gradient key written at {gradient['key_time']}")
check(color['color'] == '#3366CC', f"ColorView colour written {color['color']}")
check(r['scene_unchanged'] and r['files_after_undo'], "scene file unchanged; every file back to its original bytes after Undo")
check(r['selection_keeps_layout'], "selecting another curve or gradient key does not move any field")
for view in r['inspections']:
    rows = view['rows']
    label_x = sorted({round(x['label_x'] * 2) / 2 for x in rows})
    field_x = sorted({round(x['field_x'] * 2) / 2 for x in rows})
    offset = max(x['center_offset'] for x in rows)
    heights = sorted({x['field_height'] for x in rows if x['field_type'] in ('TextBox', 'NumericUpDown', 'ComboBox', 'Button')})
    bare = [x['text'] for x in rows if x['natural'] > x['label_width'] + .5 and not (x['trimmed'] and x['tip'])]
    outside = [x['text'] for x in rows if not x['inside']]
    print(f"{view['capture']}: window {view['window_width']:.0f}px, panel {view['panel_width']}px, {len(rows)} rows, label x {label_x}, field x {field_x}")
    check(len(rows) >= 30 and len(label_x) == 1 and len(field_x) == 1, f"{view['capture']}: labels and fields share one left edge")
    check(offset <= 1, f"{view['capture']}: label and field centres within 1px ({offset})")
    check(len(heights) == 1, f"{view['capture']}: input fields share one height {heights}")
    check(not bare and not outside, f"{view['capture']}: clipped labels trim with a tooltip {bare}, fields inside the panel {outside}")
t = r['long_title']
check(t['trimmed'] and t['tip'] and t['inside'], f"long object name trims in the panel title with a tooltip {t}")
if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print('PASS Particle panel in', out)
