"""Build distribution players with the current host SDK and an isolated template kit."""
import os
from pathlib import Path
import platform
import shutil
import subprocess

REPO = Path(__file__).resolve().parents[2]
ENGINE = REPO / "engine"


def run(*args, env=None):
    print("+", *args, flush=True)
    subprocess.run(args, cwd=REPO, env=env, check=True)


def host_rid():
    system = platform.system()
    machine = platform.machine().lower()
    if system == "Darwin" and machine == "arm64":
        rid = "osx-arm64"
    elif system in ("Linux", "Windows") and machine in ("amd64", "x86_64"):
        rid = "win-x64" if system == "Windows" else "linux-x64"
    else:
        raise ValueError(f"Unsupported build host: {system} {machine}")
    return rid


def prepare_tools(work, rid):
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
    return env, flags


def build_player(work, project, mode, rid, output, env):
    run("dotnet", work / "cli/MoNoon.Cli.dll", "build", project, mode, "--profile",
        "distribution", "--target", rid, "--output", output, env=env)
    bundle = Path(project.name + ".app/Contents/MacOS") if rid == "osx-arm64" else Path(project.name + "-" + rid)
    product = (output if mode == "release" else output / "NativeAOT") / bundle
    if (product / "MoNoonPlayer.dll").exists() != (mode == "release"):
        raise RuntimeError(f"Unexpected compilation mode at {product}")
    return product
