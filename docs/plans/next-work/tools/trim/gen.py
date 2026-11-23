import os, sys, zlib, struct, wave
d = sys.argv[1]
os.makedirs(d, exist_ok=True)
w = h = 1024
raw = b''.join(b'\x00' + os.urandom(w*4) for _ in range(h))
def chunk(t, data): return struct.pack('>I', len(data)) + t + data + struct.pack('>I', zlib.crc32(t+data) & 0xffffffff)
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
open(os.path.join(d, 'BigUnused.png'), 'wb').write(png)
with wave.open(os.path.join(d, 'BigUnused.wav'), 'wb') as f:
    f.setnchannels(2); f.setsampwidth(2); f.setframerate(44100); f.writeframes(os.urandom(44100*4*30))
