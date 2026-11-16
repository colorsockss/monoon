"""Analyze actual MONOON_AUDIO_CAPTURE output for AudioLab (stdlib, little-endian hosts)."""
from array import array
import argparse
import csv
import hashlib
import math
from pathlib import Path
import sys
import wave

RATE = 48000
PERIOD = 57600
BLOCK = 512


def read_capture(file):
    samples = array("f")
    samples.frombytes(file.read_bytes())
    if sys.byteorder != "little":
        samples.byteswap()
    n = len(samples) // 2
    assert n > 4 * PERIOD and len(samples) == 2 * n
    steady = samples[2 * PERIOD:4 * PERIOD]
    assert steady == samples[4 * PERIOD:6 * PERIOD] == samples[6 * PERIOD:8 * PERIOD]
    # Include every sample on both sides of each join as well as the interiors.
    assert samples[2 * (PERIOD - 960):2 * (PERIOD + 960)] == samples[2 * (2 * PERIOD - 960):2 * (2 * PERIOD + 960)]

    def differs(i):
        j = i % PERIOD
        return samples[2*i] != steady[2*j] or samples[2*i+1] != steady[2*j+1]

    # Unity gain is reached on the last fade-in frame. Stop takes effect between SDL blocks.
    fade_in = max(i for i in range(PERIOD) if differs(i)) + 2
    first_change = next(i for i in range(4 * PERIOD, n) if differs(i))
    stop = first_change // BLOCK * BLOCK
    fade_out = max(i for i in range(stop, n) if samples[2*i] or samples[2*i+1]) - stop + 2
    assert (fade_in, fade_out) == (19200, 28800)
    assert all(x == 0 for x in samples[2*(stop + fade_out - 1):])

    max_error = 0.0
    for start, length, rising in [(0, fade_in, True), (stop, fade_out, False)]:
        for i in range(length):
            frame = start + i
            amp = (i + 1) / length
            gain = amp if rising else 1 - amp
            for c in (0, 1):
                error = abs(samples[2*frame + c] - steady[2*(frame % PERIOD) + c]*gain)
                max_error = max(max_error, error)
    assert max_error < 1e-4, max_error

    jumps, typical_peaks = [], []
    for c in (0, 1):
        channel = steady[c::2]
        jumps.append(abs(channel[0] - channel[-1]))
        typical_peaks.append(max(abs(a-b) for a, b in zip(channel[2400:-2401], channel[2401:-2400])))
    assert all(j <= peak for j, peak in zip(jumps, typical_peaks)), (jumps, typical_peaks)
    return samples, steady, dict(loop_frames=PERIOD, fade_in_frames=fade_in, fade_out_frames=fade_out,
        stop_frame=stop, captured_frames=n, seam_left=jumps[0], seam_right=jumps[1],
        interior_peak_left=typical_peaks[0], interior_peak_right=typical_peaks[1],
        max_envelope_error=max_error, first_four_loops_sha256=hashlib.sha256(samples[:8*PERIOD].tobytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for arg in ["captures", "wav_builds", "ogg_builds", "results"]:
        parser.add_argument(arg, type=Path)
    args = parser.parse_args()
    args.results.mkdir(parents=True, exist_ok=True)
    baseline = {}
    hashes = {}
    with (args.results / "playback.csv").open("w") as dest:
        writer = None
        for codec in ["wav", "ogg"]:
            for mode in ["scene-play", "release", "aot"]:
                capture = args.captures / codec / mode / "output.f32"
                samples, steady, row = read_capture(capture)
                digest = row["first_four_loops_sha256"]
                assert hashes.setdefault(codec, digest) == digest
                if codec == "wav":
                    baseline[mode] = steady
                else:
                    around = steady[-960:] + steady[:960]
                    ref = baseline[mode][-960:] + baseline[mode][:960]
                    similarity = sum(a*b for a,b in zip(around,ref)) / math.sqrt(sum(a*a for a in around)*sum(b*b for b in ref))
                    assert similarity > .99
                    row["seam_wav_similarity"] = similarity
                row = dict(codec=codec, mode=mode, **row)
                if writer is None:
                    writer = csv.DictWriter(dest, [*row, "seam_wav_similarity"], lineterminator="\n")
                    writer.writeheader()
                writer.writerow(row)
                if codec == "ogg" and mode == "aot":
                    with wave.open(str(args.results / "native-aot-output.wav"), "wb") as out:
                        out.setparams((2, 2, RATE, 0, "NONE", "not compressed"))
                        out.writeframes(array("h", (round(max(-1,min(1,x))*32767) for x in samples)).tobytes())

    with (args.results / "sizes.csv").open("w") as dest:
        writer = csv.writer(dest, lineterminator="\n")
        writer.writerow(["mode", "codec", "music_bytes", "app_bytes", "build_id"])
        for mode, sub in [("release", ""), ("aot", "NativeAOT")]:
            for codec, builds in [("wav", args.wav_builds), ("ogg", args.ogg_builds)]:
                app = builds / sub / "AudioLab.app"
                bin_dir = app / "Contents/MacOS"
                track, = (bin_dir / "Game/Assets").glob("Tide.*")
                assert track.suffix == "." + codec
                total = sum(p.stat().st_size for p in app.rglob("*") if p.is_file())
                build_id, = (l.split('"')[1] for l in (bin_dir / "MoNoonBuild").read_text().splitlines() if l.strip().startswith("buildId:"))
                writer.writerow([mode, codec, track.stat().st_size, total, build_id])
    print("PASS six output captures, sample-exact periods and fades, boundary levels and four app inventories.")


if __name__ == "__main__":
    main()
