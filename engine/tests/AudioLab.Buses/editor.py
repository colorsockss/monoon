"""Record an AudioLab copy's Scene Play output with the AudioLab.Vorbis probe (Play, four music loops, Stop, leave Play).
Usage: editor.py <MoNoon executable> <AudioLab copy> <AudioProbe.dll> <new results folder>
Run it through engine/scripts/gui-lock.sh. Analyze the capture with verify.py check/compare."""
import os
from pathlib import Path
import subprocess
import sys

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_AUDIO_EDITOR=str(out),
           SDL_AUDIO_DRIVER="disk", SDL_AUDIO_FORMAT="F32LE", SDL_AUDIO_CHANNELS="2", SDL_AUDIO_FREQUENCY="48000",
           SDL_AUDIO_DISK_OUTPUT_FILE=str(out / "device.f32"),
           MONOON_AUDIO_CAPTURE=str(out / "mix.f32"), MONOON_USER_DATA=str(out / "profile"))
with (out / "editor.log").open("wb") as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / "error.txt").exists():
    raise RuntimeError((out / "error.txt").read_text(encoding="utf-8"))
print(f"{out.name}: {(out / 'mix.f32').stat().st_size // 8} frames captured")
