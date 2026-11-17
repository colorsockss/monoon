"""Build and replay both InventoryLab players on the current desktop OS."""
import argparse
import hashlib
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tempfile
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from desktop_players import ENGINE, REPO, build_player, host_rid, prepare_tools, run
FIXTURE = ENGINE / "tests" / "fixtures" / "InventoryLab"
V31_COMMIT = "6232209b5ce4a2c7516db0c8fd989ba63a8e77c5"
V2_SLOT = ENGINE / "tests/results/inventorylab-upgrade-x64/v2-input.save"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def project_files(project):
    return {p.relative_to(project): sha(p.read_bytes()) for p in project.rglob("*") if p.is_file()}


def migrate_v31(work, flags, env):
    marker = "engine/src/Engine.Player/MoNoonContent.version"
    old_version = subprocess.check_output(["git", "show", f"{V31_COMMIT}:{marker}"], cwd=REPO)
    if old_version.strip() != b"31":
        raise ValueError(f"{V31_COMMIT} is not the v31 source revision")
    archive = work / "v31.zip"
    run("git", "-c", "core.autocrlf=false", "archive", "--format=zip", "-o", archive,
        V31_COMMIT, "samples/InventoryLab")
    with zipfile.ZipFile(archive) as source:
        source.extractall(work)
    project = work / "samples/InventoryLab"
    original = project_files(project)
    print("Migrating InventoryLab from", V31_COMMIT, "files:", len(original), flush=True)
    run("dotnet", "build", ENGINE / "src/Engine.Editor", *flags, "-o", work / "editor", env=env)
    run("dotnet", work / "editor/MoNoon.dll", "--migrate-project", project, env=env)
    if project_files(project) != original:
        raise ValueError("Migration changed the already-current v31 InventoryLab source or asset IDs")
    return project, original


def check_slot(slot, evidence, expected):
    # Keep the actual player output even when the comparison fails.
    shutil.copy2(slot, evidence)
    actual = evidence.read_bytes()
    if actual != expected:
        raise ValueError(f"{evidence.name}: expected {len(expected)} bytes {sha(expected)}, "
                         f"got {len(actual)} bytes {sha(actual)}")
    print("PASS", evidence.name, len(actual), "bytes SHA-256", sha(actual), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="Distribution build directory")
    parser.add_argument("results", type=Path, help="New directory for the two saves and build receipts")
    parser.add_argument("--expected-rid", choices=("osx-arm64", "win-x64", "linux-x64"),
                        help="Fail if the runner OS or CPU differs from the requested target")
    parser.add_argument("--upgrade-from-v31", action="store_true",
                        help="Migrate the archived v31 sample with the v35 editor and complete its quest from a v2 slot")
    parser.add_argument("--slot-recovery", action="store_true",
                        help="Check damaged-slot recovery and reward reclaims in both built players")
    args = parser.parse_args()
    output, results = args.output.resolve(), args.results.resolve()
    rid = host_rid()
    if args.expected_rid and rid != args.expected_rid:
        parser.error(f"Expected {args.expected_rid}, running on {rid}")
    results.mkdir(parents=True, exist_ok=False)
    print("Build host:", platform.platform(), platform.machine(), flush=True)
    run("dotnet", "--info")
    recording = FIXTURE / "Herbalist.inputreplay"
    expected = (FIXTURE / "Herbalist.save").read_bytes()
    print("Expected Mac save SHA-256", sha(expected), flush=True)
    if args.upgrade_from_v31:
        previous = V2_SLOT.read_bytes()
        if sha(previous) != "7450a6e0d3dab14ec6f69717f9b13e4f733197c7cb8a8f183738fc51bd13e1b2":
            raise ValueError("The v2 input differs from the Mac upgrade fixture")
    else:
        print("Mac input replay SHA-256", sha(recording.read_bytes()), flush=True)
    with tempfile.TemporaryDirectory(prefix="monoon-guest-") as tmp:
        work = Path(tmp)
        env, flags = prepare_tools(work, rid)
        project = REPO / "samples/InventoryLab"
        if args.upgrade_from_v31:
            templates = work / "templates"
            for player in (ENGINE / "src/Engine.Player", templates / rid, templates / "native-aot/src/Engine.Player"):
                if (player / "MoNoonContent.version").read_text().strip() != "35":
                    raise ValueError(f"Expected a version 35 player at {player}")
            project, original = migrate_v31(work, flags, env)
        for mode in ("release", "native-aot"):
            product = build_player(work, project, mode, rid, output, env)
            exe = product / ("MoNoonPlayer.exe" if rid == "win-x64" else "MoNoonPlayer")
            profile = work / ("profile-" + mode)
            headless = dict(env, SDL_VIDEO_DRIVER="monoon-no-video", SDL_AUDIO_DRIVER="monoon-no-audio",
                            MONOON_USER_DATA=str(profile))
            headless.pop("DISPLAY", None)
            headless.pop("WAYLAND_DISPLAY", None)
            shutil.copy2(product / "MoNoonBuild", results / (mode + ".receipt"))
            slot = profile / "slot-001.save"
            if args.upgrade_from_v31:
                profile.mkdir()
                slot.write_bytes(previous)
                run(exe, "--smoke-test", profile, "L", env=headless)
                check_slot(slot, results / (mode + "-loaded.save"), previous)
                run(exe, "--smoke-test", profile, "L", "1", "T", "2", "1", "S", env=headless)
            else:
                run(exe, "--replay-input", recording, profile, env=headless)
            check_slot(slot, results / (mode + ".save"), expected)
            print("PASS", rid, mode, "matches the Mac save byte-for-byte", flush=True)
            if args.slot_recovery:
                run(sys.executable, Path(__file__).with_name("check_recovery.py"), exe, FIXTURE,
                    results / (mode + "-recovery"), env=headless)
        if args.upgrade_from_v31 and project_files(project) != original:
            raise ValueError("Building the upgraded players changed the archived project files")


if __name__ == "__main__":
    main()
