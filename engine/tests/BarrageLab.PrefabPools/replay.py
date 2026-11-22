"""Replay BarrageLab's prefab bullets in pooled and Instantiate/Destroy mode in a built player."""
import argparse
import csv
import json
import math
import os
from pathlib import Path
import re
import subprocess

PROJECT = "6c5dd0cd-638e-5a00-9d80-744596f3a647"
START_SCENE = "719a1aa5-015b-503e-85ad-0722931cc1c0"
FRAME = 166667  # 1/60 s in .NET ticks
OPEN, BEGIN, TICKS, WARMUP = 2, 30, 1800, 600
REPORT = re.compile(r"mode=(\w+) ticks=(\d+) fired=(\d+) hits=(\d+) exits=(\d+) active=(\d+) created=(\d+) reused=(\d+) returned=(\d+)")


def recording(path, mode_key):
    # N opens the bullet scene; R (pool) or P (instantiate) starts both runs on the same frame.
    presses = [(OPEN, "N"), (BEGIN, mode_key), (BEGIN + TICKS, "R")]
    lines = ["version: 1", f"project: {PROJECT}", f"startScene: {START_SCENE}", "frameTicks:"]
    lines += [f"- {FRAME}"] * (BEGIN + TICKS + 4)
    lines.append("events:")
    for frame, key in presses:
        for offset, down in ((0, "true"), (1, "false")):
            lines += [f"- frame: {frame + offset}", f"  timeTicks: {(frame + offset) * FRAME}", "  input:",
                      "    kind: key", f"    control: {key}", f"    down: {down}"]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def percentile(values, p):
    ordered = sorted(values)
    return ordered[min(len(ordered) - 1, math.ceil(p * len(ordered)) - 1)]


def run(player, label, mode_key, output):
    replay, capture, profile = output / f"{label}.inputreplay", output / f"{label}.csv", output / f"{label}-profile"
    recording(replay, mode_key)
    env = dict(os.environ, SDL_VIDEO_DRIVER="monoon-no-video", SDL_AUDIO_DRIVER="monoon-no-audio",
               MONOON_USER_DATA=str(profile), MONOON_REPLAY_CAPTURE=str(capture))
    out = subprocess.run([str(player), "--replay-input", str(replay), str(profile)], env=env,
                         capture_output=True, text=True, check=True).stdout
    (output / f"{label}.log").write_text(out, encoding="utf-8")
    report = REPORT.findall(out)[-1]
    keys = ("mode", "ticks", "fired", "hits", "exits", "active", "created", "reused", "returned")
    result = {k: (v if k == "mode" else int(v)) for k, v in zip(keys, report)}
    with capture.open(newline="") as f:
        rows = [r for r in csv.DictReader(f)]
    # Steady state: the frames after the warm-up and before the final report.
    window = [r for r in rows if BEGIN + 1 + WARMUP <= int(r["frame"]) < BEGIN + TICKS]
    times = [float(r["update_us"]) for r in window]
    allocations = [int(r["update_bytes"]) for r in window]
    result.update(frames=len(window), mean_us=round(sum(times) / len(times), 1), p95_us=percentile(times, .95),
                  max_us=max(times), bytes_per_frame=round(sum(allocations) / len(allocations)),
                  bytes_total=sum(allocations),
                  gen0=int(window[-1]["gen0_collections"]) - int(window[0]["gen0_collections"]))
    print(label, json.dumps(result, ensure_ascii=False), flush=True)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("player", type=Path, help="MoNoonPlayer executable of a BarrageLab build")
    parser.add_argument("output", type=Path, help="New directory for recordings, CSV captures and logs")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    pool = run(args.player.resolve(), "pool", "R", args.output)
    fresh = run(args.player.resolve(), "instantiate", "P", args.output)
    same = all(pool[k] == fresh[k] for k in ("ticks", "fired", "hits", "exits", "active"))
    (args.output / "summary.json").write_text(json.dumps({"pool": pool, "instantiate": fresh}, ensure_ascii=False, indent=2) + "\n")
    if not same or pool["mode"] != "pool" or fresh["mode"] != "instantiate" or pool["ticks"] != TICKS or pool["hits"] != pool["exits"]:
        raise SystemExit("FAIL: pooled and Instantiate/Destroy runs differ")
    print(f"PASS same {pool['hits']} hits from {pool['fired']} bullets; reused {pool['reused']} of {pool['fired']}", flush=True)


if __name__ == "__main__":
    main()
