"""Scene Play: profile CollisionLab's 10,000-collider query field in the packaged editor and check the selected frame."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

PER_KIND = 96 * 14
QUERIES = ("Physics.Overlap", "Physics.Raycast", "Physics.Sweep")

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
project = out / "CollisionLab"
shutil.copytree(sample, project)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_PROFILER_EDITOR=str(out), MONOON_USER_DATA=str(out / "profile"))
with (out / "editor.log").open("wb") as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=300 * float(os.environ.get('MONOON_QA_SLOW') or 1), check=True)
if (out / "error.txt").exists():
    raise SystemExit((out / "error.txt").read_text(encoding="utf-8"))
result = json.loads((out / "result.json").read_text(encoding="utf-8"))
heavy = [f for f in result["query_frames"] if f["calls"] == {q: PER_KIND for q in QUERIES}]
for f in result["query_frames"]:
    print(json.dumps(f))
print(result["title"])
for row in result["table"]:
    print(f"{row[1]:>9} {row[2]:>10} {row[3]:>10}  {row[0]}")
slowest = next((f for f in heavy if f["frame"] == result["slowest"]), None)
if not heavy or slowest is None:
    raise SystemExit(f"FAIL: the slowest frame {result['slowest']} is not a frame with {PER_KIND} queries of each kind")
share = slowest["query_ms"] / slowest["frame_ms"]
if share < .5:
    raise SystemExit(f"FAIL: queries take only {share:.1%} of the slowest frame")
print("load frame", json.dumps(result["load"]))
print(f"PASS slowest Scene Play frame after R {slowest['frame']}: {3 * PER_KIND} queries take {share:.1%} of {slowest['frame_ms']:.1f} ms")
