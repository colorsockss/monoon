"""Build and replay both InventoryLab players on the current desktop OS."""
import argparse
import hashlib
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile

REPO = Path(__file__).resolve().parents[3]
ENGINE = REPO / "engine"
FIXTURE = ENGINE / "tests" / "fixtures" / "InventoryLab"


def run(*args, env=None):
    print("+", *args, flush=True)
    subprocess.run(args, cwd=REPO, env=env, check=True)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="Distribution build directory")
    parser.add_argument("results", type=Path, help="New directory for the two saves and build receipts")
    parser.add_argument("--expected-rid", choices=("osx-arm64", "win-x64", "linux-x64"),
                        help="Fail if the runner OS or CPU differs from the requested target")
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
    print("Mac input SHA-256", sha(recording.read_bytes()), "expected save SHA-256", sha(expected), flush=True)
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
        for mode in ("release", "native-aot"):
            run("dotnet", work / "cli/MoNoon.Cli.dll", "build", "samples/InventoryLab", mode, "--profile",
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
            run(exe, "--replay-input", recording, profile, env=headless)
            save = profile / "slot-001.save"
            shutil.copy2(save, results / (mode + ".save"))
            shutil.copy2(product / "MoNoonBuild", results / (mode + ".receipt"))
            actual = save.read_bytes()
            print(rid, mode, "save bytes", len(actual), "SHA-256", sha(actual), flush=True)
            if actual != expected:
                raise ValueError(f"{mode} save differs from the Mac recording")
            print("PASS", rid, mode, "matches the Mac save byte-for-byte", flush=True)


if __name__ == "__main__":
    main()
