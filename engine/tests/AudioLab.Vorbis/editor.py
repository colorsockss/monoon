"""Record AudioLab's Scene Play output in the editor and analyze it like a distribution capture.
Usage: editor.py <MoNoon executable> <AudioLab copy> <AudioProbe.dll> <new results folder>"""
import json
import os
from pathlib import Path
import subprocess
import sys

from measure import read_capture

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_AUDIO_EDITOR=str(out),
           SDL_AUDIO_DRIVER="disk", SDL_AUDIO_FORMAT="F32LE", SDL_AUDIO_CHANNELS="2", SDL_AUDIO_FREQUENCY="48000",
           SDL_AUDIO_DISK_OUTPUT_FILE=str(out / "device.f32"),
           MONOON_AUDIO_CAPTURE=str(out / "mix.f32"), MONOON_USER_DATA=str(out / "profile"))
with (out / "editor.log").open("wb") as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=600, check=True)
if (out / "error.txt").exists():
    raise RuntimeError((out / "error.txt").read_text(encoding="utf-8"))
_, _, metrics = read_capture(out / "mix.f32")
before = int((out / "before-f.txt").read_text())
if not before <= metrics["stop_frame"] <= before + 48000:
    raise ValueError(f"Fade started at {metrics['stop_frame']} frames, Stop was called at {before}")
(out / "measurements.json").write_text(json.dumps(metrics, indent=2) + "\n")
print(f"PASS scene-play: {metrics['loop_frames']} frames per loop, {metrics['fade_in_frames']} in, "
      f"{metrics['fade_out_frames']} out. PCM SHA-256 {metrics['first_four_loops_sha256']}")
