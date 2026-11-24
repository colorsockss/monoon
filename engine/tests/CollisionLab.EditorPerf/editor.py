"""Editor cost on CollisionLab's Field10000: run one or more packaged editors alternately and report medians.

usage: editor.py <project> <out> <runs> <label>=<editor>,<hook> [<label>=<editor>,<hook> ...]
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

project, out = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
runs = int(sys.argv[3])
editors = {}
for arg in sys.argv[4:]:
    label, paths = arg.split("=", 1)
    editor, hook = paths.split(",", 1)
    editors[label] = (Path(editor).resolve(), Path(hook).resolve())
out.mkdir(parents=True)
results = {label: [] for label in editors}
for i in range(1, runs + 1):
    for label, (editor, hook) in editors.items():
        run = out / f"{label}-{i}"
        run.mkdir()
        copy = run / "CollisionLab"
        shutil.copytree(project, copy)
        env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_EDITOR_PERF=str(run), MONOON_USER_DATA=str(run / "profile"))
        with (run / "editor.log").open("wb") as log:
            subprocess.run([str(editor), str(copy)], env=env, stdout=log, stderr=log, timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
        shutil.rmtree(copy)
        shutil.rmtree(run / "profile", ignore_errors=True)
        if (run / "error.txt").exists():
            raise SystemExit(f"FAIL {label} {i}: " + (run / "error.txt").read_text(encoding="utf-8"))
        r = json.loads((run / "result.json").read_text(encoding="utf-8"))
        results[label].append(r)
        print(label, i, json.dumps({k: v.get("median_ms", v.get("ms")) if isinstance(v, dict) else v for k, v in r.items()}))

KEYS = ["edit_frame", "world_items", "edit_items", "edit_rebuild", "edit_rebuild_layout", "play_enter", "toggle", "toggle_layout",
        "play_rebuild", "play_rebuild_layout"]
summary = {}
for label, rs in results.items():
    row = {}
    for key in KEYS:
        ms = sorted(r[key].get("median_ms", r[key].get("ms")) for r in rs)
        mb = sorted(r[key].get("median_bytes", r[key].get("bytes")) for r in rs)
        row[key] = {"median_ms": ms[len(ms) // 2], "min_ms": ms[0], "max_ms": ms[-1], "median_mb": round(mb[len(mb) // 2] / 2**20, 2)}
    row["play_enter_layout"] = {"median_ms": sorted(r["play_enter"]["layout_ms"] for r in rs)[len(rs) // 2]}
    row["toggle_reused_rows"] = all(r["toggle_reused_rows"] for r in rs)
    summary[label] = row
    for key, v in row.items():
        print("MEDIAN", label, key, v)
(out / "summary.json").write_text(json.dumps({"runs": results, "summary": summary}, indent=1) + "\n", encoding="utf-8")
print("PASS editor perf probe: selection, expansion and row opacity checks held in every run")
