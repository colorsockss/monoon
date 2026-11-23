import sys, re, collections, xml.etree.ElementTree as ET
# Sums ILC map.xml node lengths by node kind and by the assembly prefix of the mangled symbol name.
PREFIX = [('S_P_CoreLib_', 'System.Private.CoreLib'), ('S_P_Reflection_', 'System.Private.Reflection*'),
          ('S_P_TypeLoader_', 'System.Private.TypeLoader'), ('S_P_StackTraceMetadata_', 'StackTraceMetadata'),
          ('S_P_Interop', 'System.Private.Interop'), ('System_Text_Json', 'System.Text.Json'),
          ('System_Linq_Expressions', 'System.Linq.Expressions'), ('System_Linq', 'System.Linq'),
          ('System_Collections', 'System.Collections*'), ('System_Private_Uri', 'System.Private.Uri'),
          ('System_Text_RegularExpressions', 'System.Text.RegularExpressions'), ('System_Text_Encodings', 'System.Text.Encodings.Web'),
          ('System_Memory', 'System.Memory'), ('System_Runtime', 'System.Runtime*'), ('System_Console', 'System.Console'),
          ('System_IO_Compression', 'System.IO.Compression'), ('System_Security', 'System.Security*'),
          ('System_ComponentModel', 'System.ComponentModel*'), ('System_Threading', 'System.Threading*'),
          ('System_Diagnostics', 'System.Diagnostics*'), ('System_Private_Xml', 'System.Private.Xml'), ('System_', 'System.* other'),
          ('Microsoft_Extensions_Logging', 'M.E.Logging*'), ('Microsoft_Extensions_DependencyInjection', 'M.E.DependencyInjection*'),
          ('Microsoft_Extensions_Options', 'M.E.Options'), ('Microsoft_Extensions_Configuration', 'M.E.Configuration*'),
          ('Microsoft_Extensions_Primitives', 'M.E.Primitives'), ('Microsoft_Extensions_ObjectPool', 'M.E.ObjectPool'),
          ('Microsoft_Extensions', 'M.E.* other'),
          ('Engine_Core', 'Engine.Core'), ('Engine_Desktop', 'Engine.Desktop'), ('MoNoonPlayer', 'MoNoonPlayer'), ('MoNoon_GameScripts', 'GameScripts'),
          ('VYaml', 'VYaml'), ('Brutal_Numerics', 'Brutal.Numerics'), ('Brutal_Sdl_Ttf', 'Brutal.Sdl.Ttf'), ('Brutal_Sdl_Mixer', 'Brutal.Sdl.Mixer'),
          ('Brutal_Sdl', 'Brutal.Sdl'), ('Brutal_Box2D', 'Brutal.Box2D'), ('Brutal_Logging', 'Brutal.Logging'), ('Brutal_Strings', 'Brutal.Strings'),
          ('Brutal_', 'Brutal.* other'), ('CommunityToolkit', 'CommunityToolkit.HighPerformance')]
def owner(name):
    n = name.lstrip('_')
    best = None
    for order, (p, a) in enumerate(PREFIX):
        i = n.find(p)
        if i != -1 and (best is None or i < best[0]): best = (i, a)
    return best[1] if best and best[0] < 60 else '(unattributed)'
kinds = collections.Counter(); owners = collections.Counter(); top = []
for _, el in ET.iterparse(sys.argv[1]):
    L = el.get('Length')
    if L is None: continue
    L = int(L); kinds[el.tag] += L
    name = el.get('Name') or ''
    owners[owner(name)] += L
    top.append((L, el.tag, name[:140]))
    el.clear()
tot = sum(kinds.values())
print(f'total node bytes {tot:,}')
print('-- by node kind'); [print(f'  {k:32} {v:>11,} {100*v/tot:5.1f}%') for k, v in kinds.most_common(14)]
print('-- by owner (prefix heuristic)'); [print(f'  {k:32} {v:>11,} {100*v/tot:5.1f}%') for k, v in owners.most_common(40)]
if len(sys.argv) > 2:
    print('-- largest nodes'); [print(f'  {l:>9,} {t:20} {n}') for l, t, n in sorted(top, reverse=True)[:int(sys.argv[2])]]
