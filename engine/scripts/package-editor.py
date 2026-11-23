"""Build the standalone Windows editor or an editable KeyRoom sample archive (no Linux editor since 2026-11-23)."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parents[2]


def package_sample(archive):
    project = ROOT / "samples/KeyRoom"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as bundle:
        for entry in ("GameSettings", ".monoon-assets", "Assets", "Editor", "Scenes", "Scripts", ".monoon-atlas"):
            source = project / entry
            if not source.exists():
                if entry == ".monoon-atlas":
                    continue
                raise FileNotFoundError(source)
            files = sorted(source.rglob("*")) if source.is_dir() else [source]
            for path in files:
                if path.is_file() and path.name != ".DS_Store":
                    bundle.write(path, Path("KeyRoom") / path.relative_to(project))


def package_editor(target, stage, archive):
    subprocess.run([
        "dotnet", "publish", str(ROOT / "engine/src/Engine.Editor/Engine.Editor.csproj"),
        "-c", "Release", "-r", target, "--self-contained", "true", "-o", str(stage),
        "-nr:false", "-m:1", "-p:UsedAvaloniaProducts=", "-p:UseSharedCompilation=false",
        "-p:NuGetAudit=false", "-p:PublishSingleFile=false",
    ], check=True)
    native_files = ("MoNoon.exe", "coreclr.dll", "SDL3.dll", "SDL3_mixer.dll", "libSkiaSharp.dll")
    for name in (*native_files, "MoNoon.dll", "Microsoft.CodeAnalysis.CSharp.dll"):
        if not (stage / name).is_file():
            raise RuntimeError(f"Publish is missing {name}")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(stage.rglob("*")):
            if path.is_file():
                bundle.write(path, Path("editor") / path.relative_to(stage))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("target", choices=["win-x64", "keyroom"])
    parser.add_argument("--output", type=Path, help="Override the artifact directory.")
    args = parser.parse_args()
    default_output = ROOT / ("samples/dist" if args.target == "keyroom" else "editor")
    output = (args.output or default_output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    name = {"win-x64": "MoNoon-win-x64.zip", "keyroom": "KeyRoom-sample.zip"}[args.target]
    with tempfile.TemporaryDirectory(dir=output) as work:
        artifact = Path(work) / name
        if args.target == "keyroom":
            package_sample(artifact)
        else:
            package_editor(args.target, Path(work) / "publish", artifact)
        os.replace(artifact, output / name)
    print(output / name)


if __name__ == "__main__":
    main()
