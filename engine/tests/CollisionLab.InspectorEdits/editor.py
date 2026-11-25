"""Inspector edit cost (I-1) on CollisionLab: run one or more packaged editors alternately and report medians.

usage: editor.py <project> <out> <runs> <label>=<editor>,<hook> [<label>=<editor>,<hook> ...]
MONOON_I1_SCENE=Showcase (with samples/KeyRoom as <project>) measures that scene instead of Field10000 and skips the lab checks.
The label "base" runs without the new-editor checks (one undo record per commit); every other label runs them.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

project, out = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
runs = int(sys.argv[3])
scene = os.environ.get("MONOON_I1_SCENE", "Field10000")
editors = {}
for arg in sys.argv[4:]:
    label, paths = arg.split("=", 1)
    editor, hook = paths.split(",", 1)
    editors[label] = (Path(editor).resolve(), Path(hook).resolve())
out.mkdir(parents=True)
results = {label: [] for label in editors}
saved = {}
for i in range(1, runs + 1):
    for label, (editor, hook) in editors.items():
        run = out / f"{label}-{i}"
        run.mkdir()
        copy = run / project.name
        shutil.copytree(project, copy)
        env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_EDITOR_PERF=str(run), MONOON_USER_DATA=str(run / "profile"),
                   MONOON_I1_STRICT="0" if label == "base" else "1")
        with (run / "editor.log").open("wb") as log:
            subprocess.run([str(editor), str(copy)], env=env, stdout=log, stderr=log, timeout=900, check=True)
        if (run / "error.txt").exists():
            raise SystemExit(f"FAIL {label} {i}: " + (run / "error.txt").read_text(encoding="utf-8"))
        # The same typed edits must save the same scene bytes in every editor and run.
        saved[f"{label}-{i}"] = hashlib.sha256((copy / "Scenes" / f"{scene}.scene").read_bytes()).hexdigest()
        shutil.rmtree(copy)
        shutil.rmtree(run / "profile", ignore_errors=True)
        r = json.loads((run / "result.json").read_text(encoding="utf-8"))
        results[label].append(r)
        print(label, i, json.dumps({k: v.get("median_ms", v.get("ms")) if isinstance(v, dict) else v for k, v in r.items()}))

def mid(values):
    values = sorted(values)
    return values[len(values) // 2], values[0], values[-1]

summary = {}
for label, rs in results.items():
    row = {}
    for key in ["number_key", "number_commit", "number_edit", "text_key", "text_commit", "text_edit"]:
        ms, lo, hi = mid(r[key]["median_ms"] for r in rs)
        mb = mid(r[key]["median_bytes"] for r in rs)[0]
        row[key] = {"median_ms": round(ms, 3), "min_ms": round(lo, 3), "max_ms": round(hi, 3), "median_mb": round(mb / 2**20, 2)}
    for key in ["ms", "allocated_bytes", "retained_bytes", "snapshot_bytes", "snapshot_strings", "undo_ms", "undo_bytes", "redo_ms", "redo_bytes"]:
        if any(key not in r["history_100"] for r in rs): continue
        v, lo, hi = mid(r["history_100"][key] for r in rs)
        scale = 2**20 if key.endswith("bytes") else 1
        row["history_100_" + key.replace("_bytes", "_mb")] = {"median": round(v / scale, 1), "min": round(lo / scale, 1), "max": round(hi / scale, 1)}
    row["captures_per_edit"] = sorted({c for r in rs for c in r["number_captures"] + r["text_captures"]})
    summary[label] = row
    for key, v in row.items():
        print("MEDIAN", label, key, v)
if len(set(saved.values())) != 1:
    raise SystemExit(f"FAIL saved {scene} differs: " + json.dumps(saved))
(out / "summary.json").write_text(json.dumps({"runs": results, "summary": summary, "scene": scene, "saved_sha256": saved}, indent=1) + "\n",
                                  encoding="utf-8")
print(f"PASS inspector edit probe: checks held in every run and every run saved the same {scene} bytes", next(iter(saved.values())))
