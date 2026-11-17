"""Exercise slot recovery in a built InventoryLab player on its native OS."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile


def verify(executable, fixtures, destination):
    destination.mkdir(parents=True, exist_ok=False)
    first = (fixtures / "Herbalist.save").read_bytes()
    second = first.replace(b"count0: 3", b"count0: 2").replace(b'"paid"', b'""')
    denied = first.replace(b'"paid"', b'"reward"')
    if second == first or denied == first:
        raise ValueError("Expected the paid InventoryLab fixture with three herbs")

    def check(path, expected):
        if path.read_bytes() != expected:
            raise AssertionError(f"Unexpected {path.name} contents: {path.read_bytes()!r}")

    def run(*args):
        env = dict(os.environ, SDL_VIDEO_DRIVER="monoon-no-video", SDL_AUDIO_DRIVER="monoon-no-audio",
                   MONOON_USER_DATA=str(profile))
        subprocess.run([str(executable), *map(str, args)], cwd=executable.parent, env=env, check=True)

    with tempfile.TemporaryDirectory() as root:
        profile = Path(root) / "profile"
        profile.mkdir()
        slot = profile / "slot-001.save"
        backup = profile / "slot-001.save.bak"
        run("--replay-input", fixtures / "Herbalist.inputreplay", profile)
        check(slot, first)
        run("--smoke-test", profile, "L", "1", "U", "S")
        check(slot, second)
        check(backup, first)
        shutil.copyfile(slot, destination / "second.save")
        shutil.copyfile(backup, destination / "first.save")
        saved = Path(root) / "two-saves"
        shutil.copytree(profile, saved)
        for case, damage in (("truncated", b"version: 3\nvalues: ["), ("edited-count", second.replace(b"count0: 2", b"count0: 1"))):
            profile = Path(root) / case
            shutil.copytree(saved, profile)
            slot, backup = profile / "slot-001.save", profile / "slot-001.save.bak"
            slot.write_bytes(damage)
            run("--smoke-test", profile, "L")
            check(slot, first)
            check(backup, first)
            shutil.copyfile(slot, destination / (case + "-recovered.save"))
            # Force recovery inside the same process as the second claim attempt.
            slot.write_bytes(damage)
            run("--smoke-test", profile, "L", "1", "T", "2", "1", "S")
            check(slot, denied)
            check(backup, first)
            shutil.copyfile(slot, destination / (case + "-after-claim.save"))
            print(f"PASS {case}: claimed, herbs=3, gems=1, repeat claim stayed at reward", flush=True)

    shutil.copyfile(executable.parent / "MoNoonBuild", destination / "build.receipt")
    hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(destination.glob("*.save"))}
    (destination / "result.json").write_text(json.dumps(dict(system=platform.platform(), machine=platform.machine(),
                                                             saves=hashes), indent=2) + "\n")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path)
    parser.add_argument("fixtures", type=Path)
    parser.add_argument("results", type=Path)
    args = parser.parse_args()
    verify(args.executable.resolve(), args.fixtures.resolve(), args.results.resolve())
