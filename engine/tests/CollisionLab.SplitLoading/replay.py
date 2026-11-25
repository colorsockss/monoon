"""Load CollisionLab's 10,000-collider field with large backdrops in development players and compare split and one-step loading.

prepare <output project>         copy samples/CollisionLab (with the test-only fields, Shared/collision_lab.py) and add four 4096x4096 backdrop PNGs to Field10000
run <player> <output> [--runs N]  replay add, add+cancel and replace loads with budget 0 (previous) and 4 ms
"""
import argparse
import csv
import json
import os
from pathlib import Path
import shutil
import struct
import sys
import subprocess
import uuid
import zlib

PROJECT = START_SCENE = "5750e5bb-1700-5a5b-94c2-b6bd02a0356c"
FRAME = 166667  # 1/60 s in .NET ticks
PRESS, FRAMES = 2, 300
BACKDROPS, SIZE = 4, 4096
BUDGETS = {"previous": "0", "split": "4"}
SAMPLE = Path(__file__).resolve().parents[3] / "samples" / "CollisionLab"
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Shared"))
import collision_lab  # noqa: E402


def png(path, seed):
    # Rows are rotations of one row: small files whose decode still inflates SIZE*SIZE*4 bytes
    # and runs the Paeth filter (type 4), the most expensive PNG row filter, over every byte.
    row = bytes((x * 255 // SIZE + seed * 40 + c * 60) & 0xFF if c < 3 else 255 for x in range(SIZE) for c in range(4))
    raw = b"".join(b"\4" + row[(y * 52) % len(row):] + row[:(y * 52) % len(row)] for y in range(SIZE))
    chunk = lambda kind, data: struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b""))


def prepare(project):
    collision_lab.prepare(SAMPLE, project)
    lines = []
    for i in range(1, BACKDROPS + 1):
        asset = uuid.uuid5(uuid.NAMESPACE_URL, f"monoon/split-loading/backdrop{i}")
        png(project / "Assets" / f"Backdrop{i}.png", i)
        (project / "Assets" / f"Backdrop{i}.png.meta").write_text(f"id: {asset}\n", encoding="utf-8")
        obj = uuid.uuid5(uuid.NAMESPACE_URL, f"monoon/split-loading/backdrop-object{i}")
        lines.append(f"- {{id: {obj}, name: Backdrop {i}, x: {-1600 + i * 900}, y: -900, width: 800, height: 800, components: "
                     f"[{{type: spriteRenderer, asset: Assets/Backdrop{i}.png, assetId: {asset}}}]}}")
    scene = project / "Scenes" / "Field10000.scene"
    text = scene.read_text(encoding="utf-8")
    # Keep the room's own entry benchmark out of the loading frames: drop the QueryField component.
    query = "  - type: script\n    script: QueryField\n"
    start = text.index(query)
    text = text[:start] + text[text.index("\n", start + len(query)) + 1:]
    at = text.index("\ndefaultCameraId:") + 1  # The object list ends before the scene's camera key.
    scene.write_text(text[:at] + "\n".join(lines) + "\n" + text[at:], encoding="utf-8")
    print(f"PREPARED {project}: Field10000 + {BACKDROPS} backdrops of {SIZE}x{SIZE}")


def recording(path, key, cancel=None):
    lines = ["version: 1", f"project: {PROJECT}", f"startScene: {START_SCENE}", "frameTicks:"]
    lines += [f"- {FRAME}"] * FRAMES
    lines.append("events:")
    for frame, control in [(PRESS, key)] + ([(cancel, "Backspace")] if cancel else []):
        for offset, down in ((0, "true"), (1, "false")):
            lines += [f"- frame: {frame + offset}", f"  timeTicks: {(frame + offset) * FRAME}", "  input:",
                      "    kind: key", f"    control: {control}", f"    down: {down}"]
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def frames(path):
    rows = {}
    with path.open(newline="") as f:
        for r in csv.DictReader(f):
            rows.setdefault(int(r["frame"]), []).append(r)
    return rows


def window(rows):
    """Frames from the key press to the commit (or the last loading frame) with their loading work."""
    def us(frame, name): return sum(float(r["inclusive_us"]) for r in rows[frame] if r["name"] == name)
    loading = [n for n in sorted(rows) if any(r["name"] == "Scene.Loading" for r in rows[n])]
    commit = next((n for n in loading if us(n, "Scene.Commit") > 0), None)
    end = commit if commit is not None else loading[-1]
    span = [n for n in sorted(rows) if PRESS < n <= end]
    frame_ms = [float(rows[n][0]["inclusive_us"]) / 1000 for n in span]
    times = sorted(frame_ms)
    p95 = times[min(len(times) - 1, int(round(.95 * (len(times) - 1))))]
    slowest = max(span, key=lambda n: float(rows[n][0]["inclusive_us"]))
    parts = {name: round(sum(us(n, name) for n in span) / 1000, 3) for name in
             ("Scene.Loading", "Scene.Instantiate", "Scene.Upload", "Scene.Commit")}
    return {"frames": len(span), "first": span[0], "end": end, "committed": commit is not None,
            # Paced frames: a frame lasts its 1/60 s slot unless its work takes longer.
            "total_ms": round(sum(max(t, 1000 / 60) for t in frame_ms), 3), "work_total_ms": round(sum(frame_ms), 3), "max_ms": round(times[-1], 3), "p95_ms": round(p95, 3),
            "slowest_frame": slowest, "slowest_gen0": int(rows[slowest][0]["gen0"]), "slowest_tree": [(r["name"], round(float(r["inclusive_us"]) / 1000, 3)) for r in rows[slowest]
                                                        if int(r["depth"]) <= 3 and float(r["inclusive_us"]) >= 500],
            "work_ms": parts, "upload_frames": [n for n in span if us(n, "Scene.Upload") > 0]}


