"""Dock layout regression in the packaged Mac editor: move/split/tab panels, splitters, restart restore, fallback, Scene Play."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:5]]
phases = sys.argv[5:] or ["first", "restore", "fallback", "panels", "panels-restore"]
out.mkdir(parents=True, exist_ok=phases[0] != "first")
project = out / "KeyRoom"
if not project.exists():
    shutil.copytree(sample, project)
profile = out / "profile"


def launch(phase, user_data):
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_DOCKING_OUTPUT=str(out),
               MONOON_DOCKING_PHASE=phase, MONOON_USER_DATA=str(user_data))
    with (out / f"{phase}-editor.log").open("wb") as log:
        code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
    if (out / f"{phase}-error.txt").exists():
        raise SystemExit(f"FAIL {phase}: " + (out / f"{phase}-error.txt").read_text(encoding="utf-8"))
    if code != 0 or not (out / f"{phase}.json").exists():
        raise SystemExit(f"FAIL {phase}: editor exited with {code}; see {phase}-editor.log")
    result = json.loads((out / f"{phase}.json").read_text(encoding="utf-8"))
    print(f"PASS {phase}")
    return result


for phase in phases:
    if phase == "fallback":
        # A layout and window saved on a bigger, now missing display.
        bad = out / "fallback-profile"
        bad.mkdir(exist_ok=True)
        saved = json.loads((profile / "editor-layout.json").read_text(encoding="utf-8"))
        saved["Layout"]["Children"][0]["Size"] = 40000
        saved["Custom"] = saved["Layout"]
        saved.update(X=-60000, Y=-60000, Width=9000, Height=7000)
        (bad / "editor-layout.json").write_text(json.dumps(saved, indent=2), encoding="utf-8")
        launch(phase, bad)
    elif phase == "panels-restore":
        # The Console window as saved, plus an Animator window saved on a monitor that no longer exists.
        path = out / "panels-profile" / "editor-layout.json"
        saved = json.loads(path.read_text(encoding="utf-8"))
        saved["Floating"].append({"Panel": "Animator", "X": -60000, "Y": -60000, "Width": 640, "Height": 420})
        path.write_text(json.dumps(saved, indent=2), encoding="utf-8")
        result = launch(phase, out / "panels-profile")
        print("  Console", result["console_restored"], "Animator fallback", result["animator_fallback"])
    else:
        result = launch(phase, out / ("panels-profile" if phase == "panels" else "profile"))
    if phase == "first":
        for row in result["baselines"]:
            print(f"  Inspector {row['label']:>10} window {row['window']:>6} width {row['inspector']}: label x {row['label_x']} input x {row['input_x']}"
                  f" column {row['column']} drift {row['drift']} vertical {row['vertical']}")
        print("  splitter", result["splitter"])
        for row in result["play"]:
            print("  Play", row)
    if phase == "panels":
        for row in result["opened"]:
            print("  panel", row)
        for row in result["floats"]:
            print("  float", row)
        for row in result["game"]["running"]:
            print("  Play", row)
        print("  tools", result["tools"])
if (profile / "editor-layout.json").exists():
    print("saved layout:", json.dumps(json.loads((profile / "editor-layout.json").read_text(encoding="utf-8"))["Layout"]))
