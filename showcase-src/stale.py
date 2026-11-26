#!/usr/bin/env python3
"""Which showcase videos may need filming again: files that changed since each video's preview was last committed.

    python3 showcase-src/stale.py            # since each docs/media/showcase/<shot>.webp (game samples too)
    python3 showcase-src/stale.py <commit>   # since one commit for every shot

Uncommitted edits count. A listed video is a candidate, not a verdict: docs/manual/feature-changes.md#영상을-다시-찍을지-판단
says how to decide. Runtime engine changes are listed for every shot because any of them can reach the screen.
"""
import json, subprocess, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ('engine/src/Engine.Core/', 'engine/src/Engine.Desktop/', 'engine/src/Engine.Player/', 'engine/src/Engine.Authoring/',
           'engine/native/')


def git(*args):
    return subprocess.run(['git', '-C', str(ROOT), *args], capture_output=True, text=True, check=True).stdout.split()


def art_folder(sample):
    return 'tiles' if sample == 'KeyRoomTiles' else sample.lower().removesuffix('lab')


def changes(shot, base=None):
    sample = json.loads(shot.read_text())['sample']
    base = base or next(iter(git('log', '-1', '--format=%H', '--', f'docs/media/showcase/{shot.stem}.webp')), None)
    if base is None:
        return None, [], []  # no committed preview yet to date it; pass a commit
    own = (f'samples/{sample}/', f'showcase-src/{art_folder(sample)}/', f'showcase-src/shots/{shot.name}')
    changed = git('diff', '--name-only', base, '--')
    return base[:7], [p for p in changed if p.startswith(own)], [p for p in changed if p.startswith(RUNTIME)]


if __name__ == '__main__':
    base = sys.argv[1] if len(sys.argv) > 1 else None
    for shot in sorted((ROOT / 'showcase-src/shots').glob('*.json')):
        since, own, runtime = changes(shot, base)
        if since is None:
            print(f'{shot.stem}: no preview in docs/media/showcase, pass a commit to check it')
            continue
        if not own and not runtime:
            continue
        print(f'{shot.stem} (since {since}): {len(own)} sample/shot file(s), {len(runtime)} runtime file(s)')
        for p in own + runtime:
            print(f'    {p}')
