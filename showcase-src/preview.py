#!/usr/bin/env python3
"""Animated WebP previews of showcase videos for the README and manual (GitHub shows them inline; mp4 it does not).

    python3 showcase-src/preview.py ~/Movies/MoNoon-showcase/KeyRoom.mp4 docs/media/showcase/KeyRoom.webp

Frames come from decode.swift (AVFoundation); Pillow writes the looping WebP. Defaults keep a video near 0.2 MB per second:
560 px wide, 24 fps (12 fps looked choppy), lossy quality 45 (CameraLab, 37 s: 6.8 MB). The videos have no sound, so the preview shows everything the mp4 does.
"""
import argparse, shutil, subprocess, tempfile
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def decoder():
    exe = ROOT / 'engine/.artifacts/film/tools/decode'
    src = ROOT / 'showcase-src/decode.swift'
    if not exe.exists() or exe.stat().st_mtime < src.stat().st_mtime:
        exe.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(['swiftc', '-O', str(src), '-o', str(exe)], check=True, stderr=subprocess.DEVNULL)
    return exe


def preview(mp4, out, fps=24, width=560, quality=45):
    work = Path(tempfile.mkdtemp(prefix='preview-'))
    try:
        subprocess.run([str(decoder()), str(mp4), str(work), str(fps), str(width)], check=True, stdout=subprocess.DEVNULL)
        frames = [Image.open(p).convert('RGB') for p in sorted(work.glob('*.png'))]
        out.parent.mkdir(parents=True, exist_ok=True)
        # WebP stores whole milliseconds per frame; spread the rounding so 24 fps does not drift slow.
        duration = [round((i + 1) * 1000 / fps) - round(i * 1000 / fps) for i in range(len(frames))]
        frames[0].save(out, save_all=True, append_images=frames[1:], duration=duration, loop=0,
                       lossy=True, quality=quality, method=4)
    finally:
        shutil.rmtree(work)
    return out.stat().st_size


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('mp4', type=Path); p.add_argument('webp', type=Path)
    p.add_argument('--fps', type=int, default=24); p.add_argument('--width', type=int, default=560)
    p.add_argument('--quality', type=int, default=45)
    a = p.parse_args()
    print(f'{a.webp} {preview(a.mp4.expanduser(), a.webp, a.fps, a.width, a.quality) / 1e6:.2f} MB')
