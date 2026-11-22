"""Prepare a disposable KeyRoom and compare fixed-tick component values with the authored keys."""
from pathlib import Path
import argparse
import json
import os
import shutil
import subprocess
import sys

HERE = Path(__file__).resolve().parent
EXPECTED = {
    "lid": [(0,1,"FFEBB854"),(-12,1.2,"FFEBB854"),(-24,1.4,"FFEBB854"),(-12,1.2,"FFEBB854"),(0,1,"FFEBB854")],
    "coin": [(0,1,"FFFFFFFF"),(0,1,"999FE1CF"),(0,1,"333FC39F"),(0,1,"999FE1CF"),(0,1,"FFFFFFFF")],
}


def prepare(sample, dest):
    shutil.copytree(sample, dest, ignore=shutil.ignore_patterns(".monoon", ".DS_Store", "bin", "obj"))
    shutil.copyfile(HERE/"PoseObserver.source", dest/"Scripts/PoseObserver.cs")
    for file, label in [(dest/"Scenes/KeyRoom.scene","lid"),(dest/"Assets/Coin.prefab","coin")]:
        s = file.read_text(encoding="utf-8")
        start = s.index("  name: Chest Lid\n") if label == "lid" else s.index("  name: Coin\n")
        pos = s.index("  components:\n", start) + len("  components:\n")
        file.write_text(s[:pos] + f'  - type: script\n    script: PoseObserver\n    values:\n      Label: "{label}"\n' + s[pos:],encoding="utf-8")


def check(path):
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        label, tick, rotation, sx, sy, rgba = line.split()
        key = (label,int(tick))
        assert key not in rows, key
        rows[key] = [float(rotation),float(sx),float(sy),rgba]
    assert len(rows) == 10, rows
    for label, points in EXPECTED.items():
        for i,(angle,sy,rgba) in enumerate(points):
            got = rows[label,i*15]
            assert got == [angle,1,sy,rgba], (label,i*15,got,(angle,1,sy,rgba))
    return [{"object":k[0], "tick":k[1], "rotation":v[0], "scaleX":v[1], "scaleY":v[2], "argb":v[3]} for k,v in sorted(rows.items())]


def play(exe, output):
    output.mkdir(parents=True, exist_ok=False)
    report = output/"values.tsv"
    env = dict(os.environ, MONOON_POSE_REPORT=str(report.resolve()), MONOON_USER_DATA=str((output/"user").resolve()))
    subprocess.run([str(exe.resolve()),"--replay-input",str(HERE/"Compare.inputreplay"),str((output/"slot").resolve())],env=env,check=True)
    result = check(report)
    (output/"result.json").write_text(json.dumps(result,indent=2)+"\n")


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("action",choices=["prepare","play","check"])
    cli.add_argument("paths",nargs="+",type=Path)
    args = cli.parse_args()
    {"prepare":prepare,"play":play,"check":check}[args.action](*args.paths)
