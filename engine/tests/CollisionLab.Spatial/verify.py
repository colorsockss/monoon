"""Run a published CollisionLab player or verify its captured --smoke-test log."""
import argparse
import math
import os
from pathlib import Path
import subprocess
import tempfile

# SHA-256 of the ordered target IDs for each of the 288 full-scan query answers.
EXPECTED = {
    1000: "32FA7AB7170DF5D060A9FC399E6B1BCF6F274A48852DEEF17AA19B8CA1FEFB63",
    10000: "B5C817DB18262EA1E4C48008C24F77B416FA20AC1ED8447702902803F7D8B896",
}
KINDS = ("Overlap", "Raycast", "Sweep")


def verify(log):
    rows = {}
    for line in log.splitlines():
        if not line.startswith("SPATIAL "):
            continue
        fields = dict(pair.split("=", 1) for pair in line.split()[1:])
        count, kind = int(fields["count"]), fields["kind"]
        if fields["sha256"] != EXPECTED[count] or (count, kind) in rows:
            raise ValueError(f"{count} {kind}: changed target order or duplicate measurement")
        n = int(fields["n"])
        if n != (1728 if kind == "All" else 576):
            raise ValueError(f"{count} {kind}: incomplete samples")
        linear, indexed = int(fields["linearCandidates"]), int(fields["gridCandidates"])
        if not 0 < indexed < linear <= count * n:
            raise ValueError(f"{count} {kind}: invalid candidate counts")
        for key in ("linearP95us", "gridP95us"):
            value = float(fields[key])
            if not math.isfinite(value) or value <= 0:
                raise ValueError(f"{count} {kind}: invalid {key}")
        rows[count, kind] = (linear, indexed)
    if set(rows) != {(count, kind) for count in EXPECTED for kind in (*KINDS, "All")}:
        raise ValueError("Missing or unexpected scene/query measurements")
    for count in EXPECTED:
        total = tuple(map(sum, zip(*(rows[count, kind] for kind in KINDS))))
        if rows[count, "All"] != total:
            raise ValueError(f"{count}: per-query counts disagree with total")
    if "PASS player smoke:" not in log:
        raise ValueError("Player did not complete the run")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--player", type=Path)
    source.add_argument("--log", type=Path)
    parser.add_argument("--output", type=Path, help="write the raw player log")
    args = parser.parse_args()
    if args.player:
        with tempfile.TemporaryDirectory(prefix="collision-lab-") as temp:
            env = dict(os.environ, DOTNET_TieredCompilation="0", MONOON_USER_DATA=temp)
            run = subprocess.run([str(args.player.resolve()), "--smoke-test", temp, "1", "2"],
                                 env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
            log = run.stdout
    else:
        log = args.log.read_text(encoding="utf-8")
    print(log, end="")
    if args.output:
        args.output.write_text(log, encoding="utf-8")
    if args.player:
        run.check_returncode()
    verify(log)


if __name__ == "__main__":
    main()
