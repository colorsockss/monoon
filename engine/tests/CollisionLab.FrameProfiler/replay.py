"""Replay CollisionLab's 10,000-collider query field in a development player and read its frame profile."""
import argparse
import csv
import json
import os
from pathlib import Path
import subprocess

PROJECT = START_SCENE = "5750e5bb-1700-5a5b-94c2-b6bd02a0356c"
FRAME = 166667  # 1/60 s in .NET ticks
OPEN, REPEAT, FRAMES = 2, 90, 150
PER_KIND = 96 * 14  # QueryField: 96 paths x (2 reference runs + 6 passes x 2 modes)
QUERIES = ("Physics.Overlap", "Physics.Raycast", "Physics.Sweep")


def recording(path):
    # 2 opens Field10000, whose QueryField measures once on entry; R repeats the measurement.
    lines = ["version: 1", f"project: {PROJECT}", f"startScene: {START_SCENE}", "frameTicks:"]
    lines += [f"- {FRAME}"] * FRAMES
    lines.append("events:")
    for frame, key in ((OPEN, "2"), (REPEAT, "R")):
        for offset, down in ((0, "true"), (1, "false")):
            lines += [f"- frame: {frame + offset}", f"  timeTicks: {(frame + offset) * FRAME}", "  input:",
                      "    kind: key", f"    control: {key}", f"    down: {down}"]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def read_frames(path):
    frames = {}
    with path.open(newline="") as f:
        for row in csv.DictReader(f):
            frames.setdefault(int(row["frame"]), []).append(row)
    return frames


def summarize(rows):
    root = rows[0]
    by_name = {}
    for r in rows:
        calls, us = by_name.get(r["name"], (0, 0.0))
        by_name[r["name"]] = (calls + int(r["calls"]), us + float(r["inclusive_us"]))
    frame_us = float(root["inclusive_us"])
    query_us = sum(by_name.get(q, (0, 0))[1] for q in QUERIES)
    return {"frame": int(root["frame"]), "frame_ms": round(frame_us / 1000, 3),
            "query_ms": round(query_us / 1000, 3), "query_share": round(query_us / frame_us, 4),
            "calls": {q: by_name.get(q, (0, 0))[0] for q in QUERIES},
            "script_ms": round(by_name.get("QueryField.FixedUpdate", (0, 0))[1] / 1000, 3),
            "loading_ms": round(by_name.get("Scene.Loading", (0, 0))[1] / 1000, 3),
            "alloc_bytes": int(root["alloc_bytes"]), "gen0": int(root["gen0"]), "dropped": int(root["dropped"]),
            "tree": [(int(r["depth"]), r["name"], int(r["calls"]), float(r["inclusive_us"]), float(r["self_us"])) for r in rows]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("development", type=Path, help="MoNoonPlayer of a --profile development CollisionLab build")
    parser.add_argument("distribution", type=Path, help="MoNoonPlayer of a --profile distribution CollisionLab build")
    parser.add_argument("output", type=Path, help="New directory for the recording, capture and logs")
    args = parser.parse_args()
    out = args.output
    out.mkdir(parents=True, exist_ok=False)
    replay = out / "Field10000.inputreplay"
    recording(replay)
    results = {}
    for label, player in (("development", args.development), ("distribution", args.distribution)):
        capture, profile = out / f"{label}.csv", out / f"{label}-profile"
        env = dict(os.environ, SDL_VIDEO_DRIVER="monoon-no-video", SDL_AUDIO_DRIVER="monoon-no-audio",
                   DOTNET_TieredCompilation="0", MONOON_USER_DATA=str(profile), MONOON_FRAME_PROFILE=str(capture))
        run = subprocess.run([str(player.resolve()), "--replay-input", str(replay), str(profile)], env=env,
                             capture_output=True, text=True, check=True)
        (out / f"{label}.log").write_text(run.stdout + run.stderr, encoding="utf-8")
        # Timings differ between runs; the target-order hash and candidate counts must not.
        spatial = sorted(" ".join(f for f in l.split() if "P95" not in f) for l in run.stdout.splitlines() if l.startswith("SPATIAL "))
        results[label] = {"captured": capture.exists(), "spatial": spatial}

    dist = results["distribution"]
    if dist["captured"] or "frame profiling needs a development-profile build" not in (out / "distribution.log").read_text():
        raise SystemExit("FAIL: the distribution build wrote a frame profile")
    dev = results["development"]
    # Each measurement logs 4 lines with the same target-order hash; both builds must agree.
    if len(dev["spatial"]) != 8 or dev["spatial"] != dist["spatial"]:
        raise SystemExit("FAIL: development and distribution query results differ")

    frames = read_frames(out / "development.csv")
    if sorted(frames) != list(range(1, FRAMES + 1)):
        raise SystemExit(f"FAIL: expected frames 1..{FRAMES}, got {len(frames)}")
    summaries = {n: summarize(rows) for n, rows in frames.items()}
    heavy = [s for s in summaries.values() if s["calls"] == {q: PER_KIND for q in QUERIES}]
    # The start scene's Probe casts 3 overlaps, 1 ray and 1 sweep once; nothing else queries.
    light = [s for s in summaries.values() if any(s["calls"].values()) and s not in heavy]
    # Like a user who presses R and clicks the spike that follows; entering the field is a separate loading spike.
    slowest = max((s for n, s in summaries.items() if n > REPEAT), key=lambda s: s["frame_ms"])
    loading = max(summaries.values(), key=lambda s: s["loading_ms"])
    times = sorted(s["frame_ms"] for s in summaries.values())
    report = {"frames": len(frames), "median_ms": times[len(times) // 2], "slowest_after_r": slowest,
              "loading": {k: v for k, v in loading.items() if k != "tree"},
              "query_frames": [{k: v for k, v in s.items() if k != "tree"} for s in light + heavy],
              "distribution_capture": dist["captured"]}
    (out / "summary.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    for s in light + heavy:
        print(json.dumps({k: v for k, v in s.items() if k != "tree"}, ensure_ascii=False))
    print("loading", json.dumps({k: v for k, v in loading.items() if k != "tree"}))
    print("slowest after R", slowest["frame"], f"{slowest['frame_ms']} ms")
    for depth, name, calls, total, self in slowest["tree"]:
        print(f"{calls:9,} {total / 1000:10.3f} {self / 1000:10.3f}  {'  ' * depth}{name}")
    if len(heavy) != 2 or [s["calls"] for s in light] != [dict(zip(QUERIES, (3, 1, 1)))]:
        raise SystemExit(f"FAIL: expected two frames with {PER_KIND} queries of each kind and the lab probe's 5 queries")
    if slowest is not heavy[1] or any(s["query_share"] < .5 for s in heavy):
        raise SystemExit("FAIL: physics queries do not dominate the query frames")
    print(f"PASS slowest frame after R {slowest['frame']}: {3 * PER_KIND} queries take {slowest['query_share']:.1%} of {slowest['frame_ms']} ms;"
          f" entry measurement frame {heavy[0]['frame']}: {heavy[0]['query_share']:.1%} of {heavy[0]['frame_ms']} ms;"
          f" scene loading frame {loading['frame']}: {loading['loading_ms']} of {loading['frame_ms']} ms; distribution build wrote no capture")


if __name__ == "__main__":
    main()
