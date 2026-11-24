"""Scene Play: add and cancel CollisionLab's prepared heavy field in the packaged editor with budget 0 and 4 ms."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

editor, project, hook, out = [Path(p).resolve() for p in sys.argv[1:5]]
runs = int(sys.argv[5]) if len(sys.argv) > 5 else 3
out.mkdir(parents=True)
summary = {}
for i in range(1, runs + 1):
    for path, budget in (("previous", "0"), ("split", "4")):
        run = out / f"{path}-{i}"
        run.mkdir()
        copy = run / "CollisionLab"
        shutil.copytree(project, copy)
        env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_SPLIT_LOADING_EDITOR=str(run),
                   MONOON_USER_DATA=str(run / "profile"), MONOON_SCENE_LOAD_BUDGET_MS=budget)
        with (run / "editor.log").open("wb") as log:
            subprocess.run([str(editor), str(copy)], env=env, stdout=log, stderr=log, timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
        shutil.rmtree(copy)
        shutil.rmtree(run / "profile", ignore_errors=True)
        if (run / "error.txt").exists():
            raise SystemExit(f"FAIL {path} {i}: " + (run / "error.txt").read_text(encoding="utf-8"))
        r = json.loads((run / "result.json").read_text(encoding="utf-8"))
        a = r["add"]
        summary.setdefault(path, []).append({k: a["loading"][k] for k in ("frames", "total_ms", "max_ms", "p95_ms")}
                                            | {"commit_ms": a["commit"]["max_ms"], "cancel": r["cancel"]["end"] == r["cancel"]["start"]})
        print(path, i, json.dumps(summary[path][-1]), "after cancel", r["cancel"]["end"])
(out / "summary.json").write_text(json.dumps(summary, indent=1) + "\n", encoding="utf-8")
median = lambda xs: sorted(xs)[len(xs) // 2]
for path, rs in summary.items():
    print("MEDIAN", path, {k: median([r[k] for r in rs]) for k in ("frames", "total_ms", "max_ms", "p95_ms", "commit_ms")})
if not all(r["cancel"] for rs in summary.values() for r in rs):
    raise SystemExit("FAIL: a canceled load left resources behind")
print("PASS editor Scene Play: canceled loads return to the starting resources")
