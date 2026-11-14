"""Build and replay both InventoryLab players on the current desktop OS."""
import argparse
import hashlib
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import zipfile

REPO = Path(__file__).resolve().parents[3]
ENGINE = REPO / "engine"
FIXTURE = ENGINE / "tests" / "fixtures" / "InventoryLab"
V31_COMMIT = "6232209b5ce4a2c7516db0c8fd989ba63a8e77c5"
V2_SLOT = ENGINE / "tests/results/inventorylab-upgrade-x64/v2-input.save"


def run(*args, env=None):
    print("+", *args, flush=True)
    subprocess.run(args, cwd=REPO, env=env, check=True)


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
    args = parser.parse_args()
    output, results = args.output.resolve(), args.results.resolve()
    system = platform.system()
    machine = platform.machine().lower()
    if system == "Darwin" and machine == "arm64":
        rid = "osx-arm64"
    elif system in ("Linux", "Windows") and machine in ("amd64", "x86_64"):
        rid = "win-x64" if system == "Windows" else "linux-x64"
    else:
        parser.error(f"Unsupported build host: {system} {machine}")
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
        templates = work / "templates"
        kit = templates / "native-aot"
        kit.mkdir(parents=True)
        for file in ("Directory.Build.props", "Directory.Build.targets"):
            shutil.copy2(ENGINE / file, kit / file)
        shutil.copytree(ENGINE / "native" / "windows-x64", kit / "native" / "windows-x64")
        for project in ("Engine.Core", "Engine.Desktop", "Engine.Player"):
            shutil.copytree(ENGINE / "src" / project, kit / "src" / project,
                            ignore=shutil.ignore_patterns("bin", "obj"))
        env = dict(os.environ, MONOON_PLAYERS_DIR=str(templates))
        flags = ["-c", "Release", "-p:NuGetAudit=false", "-p:UseSharedCompilation=false", "-nr:false", "-m:1"]
        run("dotnet", "publish", ENGINE / "src/Engine.Player", "-r", rid, "--self-contained", "true",
            *flags, "-o", templates / rid, env=env)
        run("dotnet", "build", ENGINE / "src/Engine.Cli", *flags, "-o", work / "cli", env=env)
        project = REPO / "samples/InventoryLab"
        if args.upgrade_from_v31:
            for player in (ENGINE / "src/Engine.Player", templates / rid, kit / "src/Engine.Player"):
                if (player / "MoNoonContent.version").read_text().strip() != "35":
                    raise ValueError(f"Expected a version 35 player at {player}")
            project, original = migrate_v31(work, flags, env)
        for mode in ("release", "native-aot"):
            run("dotnet", work / "cli/MoNoon.Cli.dll", "build", project, mode, "--profile",
                "distribution", "--target", rid, "--output", output, env=env)
            bundle = Path("InventoryLab.app/Contents/MacOS") if rid == "osx-arm64" else Path("InventoryLab-" + rid)
            product = (output if mode == "release" else output / "NativeAOT") / bundle
            exe = product / ("MoNoonPlayer.exe" if rid == "win-x64" else "MoNoonPlayer")
            if (product / "MoNoonPlayer.dll").exists() != (mode == "release"):
                raise RuntimeError(f"Unexpected compilation mode at {product}")
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
        if args.upgrade_from_v31 and project_files(project) != original:
            raise ValueError("Building the upgraded players changed the archived project files")


if __name__ == "__main__":
    main()
