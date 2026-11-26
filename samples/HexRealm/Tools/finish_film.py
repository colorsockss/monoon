#!/usr/bin/env python3
"""Finish film.py's retained PNGs on a Mac without working VideoToolbox."""
import argparse
from pathlib import Path
import subprocess
import sys


def encode(directory, output, fps, audio=None):
    import bpy

    frames = sorted(directory.glob('*.png'))
    scene = bpy.context.scene
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.resolution_percentage = 100
    scene.render.fps = fps
    scene.frame_start = 1
    scene.frame_end = len(frames)
    scene.render.use_sequencer = True
    scene.view_settings.view_transform = 'Standard'
    editor = scene.sequence_editor_create()
    strip = editor.strips.new_image('HexRealm', str(frames[0]), channel=1, frame_start=1)
    for path in frames[1:]:
        strip.elements.append(path.name)
    strip.frame_final_duration = len(frames)
    if audio:
        editor.strips.new_sound('Game mix', str(audio), channel=2, frame_start=1)
    scene.render.image_settings.media_type = 'VIDEO'
    scene.render.image_settings.file_format = 'FFMPEG'
    scene.render.ffmpeg.format = 'MPEG4'
    scene.render.ffmpeg.codec = 'H264'
    scene.render.ffmpeg.constant_rate_factor = 'NONE'
    scene.render.ffmpeg.ffmpeg_preset = 'REALTIME'
    audio_bitrate = 64 if audio else 0
    scene.render.ffmpeg.video_bitrate = min(3500, round(64000 * fps / len(frames)) - audio_bitrate)
    scene.render.ffmpeg.audio_codec = 'AAC' if audio else 'NONE'
    if audio:
        scene.render.ffmpeg.audio_bitrate = audio_bitrate
    scene.render.filepath = str(output)
    scene.render.use_file_extension = False
    bpy.ops.render.render(animation=True)


def finish():
    from PIL import Image

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    parser.add_argument('out', type=Path)
    parser.add_argument('--blender', type=Path,
                        default=Path('/Applications/Blender.app/Contents/MacOS/Blender'))
    parser.add_argument('--marks', nargs=9, type=float)
    parser.add_argument('--audio', type=Path, help='optional game mix WAV')
    parser.add_argument('--fps', type=int, default=30)
    args = parser.parse_args()
    directory = args.run.resolve() / 'joined'
    files = sorted(directory.glob('*.png'))
    if not files:
        parser.error(f'no retained PNGs in {directory}')
    marks = args.marks or [i * len(files) / (9 * args.fps) for i in range(9)]
    sheet = Image.new('RGB', (1920, 1080))
    for i, seconds in enumerate(marks):
        index = max(0, min(len(files) - 1, round(seconds * args.fps)))
        with Image.open(files[index]) as image:
            sheet.paste(image.convert('RGB').resize((640, 360), Image.Resampling.LANCZOS),
                        (640 * (i % 3), 360 * (i // 3)))
    sheet.save(args.run / 'sheet.png', optimize=True)

    args.out.mkdir(parents=True, exist_ok=True)
    mp4 = args.out.resolve() / 'HexRealm.mp4'
    command = [args.blender, '-b', '-noaudio', '--factory-startup', '--python-exit-code', '1',
               '--python', Path(__file__).resolve(), '--', directory, mp4, str(args.fps)]
    if args.audio:
        command.append(args.audio.resolve(strict=True))
    subprocess.run(command, check=True)
    print(f'{mp4} {len(files) / args.fps:.2f}s | {args.run / "sheet.png"}')


if __name__ == '__main__':
    if '--' in sys.argv:
        source, target, fps, *audio = sys.argv[sys.argv.index('--') + 1:]
        encode(Path(source), Path(target), int(fps), Path(audio[0]) if audio else None)
    else:
        finish()
