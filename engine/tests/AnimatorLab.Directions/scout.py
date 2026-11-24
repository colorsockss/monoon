"""Roadmap 16.5: writes AnimatorLab's Scout sheet, 12 direction clips, controller and scene, and holds the independent direction
model the checks use. Run once from the repository root: python3 engine/tests/AnimatorLab.Directions/scout.py samples/AnimatorLab
"""
import hashlib
import math
from pathlib import Path
import struct
import sys
import uuid
import zlib

PROJECT = '793807d6-c20d-4c32-982f-08aa2e216b01'
NAMESPACE = uuid.UUID(PROJECT)
NAMES = ['N', 'NE', 'E', 'SE', 'S', 'SW', 'W', 'NW']           # clockwise from up; world +Y points down
DIRS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]
CELL = 32
WALK = [f'Assets/ScoutWalk{n}.clip' for n in NAMES]
IDLE = [f'Assets/ScoutIdle{n}.clip' for n in ['N', 'E', 'S', 'W']]
FILES = ['Assets/Scout.png'] + WALK + IDLE + ['Assets/Scout.animator', 'Scenes/Scout.scene']
IDS = {n: str(uuid.uuid5(NAMESPACE, n)) for n in FILES + ['Scout', 'Scout camera', 'Scout canvas', 'Scout controls', 'Scout state']}


def png(path, pixels, width, height):
    raw = b''.join(b'\0' + bytes(c for px in pixels[y * width:(y + 1) * width] for c in px) for y in range(height))
    def chunk(kind, data): return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) +
                     chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b''))


def sheet():
    """8 columns (N..NW) x 3 rows (walk frame 0, walk frame 1, idle): a round body with a nose pointing the direction."""
    w, h = 8 * CELL, 3 * CELL
    px = [(0, 0, 0, 0)] * (w * h)
    colours = [((58, 170, 158, 255), (20, 70, 64, 255)), ((70, 190, 176, 255), (20, 70, 64, 255)), ((226, 172, 72, 255), (96, 64, 20, 255))]
    for row, (body, nose) in enumerate(colours):
        for col, (dx, dy) in enumerate(DIRS):
            length = math.hypot(dx, dy)
            ux, uy = dx / length, dy / length
            step = 1.5 if row == 1 else 0                  # the second walk frame leans forward
            cx, cy = 15.5 + ux * step, 15.5 + uy * step
            nx, ny = cx + ux * 9, cy + uy * 9
            for y in range(CELL):
                for x in range(CELL):
                    if (x - nx) ** 2 + (y - ny) ** 2 <= 12:
                        px[(row * CELL + y) * w + col * CELL + x] = nose
                    elif (x - cx) ** 2 + (y - cy) ** 2 <= 100:
                        px[(row * CELL + y) * w + col * CELL + x] = body
    return px, w, h


def clip(col, rows, seconds, loop):
    frames = ''.join(f'- sprite: {IDS["Assets/Scout.png"]}\n  seconds: {seconds}\n  source:\n    x: {col * CELL}\n    y: {r * CELL}\n'
                     f'    width: {CELL}\n    height: {CELL}\n' for r in rows)
    return 'frames:\n' + frames + f'loop: {"true" if loop else "false"}\n'


def write(project):
    project = Path(project)
    px, w, h = sheet()
    png(project / 'Assets/Scout.png', px, w, h)
    for i, name in enumerate(WALK):
        (project / name).write_text(clip(i, [0, 1], 0.15, True), encoding='utf-8')
    for i, name in enumerate(IDLE):
        (project / name).write_text(clip(2 * i, [2], 1, True), encoding='utf-8')
    walk = ''.join(f'  - {IDS[n]}\n' for n in WALK)
    idle = ''.join(f'  - {IDS[n]}\n' for n in IDLE)
    (project / 'Assets/Scout.animator').write_text(f'''entry: Idle
states:
- name: Idle
  clip: {IDS[IDLE[0]]}
  directions:
{idle}  direction: Move
- name: Walk
  clip: {IDS[WALK[0]]}
  directions:
{walk}  direction: Move
parameters:
- name: Move
  kind: Vector2
  default: 0
- name: Speed
  kind: Float
  default: 0
transitions:
- from: Idle
  to: Walk
  parameter: Speed
  test: Greater
  value: 0.1
- from: Walk
  to: Idle
  parameter: Speed
  test: AtMost
  value: 0.1
''', encoding='utf-8')

    def text(name, y, size, value):
        return (f'- id: {IDS[name]}\n  name: {name}\n  parentId: {IDS["Scout canvas"]}\n  x: 28\n  y: {y}\n  width: 740\n  height: {size + 16}\n'
                f'  components:\n  - type: uiRect\n  - type: uiText\n    text: "{value}"\n    size: {size}\n')
    (project / 'Scenes/Scout.scene').write_text(f'''schemaVersion: 9
objects:
- id: {IDS["Scout"]}
  name: Scout
  x: 336
  y: 161
  width: 128
  height: 128
  components:
  - type: spriteRenderer
    assetId: {IDS["Assets/Scout.png"]}
    asset: Assets/Scout.png
  - type: spriteAnimator
    controller: {IDS["Assets/Scout.animator"]}
    playOnStart: true
  - type: script
    script: ScoutDemo
    values:
      StateLabel: "{IDS["Scout state"]}"
- id: {IDS["Scout camera"]}
  name: Main Camera
  x: 400
  y: 225
  components:
  - type: camera2D
    zoom: 1
- id: {IDS["Scout canvas"]}
  name: Canvas
  components:
  - type: uiCanvas
''' + text('Scout controls', 25, 17, 'Arrows walk in 8 directions (Idle faces 4)   Space sweeps a slow turn')
      + text('Scout state', 350, 21, 'Idle  S') + f'defaultCameraId: {IDS["Scout camera"]}\n', encoding='utf-8')
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
    scene = IDS['Scenes/Scout.scene']
    if scene not in text:
        anchor = 'includedScenes:\n'
        lines = text.split(anchor, 1)
        listed = lines[1].split('\n')
        n = next(i for i, line in enumerate(listed) if not line.startswith('- '))
        settings.write_text(lines[0] + anchor + '\n'.join(listed[:n] + [f'- {scene}'] + listed[n:]), encoding='utf-8')


# Independent direction model (the controller rule written out): keep the current direction while the vector stays within
# 30 degrees (8 directions) or 60 degrees (4 directions) of it, otherwise take the nearest; a zero vector keeps the facing
# (a 4-direction set shows a diagonal facing as the axis before it clockwise: NE as N).
def pick(facing, count, x, y):
    def score(i):
        dx, dy = DIRS[i * 8 // count]
        dot = x * dx + y * dy
        return dot * dot / (dx * dx + dy * dy) if dot > 0 else -1
    slot = facing * count // 8
    length = x * x + y * y
    if length > 0 and score(slot) < (0.75 if count == 8 else 0.25) * length:
        slot = max(range(count), key=lambda i: (score(i), -i))
    return slot * 8 // count


if __name__ == '__main__':
    write(sys.argv[1])
