"""Synthetic 'dialogue line' for AudioLab: three voiced syllables with silent gaps (24 kHz mono 16-bit)."""
import math, struct, sys, wave

RATE = 24000
SYLLABLES = [(0.00, 0.20, 170, 150), (0.35, 0.20, 190, 210), (0.70, 0.20, 160, 130)]  # start, length, f0 start/end Hz
LENGTH = 0.90
FORMANTS = [(700, 1.0), (1200, 0.5), (2600, 0.2)]

samples = [0.0] * int(LENGTH * RATE)
for start, length, f0a, f0b in SYLLABLES:
    n = int(length * RATE)
    phase = 0.0
    for i in range(n):
        t = i / n
        f0 = f0a + (f0b - f0a) * t
        phase += 2 * math.pi * f0 / RATE
        env = math.sin(math.pi * t) ** 2
        v = 0.0
        for h in range(1, 20):
            f = h * f0
            weight = sum(a / (1 + ((f - c) / 120) ** 2) for c, a in FORMANTS)
            v += weight * math.sin(h * phase) / h
        samples[int(start * RATE) + i] = 0.35 * env * v
peak = max(abs(s) for s in samples)
with wave.open(sys.argv[1], "wb") as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
    w.writeframes(b"".join(struct.pack("<h", round(32767 * 0.6 * s / peak)) for s in samples))
