"""Build and replay KeyRoom's world checkpoints on the current desktop OS."""
import argparse
from hashlib import sha256
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import traceback
from uuid import UUID

TESTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TESTS))
from desktop_players import REPO, build_player, host_rid, prepare_tools

FIXTURES = Path(__file__).resolve().with_name("fixtures")
STALE_TEMPLATE_FILE = "stale-template-file.txt"


def check_runtime_package(product, mode):
    tools = ("Engine.Authoring", "Microsoft.CodeAnalysis", "Avalonia", "SkiaSharp")
    unexpected = [p.name for p in product.rglob("*.dll") if p.name.startswith(tools)]
    unexpected += [p.name for p in product.rglob("*") if "skiasharp" in p.name.lower()]
    unexpected += [name for name in (STALE_TEMPLATE_FILE, "MoNoonPlayer.files", "MoNoonContent.version",
                                     "MoNoonSdlPlayer.version", "MoNoonVorbis.version", "MoNoonShaderSheets.version",
                                     "MoNoonUiControls.version", "MoNoonObjectGroups.version", "MoNoonUiPins.version", "MoNoonComponentKeys.version", "MoNoonTileCollision.version") if (product / name).exists()]
    if mode == "release":
        dependencies = json.loads((product / "MoNoonPlayer.deps.json").read_text())["libraries"]
        unexpected += [name for name in dependencies if name.startswith(tools)]
    if unexpected:
        raise ValueError(f"Authoring or template files in {mode} game: {unexpected}")


def coin_id(save):
    # The Coin prefab has one member. The same instance ID also identifies its saved pose.
    member = re.search(rb'\n +members:\n +[0-9a-f-]+: "([0-9a-f-]+)"\n', save)
    if not member or UUID(member[1].decode()).version != 4:
        raise ValueError("Expected a newly instantiated Coin prefab member")
    return member[1]


def check_recording(player, profile, recording, slot, evidence, env, fixtures=FIXTURES):
    # Each recording restarts the exported game. Preserve the output even on a mismatch.
    subprocess.run([str(player), "--replay-input", str(fixtures / (recording + ".inputreplay")),
                    str(profile)], cwd=player.parent, env=env, timeout=60, check=True)
    shutil.copyfile(slot, evidence)
    return evidence.read_bytes()


def match(name, actual, expected):
    if actual != expected:
        raise ValueError(f"{name}: expected {sha256(expected).hexdigest()}, got {sha256(actual).hexdigest()}")
    print(f"PASS {name}: {len(actual)} bytes SHA-256 {sha256(actual).hexdigest()}", flush=True)


def check_mac_libraries(product):
    alias = product / "libSDL3.dylib"
    versioned = product / "libSDL3.0.dylib"
    if not versioned.is_file() or versioned.is_symlink() or os.readlink(alias) != versioned.name:
        raise ValueError("Expected the SDL loader alias to point to the bundled versioned library")


def check_player(product, rid, folder):
    player = product / ("MoNoonPlayer.exe" if rid == "win-x64" else "MoNoonPlayer")
    shutil.copyfile(product / "MoNoonBuild", folder / "build.receipt")
    profile = folder / "profile"
    profile.mkdir()
    env = dict(os.environ, MONOON_USER_DATA=str(profile), SDL_VIDEO_DRIVER="monoon-no-video",
               SDL_AUDIO_DRIVER="monoon-no-audio")
    slot1, slot2 = profile / "slot-001.save", profile / "slot-002.save"
    pending = (FIXTURES / "pending.save").read_bytes()
    collected = (FIXTURES / "collected.save").read_bytes()

    def play(recording, slot):
        return check_recording(player, profile, recording, slot, folder / (recording + ".save"), env)

    spawned = play("spawn", slot1)
    match("E, N, N, F5: one coin under Chest at (84, -5)", spawned,
          pending.replace(coin_id(pending), coin_id(spawned)))
    match("F9, slot 2, F5: same position, parent and instance ID", play("restore", slot2), spawned)
    # The last replay must create a fresh output, even if its F5 handler breaks.
    slot2.unlink()
    match("F9, collect, F5: chest open, coin collected and absent", play("collect", slot1), collected)
    match("F9 twice, E, slot 2, F5: coin remains absent", play("collected-reload", slot2), collected)

    mac_profile = folder / "mac-profile"
    mac_profile.mkdir()
    (mac_profile / slot1.name).write_bytes(pending)
    restored = check_recording(player, mac_profile, "restore", mac_profile / slot2.name,
                               folder / "mac-roundtrip.save", dict(env, MONOON_USER_DATA=str(mac_profile)))
    match("Mac Scene Play checkpoint round trip", restored, pending)

    check_carry_checkpoints(player, folder / "carry", env)


def check_carry_checkpoints(player, folder, env):
    fixtures = FIXTURES / "carry"
    profile = folder / "profile"
    profile.mkdir(parents=True)
    env = dict(env, MONOON_USER_DATA=str(profile))
    held_id = None
    for name, slot in (("held", 1), ("held-restored", 2), ("dropped", 1),
                       ("dropped-restored", 2), ("outside", 1), ("collected", 1)):
        save = profile / f"slot-{slot:03d}.save"
        if slot == 2:
            save.unlink(missing_ok=True)
        actual = check_recording(player, profile, name, save, folder / (name + ".save"), env, fixtures)
        if name.endswith("-restored"):
            expected = (folder / (name.removesuffix("-restored") + ".save")).read_bytes()
        else:
            expected = (fixtures / (name + ".save")).read_bytes()
            if name == "held":
                held_id = coin_id(actual)
            if name != "collected":
                expected = expected.replace(coin_id(expected), held_id)
        match("CarryCoin " + name, actual, expected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("rid", choices=("osx-arm64", "win-x64", "linux-x64"))
    parser.add_argument("results", type=Path)
    args = parser.parse_args()
    if host_rid() != args.rid:
        parser.error(f"Expected {args.rid}, running on {host_rid()}")
    results = args.results.resolve()
    results.mkdir(parents=True, exist_ok=False)
    failed = []
    with tempfile.TemporaryDirectory(prefix="monoon-coin-") as tmp:
        work = Path(tmp).resolve()
        env, _ = prepare_tools(work, args.rid)
        (Path(env["MONOON_PLAYERS_DIR"]) / args.rid / STALE_TEMPLATE_FILE).write_text("not a runtime file")
        for mode in ("release", "native-aot"):
            folder = results / mode
            folder.mkdir()
            try:
                product = build_player(work, REPO / "samples/KeyRoom", mode, args.rid, work / "exports", env)
                check_runtime_package(product, mode)
                if args.rid == "osx-arm64":
                    check_mac_libraries(product)
                check_player(product, args.rid, folder)
            except Exception:
                failure = traceback.format_exc()
                (folder / "failure.txt").write_text(failure)
                print(failure, file=sys.stderr, flush=True)
                failed.append(mode)
    if failed:
        raise SystemExit(f"{args.rid}: failed {', '.join(failed)}")


if __name__ == "__main__":
    main()
