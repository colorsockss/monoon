#!/usr/bin/env python3
"""Capture the HexRealm shot without encoding, and locate large adjacent-frame changes."""
import argparse
import csv
import json
import os
from pathlib import Path
import subprocess
import sys

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[3]
SHOT = ROOT / 'showcase-src/shots/HexRealm.json'
FPS = 30
PIXEL_THRESHOLD = 24
FRAME_THRESHOLD = .025


def capture(run):
    """Replay one complete shot. Output frame i always comes from replay tick 2*i."""
    sys.path.insert(0, str(SHOT.parent.parent))
    from film import prepare, recording

    shot = json.loads(SHOT.read_text(encoding='utf-8'))
    if len(shot['parts']) != 1 or 'keep' in shot['parts'][0]:
        raise ValueError('HexRealm must replay in one part without trimming.')
    part, = shot['parts']
    if not 100 <= part['seconds'] <= 130:
        raise ValueError('The HexRealm shot must run for 100–130 seconds.')
    run.mkdir(parents=True)
    raw = run / 'raw'
    raw.mkdir()
    _, app = prepare(shot['sample'], part.get('scene', ''), part.get('profile', 'distribution'))
    settings = next(app.rglob('Game/GameSettings')).read_text(encoding='utf-8')
    replay_text, ticks = recording(part, settings)
    replay = run / 'part0.inputreplay'
    replay.write_text(replay_text, encoding='utf-8')
    profile = run / 'profile0'
    expected = range(0, ticks, 2)
    env = dict(os.environ, MONOON_USER_DATA=str(profile), MONOON_REPLAY_SNAPSHOT_SCALE='1',
               MONOON_REPLAY_SNAPSHOTS=f'{raw};{",".join(map(str, expected))}',
               SDL_VIDEO_DRIVER='monoon-no-video', SDL_AUDIO_DRIVER='monoon-no-audio')
    with (run / 'part0.log').open('wb') as log:
        subprocess.run([str(app / 'Contents/MacOS/MoNoonPlayer'), '--replay-input', str(replay), str(profile)],
                       env=env, stdout=log, stderr=log, check=True, timeout=1800)
    if sorted(p.name for p in raw.glob('*.png')) != [f'{tick:04}.png' for tick in expected]:
        raise RuntimeError('Replay did not capture every requested tick.')
    joined = run / 'joined'
    joined.mkdir()
    for index, tick in enumerate(expected):
        (raw / f'{tick:04}.png').rename(joined / f'{index:05}.png')
    raw.rmdir()
    print(f'{len(expected)} frames, {len(expected) / FPS:.2f}s, {run}')


def audit(joined, output, marks=()):
    output.mkdir(parents=True, exist_ok=True)
    frames = sorted(joined.glob('*.png'))
    if len(frames) < 2 or any(path.name != f'{i:05}.png' for i, path in enumerate(frames)):
        raise ValueError('Expected consecutive output frames starting at zero.')
    previous = None
    hits = {}
    changes = {}
    table = [0] * (PIXEL_THRESHOLD + 1) + [255] * (255 - PIXEL_THRESHOLD)
    with (output / 'changes.tsv').open('w', newline='', encoding='utf-8') as handle:
        rows = csv.writer(handle, delimiter='\t')
        rows.writerow(('frame', 'seconds', 'percent'))
        for i, path in enumerate(frames):
            with Image.open(path) as png:
                current = png.convert('L').resize((320, 180), Image.Resampling.BICUBIC)
            if previous is not None:
                changed = ImageChops.difference(current, previous).point(table).histogram()[255]
                fraction = changed / (320 * 180)
                changes[i] = fraction
                if fraction >= FRAME_THRESHOLD:
                    hits[i] = fraction
                    rows.writerow((i, f'{i / FPS:.3f}', f'{fraction:.3%}'))
            previous = current
    marked = {round(t * FPS) for t in marks}
    if not marked.issubset(changes):
        raise ValueError('A requested mark has no previous frame or is outside the replay.')
    pairs = sorted(set(hits) | marked)
    if marked:
        with (output / 'marks.tsv').open('w', newline='', encoding='utf-8') as handle:
            rows = csv.writer(handle, delimiter='\t')
            rows.writerow(('frame', 'seconds', 'percent'))
            for i in sorted(marked):
                rows.writerow((i, f'{i / FPS:.3f}', f'{changes[i]:.3%}'))
    for i in pairs:
        sheet = Image.new('RGB', (1280, 382), '#202020')
        draw = ImageDraw.Draw(sheet)
        for x, frame in ((0, i - 1), (640, i)):
            with Image.open(frames[frame]) as image:
                sheet.paste(image.resize((640, 360), Image.Resampling.LANCZOS), (x, 22))
            draw.text((x + 8, 5), f'{frame:05}  {frame / FPS:.3f}s  |  delta {changes[i]:.2%}', fill='white')
        sheet.save(output / f'pair-{i:05}.png')
    print(f'{len(frames)} frames, {len(hits)} changes at or above {FRAME_THRESHOLD:.1%}.')
    for i, fraction in hits.items():
        print(f'{i:05}  {i / FPS:7.3f}s  {fraction:7.3%}')
    peak = max(changes, key=changes.get)
    print(f'Peak: {peak:05}  {peak / FPS:7.3f}s  {changes[peak]:7.3%}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    capture_args = commands.add_parser('capture')
    capture_args.add_argument('run', type=Path)
    audit_args = commands.add_parser('audit')
    audit_args.add_argument('joined', type=Path)
    audit_args.add_argument('output', type=Path)
    audit_args.add_argument('--at', nargs='*', type=float, default=[])
    args = parser.parse_args()
    if args.command == 'capture':
        capture(args.run.resolve())
    else:
        audit(args.joined, args.output, args.at)


if __name__ == '__main__':
    main()
