"""Roadmap 11.2 in the packaged Mac editor: save a game script during Scene Play, keep field values, keep running on a compile error."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:5]]
out.mkdir(parents=True)
project = out / "KeyRoom"
shutil.copytree(sample, project, ignore=shutil.ignore_patterns("bin", "obj", ".monoon", ".DS_Store"))
shutil.copyfile(HERE / "ReloadProbe.v1.source", project / "Scripts/ReloadProbe.cs")
scene = project / "Scenes/Showcase.scene"   # the start scene
text = scene.read_text(encoding="utf-8")
anchor = "\nobjects:\n"
assert text.count(anchor) == 1
probe = ("- id: 7d1c2e5a-4b3f-4c8e-9a61-2f0e8b7c5d34\n  name: Reload probe\n  width: 1\n  height: 1\n  components:\n"
         "  - type: script\n    script: ReloadProbe\n")
scene.write_text(text.replace(anchor, anchor + probe), encoding="utf-8")
failures = []


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message, flush=True)
    if not ok:
        failures.append(message)


env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_RELOAD_OUTPUT=str(out), MONOON_RELOAD_HARNESS=str(HERE),
           MONOON_USER_DATA=str(out / "profile"))
with (out / "editor.log").open("wb") as log:
    code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
if (out / "error.txt").exists():
    raise SystemExit("FAIL editor: " + (out / "error.txt").read_text(encoding="utf-8"))
r = json.loads((out / "reload.json").read_text(encoding="utf-8"))
v1, v2, broken = r["v1"], r["v2"], r["broken"]
check(code == 0, f"editor exited with {code}")
check(v1["ticks"] >= 30 and v1["routines"] == 1, f"v1 counted {v1['ticks']} ticks with one running routine")
check(v2["replaced"] and v2["new_assembly"], "saving the script replaced the live behaviour with one from a new assembly")
check(v2["ticks_after_reload"] >= v1["ticks"] and v2["version"] == "v1", f"Ticks kept ({v1['ticks']} → {v2['ticks_after_reload']}) and Version kept its live value")
check(v2["delta_one_tick"] == 1000, "the next tick ran the new code (+1000)")
check(v2["label"] == "added in v2" and v2["speed"] == 3, "the new field has its default; Speed (float → int) was reset")
messages = [c["Message"] for c in v2["console"]]
check(any(c["Level"] == "Warning" and "ReloadProbe.Speed" in c["Message"] for c in v2["console"]), "the type change is a Console warning")
others = [c["Message"] for c in v2["console"] if c["Level"] == "Warning" and "ReloadProbe" not in c["Message"]]
check(not others, f"KeyRoom's own behaviours carried every field ({others[:3]})")
check(v2["routines"] == 0 and any(m.startswith("Stopped ") and "running routine" in m for m in messages), "the running routine was stopped with a Console note")
check(broken["File"] and broken["File"].endswith("Scripts/ReloadProbe.cs") and broken["Line"] == 11 and "CS1002" in broken["Message"],
      f"the compile error is a clickable Console row at ReloadProbe.cs:{broken['Line']}")
check(broken["playing"] and broken["same_instance"] and broken["delta_one_tick"] == 1000, "Play kept running the previous (v2) code")
if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print("PASS script reload in", out)
