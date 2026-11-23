import os, sys, collections
RUNTIME_NATIVE = {'libcoreclr.dylib','libclrjit.dylib','libhostfxr.dylib','libhostpolicy.dylib','libSystem.Native.dylib',
    'libSystem.IO.Compression.Native.dylib','libSystem.Security.Cryptography.Native.Apple.dylib','libSystem.Net.Security.Native.dylib',
    'libSystem.Globalization.Native.dylib','libmscordaccore.dylib','libmscordbi.dylib','libclrgc.dylib','libclrgcexp.dylib','createdump',
    'libcoreclrtraceptprovider.dylib','libSystem.Security.Cryptography.Native.OpenSsl.dylib'}
def cat(rel, name):
    if rel.startswith('Game/'): return 'content'
    if rel.startswith('Licenses/'): return 'licences'
    if rel.startswith('EngineAssets'): return 'engine-assets'
    if name.endswith('.pdb') or '.dSYM' in rel or name.endswith('.dbg'): return 'symbols'
    if name in RUNTIME_NATIVE: return 'runtime-native'
    if name.endswith('.dylib') or name.endswith('.so'): return 'game-native'
    if name.endswith('.dll'):
        if name.startswith('Microsoft.Extensions'): return 'ms-extensions'
        if name.startswith(('System.','Microsoft.','mscorlib','netstandard','WindowsBase')): return 'bcl-managed'
        return 'engine-managed'
    if name == 'MoNoonPlayer': return 'exe'
    return 'other'
verbose = os.environ.get('V') == '1'
for root in sys.argv[1:]:
    out = collections.Counter(); files = collections.defaultdict(list)
    for d, _, fs in os.walk(root):
        for f in fs:
            p = os.path.join(d, f)
            if os.path.islink(p): continue
            rel = os.path.relpath(p, root).split('Contents/MacOS/', 1)[-1]
            c = cat(rel, f); s = os.path.getsize(p)
            out[c] += s; files[c].append((s, rel))
    tot = sum(out.values())
    print(f'== {root}  total {tot:,} B ({tot/1048576:.2f} MiB)')
    for c, s in out.most_common():
        print(f'  {c:15} {s:>12,}  {s/1048576:6.2f} MiB  n={len(files[c])}')
    if verbose:
        for c in ('game-native', 'runtime-native', 'engine-managed', 'ms-extensions', 'bcl-managed', 'other', 'exe', 'content'):
            for s, r in sorted(files[c], reverse=True)[:12]:
                print(f'     {c[:8]:8} {s:>11,} {r}')
