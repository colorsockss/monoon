"""Measure the two distribution players with real macOS key events and isolated profiles."""
import argparse
import csv
import hashlib
import os
from pathlib import Path
import subprocess
import tempfile
import threading
import time
import zipfile


def size(roots):
    logical = blocks = 0
    for base in roots:
        for folder, _, names in os.walk(base):
            for name in names:
                try:
                    st = os.stat(os.path.join(folder, name))
                except FileNotFoundError:  # Atomic receipt publication or clean exit.
                    continue
                logical += st.st_size
                blocks += st.st_blocks * 512
    return logical, blocks


def profile_run(player, profile, condition, keys):
    cache = profile / "Games" / "ade09cc3a76956d79832a2bea14c7e71" / "BundleCache"
    with tempfile.TemporaryDirectory(prefix="monoon-bench-") as run:
        run = Path(run)
        trace = run / "startup.csv"
        tmp = run / "tmp"
        tmp.mkdir()
        env = dict(os.environ, TMPDIR=str(tmp) + "/", MONOON_USER_DATA=str(profile),
                   MONOON_BUNDLE_CACHE="off" if condition == "off" else "on", MONOON_STARTUP_TRACE=str(trace))
        peaks = [0, 0, 0, 0]
        stop = threading.Event()

        def sample():
            sizes = (*size(tmp.glob("monoon-content-*")), *size([cache]))
            for i, n in enumerate(sizes):
                peaks[i] = max(n, peaks[i])

        def monitor():
            while not stop.is_set():
                sample()
                stop.wait(.01)

        def wait_for(check):
            deadline = time.monotonic() + 20
            while time.monotonic() < deadline:
                result = check()
                if result:
                    return result
                if proc.poll() is not None:
                    raise RuntimeError(f"Player exited {proc.returncode}: {log_path.read_text()}")
                time.sleep(.001)
            raise TimeoutError(f"{player}: {trace.read_text() if trace.exists() else 'no trace'}")

        def milestone(name):
            if trace.exists():
                for row in csv.reader(trace.read_text().splitlines()):
                    if len(row) >= 3 and row[0] == name:
                        return row[2] or True
            return None

        reader = threading.Thread(target=monitor)
        reader.start()
        log_path = run / "player.log"
        try:
            with log_path.open("w") as output:
                start = time.monotonic_ns()
                proc = subprocess.Popen([str(player)], env=env, stdout=output, stderr=subprocess.STDOUT)
                try:
                    wait_for(lambda: milestone("first_present"))
                    with subprocess.Popen([str(keys), str(proc.pid), "18"]) as key:  # 1 -> AddHerb
                        action = wait_for(lambda: milestone("first_action"))
                        action_ms = (time.monotonic_ns() - start) / 1e6
                        if key.wait(timeout=5):
                            raise RuntimeError("Could not activate the SDL window")
                    assert action == "AddHerb", action
                    slot = cache.parent / "slot-001.save"
                    # The preceding run's slot must never satisfy this check.
                    assert not slot.exists()
                    subprocess.run([str(keys), str(proc.pid), "1"], check=True)  # S -> save the new bag.
                    wait_for(slot.exists)
                    saved = slot.read_bytes()
                    assert b'item0: "herb"' in saved and b'count0: 1\n' in saved and b'item1:' not in saved, saved
                    wait_for(lambda: milestone("max_frame_ms"))
                    sample()
                    subprocess.run([str(keys), str(proc.pid), "53"], check=True)  # Escape -> normal shutdown.
                    if proc.wait(timeout=10) != 0:
                        raise RuntimeError(log_path.read_text())
                    slot.unlink()
                finally:
                    if proc.poll() is None:
                        proc.kill()
                        proc.wait()
        finally:
            stop.set()
            reader.join()
        events = list(csv.DictReader(trace.read_text().splitlines()))
        rows = {row["event"]: (float(row["elapsed_ms"]), row["value"]) for row in events}
        market, = (row for row in events if row["event"] == "bundle" and row["value"] == "Assets/Market")
        assert int(market["inflated_bytes"]) == (0 if condition == "hit" else 64 * 1024**2 + 44)
        assert rows["content_ready"][1] == condition and int(rows["frames"][1]) == 120, rows
        mount_bytes = int(rows["temp_mount_bytes" if condition == "off" else "cache_mount_bytes"][1])
        if condition == "off":
            assert peaks[0] == mount_bytes and peaks[2] == 0, peaks
        else:
            assert peaks[0] == 0 and peaks[2] >= mount_bytes, peaks
        assert not list(tmp.glob("monoon-content-*"))
        return dict(first_action_ms=round(action_ms, 3), entry_to_action_ms=rows["first_action"][0],
                    content_ms=round(rows["content_ready"][0] - rows["content_begin"][0], 3),
                    market_read_ms=float(market["read_ms"]), first_present_ms=rows["first_present"][0], max_frame_ms=float(rows["max_frame_ms"][1]),
                    temp_peak_bytes=peaks[0], temp_allocated_bytes=peaks[1],
                    cache_peak_bytes=peaks[2], cache_allocated_bytes=peaks[3],
                    temp_after_exit_bytes=size(tmp.glob("monoon-content-*"))[0],
                    cache_after_exit_bytes=size([cache])[0], save_sha256=hashlib.sha256(saved).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("builds", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--repeats", type=int, default=5)
    args = parser.parse_args()
    root = args.builds.resolve()
    players = {"Release": root, "Native AOT": root / "NativeAOT"}
    bundles = []
    for directory in players.values():
        archive = directory / "InventoryLab.app/Contents/MacOS/ContentBundles/Assets/Market.bundle"
        with zipfile.ZipFile(archive) as z:
            assert z.namelist() == ["Assets/Market/MarketMorning.wav"] and z.getinfo(z.namelist()[0]).file_size >= 64 * 1024**2
        bundles.append(hashlib.sha256(archive.read_bytes()).hexdigest())
    assert bundles[0] == bundles[1], "The two players must contain the same market bundle."
    if args.repeats < 1:
        parser.error("--repeats must be positive")
    with args.output.open("x") as out, tempfile.TemporaryDirectory(prefix="monoon-bench-profiles-") as profiles:
        keys = Path(profiles) / "mac-key"
        subprocess.run(["xcrun", "clang", "-fobjc-arc", "-Wno-deprecated-declarations", "-framework", "AppKit",
                        "-framework", "CoreGraphics", str(Path(__file__).with_name("mac_key.m")), "-o", str(keys)], check=True)
        writer = None
        save_hash = None
        for repeat in range(1, args.repeats + 1):
            order = list(players) if repeat % 2 else list(reversed(players))
            for mode in order:
                player = players[mode] / "InventoryLab.app/Contents/MacOS/MoNoonPlayer"
                profile = Path(profiles) / f"{mode}-{repeat}"
                # Only the hit depends on the miss. Rotate the baseline across repetitions.
                conditions = ["off", "miss", "hit"] if repeat % 2 else ["miss", "hit", "off"]
                for condition in conditions:
                    row = dict(mode=mode, repeat=repeat, cache=condition,
                               **profile_run(player, profile / ("off" if condition == "off" else "on"), condition, keys))
                    if save_hash is None:
                        save_hash = row["save_sha256"]
                    assert row["save_sha256"] == save_hash, "Player save differs from earlier runs"
                    if writer is None:
                        writer = csv.DictWriter(out, row.keys(), lineterminator="\n")
                        writer.writeheader()
                    writer.writerow(row)
                    out.flush()
                    print(mode, repeat, condition, row["first_action_ms"], "ms", flush=True)


if __name__ == "__main__":
    main()
