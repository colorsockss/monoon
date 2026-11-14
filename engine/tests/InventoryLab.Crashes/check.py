"""Exercise both exported macOS players and resolve the captured throw using their archived symbols."""
from pathlib import Path
import os
import re
import shutil
import subprocess
import sys
import tempfile

repo = Path(__file__).resolve().parents[3]
builds = Path(sys.argv[1]).resolve()
fixture = repo / "engine/tests/fixtures/InventoryLab/invalid-reward.mod"
script = "Scripts/Quests/QuestJournal.cs"
source = (repo / "samples/InventoryLab" / script).read_text().splitlines()
line = next(i for i, text in enumerate(source, 1) if "throw new InvalidDataException" in text and "positive integer {field}" in text)
expected = f"{script}:{line}"
cli = [repo / "monoon", "crash", "explain"]


def run(args, env=None, code=0):
    result = subprocess.run(args, text=True, capture_output=True, env=env, cwd=repo, timeout=90)
    if result.returncode != code:
        raise RuntimeError(f"{args} exited {result.returncode}\n{result.stdout}{result.stderr}")
    return result.stdout + result.stderr


with tempfile.TemporaryDirectory(prefix="monoon-crash-check-") as scratch:
    pairs = []
    for mode in ("release", "native-aot"):
        app = builds / ("NativeAOT" if mode == "native-aot" else "") / "InventoryLab.app"
        bin_dir = app / "Contents/MacOS"
        player = bin_dir / "MoNoonPlayer"
        build_id = re.search(r'buildId: "([a-f0-9]{32})"', (bin_dir / "MoNoonBuild").read_text()).group(1)
        archive = builds / "Symbols/InventoryLab/osx-arm64" / mode / build_id
        profile = Path(scratch) / mode
        env = dict(os.environ, MONOON_USER_DATA=str(profile))
        game = profile / "Games/ade09cc3a76956d79832a2bea14c7e71"
        mods = game / "Mods"
        mods.mkdir(parents=True)
        shutil.copyfile(fixture, mods / fixture.name)
        run([player, "--mods", "on", fixture.stem], env)
        output = run([player], env, code=1)
        reports = list((game / "Crashes").glob("*.crash"))
        assert len(reports) == 1 and f"Crash report: {reports[0]}" in output, output
        explained = run([*cli, reports[0], archive])
        cause = "System.IO.InvalidDataException: Quest 'herbalist' needs a positive integer rewardCount."
        assert expected in explained.split(cause, 1)[1].splitlines()[1], explained
        print(f"PASS {mode} {build_id}: {expected}")
        pairs.append((reports[0], archive))

        run([player, "--mods", "off", fixture.stem], env)
        slot = profile / "replay"
        run([player, "--replay-input", fixture.parent / "Herbalist.inputreplay", slot], env)
        assert (slot / "slot-001.save").read_bytes() == (fixture.parent / "Herbalist.save").read_bytes()
        assert list((game / "Crashes").glob("*.crash")) == reports

    for report, wrong_archive in ((pairs[0][0], pairs[1][1]), (pairs[1][0], pairs[0][1])):
        assert "do not match" in run([*cli, report, wrong_archive], code=1)
    print("PASS mismatched archives rejected and both players completed the quest after removing the fault.")
