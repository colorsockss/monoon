"""arm64 vs x64 AudioLab PCM (SDL_mixer is native code per architecture). Usage:
audio_arch.py <mac mix.f32 with the first four loops> <linux release dir> <linux native-aot dir>
Prints Release = Native AOT on x64, the size of the arm64/x64 difference over the first four loops, and the frame counts."""
from array import array
import hashlib, json, math, sys
from pathlib import Path

PERIOD = 57600
def pcm(p):
    a = array('f'); a.frombytes(Path(p).read_bytes()); return a

mac = pcm(sys.argv[1])
runs = {m: (pcm(Path(d) / 'mix.f32'), json.loads((Path(d) / 'measurements.json').read_text())) for m, d in zip(('release', 'native-aot'), sys.argv[2:4])}
(rel, rm), (aot, am) = runs['release'], runs['native-aot']
n = 8 * PERIOD
same_until = min(rm['stop_frame'], am['stop_frame'])
print('x64 Release = Native AOT, first four loops:', rel[:n] == aot[:n], hashlib.sha256(rel[:n].tobytes()).hexdigest())
print(f'x64 Release = Native AOT, frames 0..{same_until} (before the earlier F stop):', rel[:2 * same_until] == aot[:2 * same_until])
for key in ('loop_frames', 'fade_in_frames', 'fade_out_frames', 'max_envelope_error'):
    print(f'x64 {key}: release {rm[key]} native-aot {am[key]}')
diff = [i for i in range(n) if rel[i] != mac[i]]
d = [abs(rel[i] - mac[i]) for i in range(n)]
rms = math.sqrt(sum(x * x for x in d) / n); sig = math.sqrt(sum(x * x for x in mac[:n]) / n)
print(f'arm64 vs x64 first four loops ({n} samples): {len(diff)} differ, first differing frame {diff[0] // 2 if diff else None}, '
      f'max |diff| {max(d):.3g} ({max(d) * 2 ** 23:.2f} LSB of 24-bit PCM), RMS diff {rms:.3g} vs signal RMS {sig:.3g} ({20 * math.log10(rms / sig):.1f} dB)')
print('arm64 hash', hashlib.sha256(mac[:n].tobytes()).hexdigest())
