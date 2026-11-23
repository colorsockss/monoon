import ctypes, sys, glob, os
# Subset a font to the code points that appear in the given text files, using HarfBuzzSharp's native hb_subset.
lib = ctypes.CDLL(sys.argv[1]); font_path = sys.argv[2]; out = sys.argv[3]
text = ''.join(open(p, encoding='utf-8', errors='ignore').read() for p in sys.argv[4:])
cps = sorted({ord(c) for c in text} | set(range(0x20, 0x7f)))
lib.hb_blob_create_from_file.restype = ctypes.c_void_p
lib.hb_face_create.restype = ctypes.c_void_p
lib.hb_subset_input_create_or_fail.restype = ctypes.c_void_p
lib.hb_subset_input_unicode_set.restype = ctypes.c_void_p
lib.hb_subset_input_unicode_set.argtypes = [ctypes.c_void_p]
lib.hb_set_add.argtypes = [ctypes.c_void_p, ctypes.c_uint32]
lib.hb_face_create.argtypes = [ctypes.c_void_p, ctypes.c_uint]
lib.hb_subset_or_fail.restype = ctypes.c_void_p
lib.hb_subset_or_fail.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
lib.hb_face_reference_blob.restype = ctypes.c_void_p
lib.hb_face_reference_blob.argtypes = [ctypes.c_void_p]
lib.hb_blob_get_data.restype = ctypes.c_void_p
lib.hb_blob_get_data.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.c_uint)]
blob = lib.hb_blob_create_from_file(font_path.encode())
face = lib.hb_face_create(blob, 0)
inp = lib.hb_subset_input_create_or_fail()
s = lib.hb_subset_input_unicode_set(inp)
for c in cps: lib.hb_set_add(s, c)
sub = lib.hb_subset_or_fail(face, inp)
if not sub: sys.exit('subset failed')
n = ctypes.c_uint()
data = lib.hb_blob_get_data(lib.hb_face_reference_blob(sub), ctypes.byref(n))
open(out, 'wb').write(ctypes.string_at(data, n.value))
print(f'{os.path.basename(font_path)}: {os.path.getsize(font_path)} -> {n.value} bytes, {len(cps)} code points')