def replay(player, out, name, key, budget, cancel=None):
    record, capture, profile = out / f"{name}.inputreplay", out / f"{name}.csv", out / f"{name}-profile"
    recording(record, key, cancel)
    env = dict(os.environ, SDL_VIDEO_DRIVER="monoon-no-video", SDL_AUDIO_DRIVER="monoon-no-audio", DOTNET_TieredCompilation="0",
               MONOON_USER_DATA=str(profile), MONOON_FRAME_PROFILE=str(capture), MONOON_REPLAY_OFFSCREEN="1",
               MONOON_SCENE_LOAD_BUDGET_MS=budget)
    run = subprocess.run([str(player), "--replay-input", str(record), str(profile)], env=env, capture_output=True, text=True)
    log = run.stdout + run.stderr
    (out / f"{name}.log").write_text(log, encoding="utf-8")
    if run.returncode != 0:
        raise SystemExit(f"FAIL {name}: exit {run.returncode}\n{log}")
    counts = {line.split(":")[0].rsplit(" ", 1)[-1]: dict(p.split("=") for p in line.split(": ", 1)[1].split())
              for line in log.splitlines() if line.startswith("Replay resources at ")}
    result = window(frames(capture)) | {"resources": counts}
    capture.unlink()  # Keep the summary; a 900-frame CSV is several hundred KB.
    shutil.rmtree(profile, ignore_errors=True)
    record.unlink()
    return result


def run(player, out, runs):
    out.mkdir(parents=True, exist_ok=False)
    results = {}
    for i in range(1, runs + 1):
        for path, budget in BUDGETS.items():
            for scenario, key in (("add", "4"), ("replace", "3")):
                r = replay(player, out, f"{scenario}-{path}-{i}", key, budget)
                results.setdefault(f"{scenario}/{path}", []).append(r)
                print(f"{scenario:7} {path:8} run {i}: {r['frames']:4} frames  total {r['total_ms']:9.3f} ms  max {r['max_ms']:8.3f} ms"
                      f"  p95 {r['p95_ms']:7.3f} ms  committed={r['committed']}  {r['resources']}")
            # Cancel between the first and last upload of the matching complete load: objects exist, some files are
            # uploaded and the rest decode. Timing differs between runs, so a cancel that came too late is retried earlier.
            uploads = results[f"add/{path}"][-1]["upload_frames"]
            cancel = (uploads[0] + uploads[-1]) // 2 if uploads else results[f"add/{path}"][-1]["end"] - 2
            for attempt in range(3):
                r = replay(player, out, f"cancel-{path}-{i}", "4", budget, cancel)
                if not r["committed"]:
                    break
                cancel -= 2
            r["cancel_frame"], r["attempts"] = cancel, attempt + 1
            results.setdefault(f"cancel/{path}", []).append(r)
            start, end = r["resources"].get("start"), r["resources"].get("end")
            print(f"cancel  {path:8} run {i}: frame {cancel}, committed={r['committed']}, start {start}, end {end}")
            if r["committed"] or start is None or end != start:
                raise SystemExit(f"FAIL: canceling at frame {cancel} did not return to the starting resources")
    median = lambda xs: sorted(xs)[len(xs) // 2]
    table = {k: {m: median([r[m] for r in v]) for m in ("frames", "total_ms", "max_ms", "p95_ms")} for k, v in results.items()}
    (out / "summary.json").write_text(json.dumps({"medians": table, "runs": results}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    for k, v in table.items():
        print(f"MEDIAN {k:16} {v}")
    for scenario in ("add", "replace"):
        prev, split = table[f"{scenario}/previous"], table[f"{scenario}/split"]
        if not all(r["committed"] for r in results[f"{scenario}/split"] + results[f"{scenario}/previous"]):
            raise SystemExit(f"FAIL: a {scenario} load did not finish within {FRAMES} frames")
        if split["max_ms"] >= prev["max_ms"]:
            raise SystemExit(f"FAIL: split {scenario} loading did not lower the longest frame")
    print("PASS split loading lowers the longest loading frame; canceled loads return to the starting resources")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("prepare").add_argument("project", type=Path)
    r = sub.add_parser("run")
    r.add_argument("player", type=Path, help="MoNoonPlayer of a --profile development build of the prepared project")
    r.add_argument("output", type=Path)
    r.add_argument("--runs", type=int, default=3)
    args = parser.parse_args()
    if args.command == "prepare":
        prepare(args.project.resolve())
    else:
        run(args.player.resolve(), args.output.resolve(), args.runs)


if __name__ == "__main__":
    main()
