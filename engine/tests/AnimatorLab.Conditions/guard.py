"""Roadmap 16.5: writes AnimatorLab's Guard clips, controller and scene (run once from the repository root):
python3 engine/tests/AnimatorLab.Conditions/guard.py samples/AnimatorLab
"""
import hashlib
from pathlib import Path
import sys
import uuid

PROJECT = '793807d6-c20d-4c32-982f-08aa2e216b01'
NAMESPACE = uuid.UUID(PROJECT)
IDLE, RISE, LIT = '8ec064c4-dcc5-4e46-b68e-146bc34af0eb', '42500eb3-358b-4dca-aaca-fc496b73f8d6', 'e6d4a1e7-b8ab-4a4e-98a7-3276ad74a946'
REST_CLIP = '47a2ea97-aab3-47ee-b381-8dac6324f9b3'
FILES = ['Assets/Walk.clip', 'Assets/Attack.clip', 'Assets/Recover.clip', 'Assets/Hurt.clip', 'Assets/Guard.animator', 'Scenes/Guard.scene']
IDS = {n: str(uuid.uuid5(NAMESPACE, n)) for n in FILES + ['Guard', 'Camera', 'Canvas', 'Controls', 'State', 'Events']}
CLIPS = {  # name: (frames (sprite, seconds, event), loop)
    'Walk': ([(RISE, 0.15, 'Step'), (IDLE, 0.15, None)], True),
    'Attack': ([(LIT, 0.2, 'Swing'), (RISE, 0.2, None)], False),
    'Recover': ([(IDLE, 0.3, None)], False),
    'Hurt': ([(LIT, 0.1, 'Ouch'), (IDLE, 0.1, None), (LIT, 0.1, None)], False),
}


def write(project):
    project = Path(project)
    for name, (frames, loop) in CLIPS.items():
        text = 'frames:\n' + ''.join(f'- sprite: {s}\n  seconds: {t}\n' + (f'  event: {e}\n' if e else '') for s, t, e in frames)
        (project / f'Assets/{name}.clip').write_text(text + f'loop: {"true" if loop else "false"}\n', encoding='utf-8')
    clip = lambda n: IDS[f'Assets/{n}.clip']
    (project / 'Assets/Guard.animator').write_text(f'''entry: Idle
states:
- name: Idle
  clip: {REST_CLIP}
- name: Walk
  clip: {clip("Walk")}
- name: Attack
  clip: {clip("Attack")}
- name: Recover
  clip: {clip("Recover")}
- name: Hurt
  clip: {clip("Hurt")}
parameters:
- name: Speed
  kind: Float
  default: 0
- name: Grounded
  kind: Bool
  default: 1
- name: Combo
  kind: Int
  default: 0
- name: Attack
  kind: Trigger
  default: 0
- name: Hit
  kind: Trigger
  default: 0
transitions:
- from: Any State
  to: Hurt
  parameter: Hit
  test: Triggered
- from: Idle
  to: Walk
  parameter: Speed
  test: Greater
  value: 0.1
  and:
  - parameter: Grounded
    test: "True"
- from: Walk
  to: Idle
  parameter: Speed
  test: AtMost
  value: 0.1
- from: Idle
  to: Attack
  parameter: Attack
  test: Triggered
  and:
  - parameter: Combo
    test: Less
    value: 3
- from: Walk
  to: Attack
  parameter: Attack
  test: Triggered
  and:
  - parameter: Combo
    test: Less
    value: 3
- from: Attack
  to: Recover
  parameter: ""
  test: ""
  exitTime: 1
- from: Recover
  to: Idle
  parameter: ""
  test: ""
  exitTime: 1
- from: Hurt
  to: Idle
  parameter: Grounded
  test: "True"
  exitTime: 1
''', encoding='utf-8')

    def text(name, y, size, value):
        return (f'- id: {IDS[name]}\n  name: {name}\n  parentId: {IDS["Canvas"]}\n  x: 28\n  y: {y}\n  width: 740\n  height: {size + 16}\n'
                f'  components:\n  - type: uiRect\n  - type: uiText\n    text: "{value}"\n    size: {size}\n')
    (project / 'Scenes/Guard.scene').write_text(f'''schemaVersion: 9
objects:
- id: {IDS["Guard"]}
  name: Guard
  x: 320
  y: 145
  width: 160
  height: 160
  components:
  - type: spriteRenderer
    assetId: {IDLE}
    asset: Assets/Idle.png
  - type: spriteAnimator
    controller: {IDS["Assets/Guard.animator"]}
    playOnStart: true
  - type: script
    script: GuardDemo
    values:
      StateLabel: "{IDS["State"]}"
      EventLabel: "{IDS["Events"]}"
- id: {IDS["Camera"]}
  name: Main Camera
  x: 400
  y: 225
  components:
  - type: camera2D
    zoom: 1
- id: {IDS["Canvas"]}
  name: Canvas
  components:
  - type: uiCanvas
''' + text('Controls', 25, 17, 'Right walk   Left stop + reset combo   Space attack   Up hit   Down jump/land')
      + text('State', 335, 21, 'Idle') + text('Events', 380, 19, 'Frame events')
      + f'defaultCameraId: {IDS["Camera"]}\n', encoding='utf-8')
    for name in FILES:
        (project / (name + '.meta')).write_text(f'id: {IDS[name]}\n', encoding='utf-8')
    manifest = project / '.monoon-assets'
    rows = {}
    for entry in manifest.read_text(encoding='utf-8').split('- id: ')[1:]:
        lines = entry.splitlines()
        rows[lines[1].split('path: ')[1]] = lines[0]
    for name in FILES: rows[name] = IDS[name]
    manifest.write_text(''.join(f'- id: {rows[p]}\n  path: {p}\n  hash: {hashlib.sha256((project / p).read_bytes()).hexdigest().upper()}\n'
                                for p in sorted(rows)), encoding='utf-8')
    settings = project / 'GameSettings'
    text = settings.read_text(encoding='utf-8')
    if IDS['Scenes/Guard.scene'] not in text:
        settings.write_text(text.replace(f'includedScenes:\n- {PROJECT}\n', f'includedScenes:\n- {PROJECT}\n- {IDS["Scenes/Guard.scene"]}\n'), encoding='utf-8')


if __name__ == '__main__':
    write(sys.argv[1])
