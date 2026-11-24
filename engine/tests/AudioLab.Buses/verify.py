"""AudioLab audio buses: the dialogue line ducks the music by -8 dB, sample by sample, and nothing changes when it is unused.

prepare <folder>                     copy samples/AudioLab to <folder>/default and <folder>/ducking (the line plays on start)
play <MoNoonPlayer> <new folder>     headless 480-frame replay with SDL's disk audio driver and MONOON_AUDIO_CAPTURE
check <default run> <ducking run>    default PCM is the 2.5/7.8 baseline; ducking run is the baseline x -8 dB in the line's
                                     pauses and equal to it after the release; writes ducking.json
compare <run> <run> ...              the ducking runs (Scene Play, Release, Native AOT) hold the same first four loops
"""
from array import array
import hashlib
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys

REPO = Path(__file__).resolve().parents[3]
SAMPLE = REPO / "samples/AudioLab"
PROJECT = START_SCENE = "36242c46-b620-5d70-a5a5-d3a858892d65"
FRAME, FRAMES = 166667, 480  # 1/60 s in .NET ticks; 8 s of paced frames hold more than four 1.2 s loops
RATE, PERIOD, LOOPS = 48000, 57600, 4
# results/audio-mixer/playback.csv (osx-arm64). SDL_mixer is native code per architecture; x64 has its own baseline
# (linux-x64: playback-linux-x64.csv, Release = Native AOT on the Debian guest, results/next1-x64).
DEFAULT_SHA256 = {"osx-arm64": "ced6c800e6973e67473b97b0c7e302b84cd3d94078a34e6ae785b1837be5281b",
                  "linux-x64": "ddacda7e39eaedd80c08ff02dee9b27eb7888f22ccc2d53bc9816457bdc8f9e8"}
# First four loops of the ducking run, per architecture (Release = Native AOT on each).
DUCKING_SHA256 = {"osx-arm64": "f6c485bb776456a8acfcc1ef2dd8c45c6a490d08a62b4e0abd1f4a49df8b30ed",
                  "linux-x64": "f7b7a44c4febce53e0a257f154f5c3a56a5dd2609fe3fb4e2ad53c52695f69a5"}
GAIN = 10 ** (-8 / 20)
# Line.wav (24 kHz, one output frame late): syllables at 0-0.20, 0.35-0.55 and 0.70-0.90 s. The middle of each pause has
# no voice samples, even through the resampler's filter.
PAUSES = [(11000, 15400), (28000, 32000)]
RELEASED = 2 * PERIOD  # The line ends at 0.9 s and the 0.4 s release is over well before the third loop.


def prepare(folder):
    folder.mkdir(parents=True)
    for name in ("default", "ducking"):
        shutil.copytree(SAMPLE, folder / name, ignore=shutil.ignore_patterns(".monoon*", "Builds"))
    scene = folder / "ducking/Scenes/AudioLab.scene"
    text = scene.read_text(encoding="utf-8")
    marker = "  name: Line\n"
    at = text.index(marker)
    bus = text.index("    bus: Voice\n", at)
    scene.write_text(text[:bus] + "    bus: Voice\n    playOnStart: true\n" + text[bus + len("    bus: Voice\n"):], encoding="utf-8")


def play(player, out):
    out.mkdir(parents=True)
    record, profile = out / "idle.inputreplay", out / "profile"
    lines = ["version: 1", f"project: {PROJECT}", f"startScene: {START_SCENE}", "frameTicks:"] + [f"- {FRAME}"] * FRAMES
    record.write_text("\n".join(lines + ["events: []"]) + "\n", encoding="utf-8")
    env = dict(os.environ, SDL_VIDEO_DRIVER="monoon-no-video", MONOON_REPLAY_OFFSCREEN="1", SDL_AUDIO_DRIVER="disk",
               SDL_AUDIO_FORMAT="F32LE", SDL_AUDIO_CHANNELS="2", SDL_AUDIO_FREQUENCY=str(RATE),
               SDL_AUDIO_DISK_OUTPUT_FILE=str(out / "device.f32"), MONOON_AUDIO_CAPTURE=str(out / "mix.f32"),
               MONOON_USER_DATA=str(profile))
    run = subprocess.run([str(player), "--replay-input", str(record), str(profile)], env=env, capture_output=True, text=True)
    (out / "player.log").write_text(run.stdout + run.stderr, encoding="utf-8")
    if run.returncode != 0:
        raise SystemExit(f"FAIL {out}: exit {run.returncode}\n{run.stdout}{run.stderr}")
    print(f"{out.name}: {len(samples(out)) // 2} frames, first four loops {first_loops(out)}")


def samples(run):
    data = array("f")
    data.frombytes((run / "mix.f32").read_bytes())
    if sys.byteorder != "little":
        data.byteswap()
    if len(data) < 2 * LOOPS * PERIOD:
        raise ValueError(f"{run}: only {len(data) // 2} captured frames")
    return data


def first_loops(run):
    return hashlib.sha256(samples(run)[:2 * LOOPS * PERIOD].tobytes()).hexdigest()


def host_rid():
    sys.path.insert(0, str(REPO / "engine/tests"))
    from desktop_players import host_rid as rid
    return rid()


def check(default, ducking):
    base, duck = samples(default), samples(ducking)
    want = DEFAULT_SHA256.get(host_rid())
    got = first_loops(default)
    if want and got != want:
        raise ValueError(f"unused buses changed the AudioLab baseline: {got}, expected {want}")
    ducked = DUCKING_SHA256.get(host_rid())
    if ducked and first_loops(ducking) != ducked:
        raise ValueError(f"ducking PCM changed: {first_loops(ducking)}, expected {ducked}")
    worst, ratios = 0.0, []
    for start, end in PAUSES:
        for i in range(2 * start, 2 * end):
            worst = max(worst, abs(duck[i] - base[i] * GAIN))
            if abs(base[i]) > 1e-3:
                ratios.append(duck[i] / base[i])
    if worst > 2e-7 or not ratios:
        raise ValueError(f"music in the line's pauses is not the baseline at -8 dB (max error {worst})")
    db = [20 * math.log10(r) for r in ratios]
    if duck[2 * RELEASED:2 * LOOPS * PERIOD] != base[2 * RELEASED:2 * LOOPS * PERIOD]:
        raise ValueError("music did not return to the baseline after the release")
    restored = next(f for f in range(RELEASED, -1, -1) if duck[2 * f] != base[2 * f] or duck[2 * f + 1] != base[2 * f + 1]) + 1
    result = dict(default_sha256=got, ducking_sha256=first_loops(ducking), pause_samples=len(ratios),
                  pause_db_min=min(db), pause_db_max=max(db), pause_max_error=worst, baseline_from_frame=restored)
    (ducking / "ducking.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"PASS {ducking.name}: pauses {min(db):.4f}..{max(db):.4f} dB over {len(ratios)} samples (max error {worst:.2e}), "
          f"baseline again from frame {restored}; default {got[:8]}, ducking {result['ducking_sha256'][:8]}")


def compare(*runs):
    hashes = {str(r): first_loops(r) for r in runs}
    if len(set(hashes.values())) != 1:
        raise ValueError(f"ducking PCM differs: {hashes}")
    print(f"PASS {len(runs)} runs: first four loops {next(iter(hashes.values()))}")


if __name__ == "__main__":
    command, *paths = sys.argv[1:]
    {"prepare": prepare, "play": play, "check": check, "compare": compare}[command](*(Path(p).resolve() for p in paths))
