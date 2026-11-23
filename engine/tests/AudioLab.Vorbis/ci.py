"""Build both AudioLab distributions on this host and compare their playback with the recording for this RID."""
import argparse
import csv
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import time
import traceback

from measure import BLOCK, PERIOD, RATE, read_capture

TESTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TESTS))
from desktop_players import REPO, build_player, host_rid, prepare_tools

PROJECT = REPO / "samples/AudioLab"
DESKTOP = TESTS / "KeyRoom.Desktop"


def capture(executable, environment, folder, mac_input):
    capture = folder / "mix.f32"

    def frames():
        if not capture.exists():
            return 0
        with capture.open("rb") as f:
            return f.seek(0, 2) // 8

    with (folder / "player.log").open("wb") as log:
        proc = subprocess.Popen([str(executable)], cwd=executable.parent, env={**os.environ, **environment},
                                stdout=log, stderr=subprocess.STDOUT)
        def wait_for(check, description, timeout=30):
            deadline = time.monotonic() + timeout
            while time.monotonic() < deadline:
                if proc.poll() is not None:
                    raise RuntimeError(f"AudioLab exited early with status {proc.returncode}")
                if check():
                    return
                time.sleep(.10)
            raise TimeoutError(description)

        try:
            if mac_input:
                def command(verb):
                    return [str(mac_input), verb, str(proc.pid)]
                def focus_game():
                    return subprocess.check_output(command("focus"), text=True).strip()
                def press(key):
                    subprocess.run(command(key), check=True)
                def screenshot(window_id):
                    subprocess.run(["screencapture", "-x", "-o", "-l", window_id, str(folder / "after-f.png")], check=True)
            else:
                sys.path.insert(0, str(DESKTOP))
                from verify_editor import WindowsWindow, X11Window, focus, tap
                window = (WindowsWindow if os.name == "nt" else X11Window)(proc, "AudioLab")
                def focus_game():
                    # SDL may replace its initial window while the renderer starts.
                    wait_for(window.locate, "AudioLab input window")
                    focus(window)
                def press(key):
                    tap(window, key)
                def screenshot(_):
                    window.capture().save(folder / "after-f.png")
            wait_for(lambda: frames() >= 4 * PERIOD + BLOCK * 4, "four music loops")
            window_id = focus_game()
            before = frames()
            (folder / "before-f.txt").write_text(str(before))
            press("f")
            wait_for(lambda: frames() >= before + 24000, "fade after F")
            time.sleep(1)
            screenshot(window_id)
            focus_game()
            press("Escape")
            if proc.wait(timeout=15) != 0:
                raise RuntimeError("AudioLab failed to exit normally")
        finally:
            if proc.poll() is None:
                proc.kill()
                proc.wait()


def baseline_csv(rid):
    """SDL_mixer is native code built per architecture, so its PCM differs in the last bit between arm64 and x64.
    osx-arm64 keeps results/audio-mixer/playback.csv; other RIDs use playback-<rid>.csv (same columns)."""
    name = "playback.csv" if rid == "osx-arm64" else f"playback-{rid}.csv"
    return TESTS / "results/audio-mixer" / name


def compare(folder, baseline):
    mix_path = folder / "mix.f32"
    _, _, metrics = read_capture(mix_path)
    before = int((folder / "before-f.txt").read_text())
    if not before <= metrics["stop_frame"] <= before + 2 * RATE:
        raise ValueError(f"Fade started at {metrics['stop_frame']} frames, F was sent at {before}")
    mix = mix_path.read_bytes()
    device = (folder / "device.f32").read_bytes()
    offset = device.find(mix)
    metrics.update(device_offset_bytes=offset, device_bytes=len(device), frames_before_f=before)
    (folder / "measurements.json").write_text(json.dumps(metrics, indent=2) + "\n")
    for key in ("first_four_loops_sha256", "loop_frames", "fade_in_frames", "fade_out_frames"):
        if str(metrics[key]) != baseline[key]:
            raise ValueError(f"{key}: baseline {baseline[key]}, runner {metrics[key]}")
    # The device can start earlier and drain later. All intervening stereo frames must be contiguous.
    if offset < 0 or offset % 8 or len(device) % 8 or any(device[:offset]) or any(device[offset + len(mix):]):
        raise ValueError("SDL disk output differs from the captured mix or contains an audible discontinuity")
    return metrics


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rid", choices=("win-x64", "linux-x64", "osx-arm64"))
    parser.add_argument("results", type=Path)
    args = parser.parse_args()
    if host_rid() != args.rid:
        parser.error(f"Expected {args.rid}, running on {host_rid()}")
    root = args.results.resolve()
    root.mkdir(parents=True, exist_ok=False)
    if not baseline_csv(args.rid).exists():
        parser.error(f"No PCM baseline for {args.rid}: {baseline_csv(args.rid)}")
    with baseline_csv(args.rid).open() as f:
        mac = {row["mode"]: row for row in csv.DictReader(f) if row["codec"] == "ogg"}
    errors = []
    with tempfile.TemporaryDirectory(prefix="monoon-audio-") as tmp:
        work = Path(tmp).resolve()
        build_env, _ = prepare_tools(work, args.rid)
        mac_input = None
        if args.rid == "osx-arm64":
            mac_input = work / "mac-input"
            subprocess.run(["xcrun", "swiftc", str(Path(__file__).with_name("mac-input.swift")),
                            "-o", str(mac_input)], check=True)
        for mode, reference in (("release", "release"), ("native-aot", "aot")):
            folder = root / mode
            folder.mkdir()
            try:
                product = build_player(work, PROJECT, mode, args.rid, work / "exports", build_env)
                shutil.copyfile(product / "MoNoonBuild", folder / "MoNoonBuild")
                game_assets = product / "Game/Assets"
                if (game_assets / "Tide.ogg").read_bytes() != (PROJECT / "Assets/Tide.ogg").read_bytes() or (game_assets / "Tide.wav").exists():
                    raise ValueError("Expected the imported Tide.ogg in the distribution")
                environment = dict(SDL_AUDIO_DRIVER="disk", SDL_AUDIO_FORMAT="F32LE", SDL_AUDIO_CHANNELS="2",
                                   SDL_AUDIO_FREQUENCY=str(RATE), SDL_AUDIO_DISK_OUTPUT_FILE=str(folder / "device.f32"),
                                   MONOON_AUDIO_CAPTURE=str(folder / "mix.f32"), MONOON_USER_DATA=str(folder / "profile"))
                player = "MoNoonPlayer.exe" if args.rid == "win-x64" else "MoNoonPlayer"
                capture(product / player, environment, folder, mac_input)
                metrics = compare(folder, mac[reference])
                print(f"PASS {args.rid} {mode}: {metrics['loop_frames']} frames per loop, "
                      f"{metrics['fade_in_frames']} in, {metrics['fade_out_frames']} out. "
                      f"PCM SHA-256 {metrics['first_four_loops_sha256']}", flush=True)
            except Exception:
                failure = traceback.format_exc()
                (folder / "failure.txt").write_text(failure)
                print(failure, file=sys.stderr, flush=True)
                errors.append(mode)
    if errors:
        raise SystemExit(f"{args.rid} failed: {', '.join(errors)}")


if __name__ == "__main__":
    main()
