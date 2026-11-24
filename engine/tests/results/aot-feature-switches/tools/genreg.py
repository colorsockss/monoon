import sys, os, re
# usage: genreg.py <generated dir> <out .cs>
# Writes PlayerYamlRegistration.cs: the generator's registration list with each line gated by the features whose types alone use it.
G = sys.argv[1]
refs = {}
for f in os.listdir(G):
    m = re.match(r'Engine\.Core\.(\w+)\.YamlFormatter\.g\.cs$', f)
    if m: refs[m.group(1)] = set(re.findall(r'global::Engine\.Core\.(\w+)', open(os.path.join(G, f)).read())) - {m.group(1)}
reg = set(refs)
refs['SceneComponent'] = set()  # the union names every component; not a reason to keep one
def reach(roots):
    seen = set(roots); stack = list(roots)
    while stack:
        for y in refs.get(stack.pop(), ()):
            if y in reg and y not in seen: seen.add(y); stack.append(y)
    return seen
F = {
 'Text': ['Text2DComponent', 'UiTextComponent', 'HudComponent', 'CutsceneDirectorComponent', 'CutsceneSequence', 'UiButtonComponent',
          'UiSliderComponent', 'UiToggleComponent', 'UiDropdownComponent', 'UiTextFieldComponent'],
 'Audio': ['AudioSourceComponent', 'AudioListener2DComponent'],
 'Physics': [t for t in reg if re.search(r'Collider2DComponent$|Joint2DComponent$|^Rigidbody2DComponent$|^CharacterMotor2DComponent$', t)],
 'Tilemaps': ['Tilemap2DComponent', 'Tileset', 'TilePage'],
 'Particles': ['ParticleEmitter2DComponent', 'ParticleEffect'],
 'Lighting': ['PointLight2DComponent', 'GlobalLight2DComponent'],
 'Skeletons': ['SkeletonAnimator2DComponent', 'FootIK2DComponent', 'SkeletonRig'],
 'Tweens': ['PositionTween2DComponent', 'PathFollower2DComponent'],
}
owner = {}
for f, roots in F.items():
    cand = reach([r for r in roots if r in reg])
    for t in cand - reach(reg - cand): owner[t] = f
lines = [l.strip() for l in open(os.path.join(G, 'VYamlGeneratedRegistration.g.cs')) if 'Register' in l and l.strip().endswith(';')]
def gate(line):
    m = re.match(r'global::Engine\.Core\.(\w+)\.__RegisterVYamlFormatter\(\);', line)
    if m: return {owner[m.group(1)]} if m.group(1) in owner else set()
    args = re.findall(r'global::Engine\.Core\.(\w+)', line)
    if not args: return set()
    users = [t for t in reg if any(a in refs.get(t, ()) for a in args)]
    fs = {owner.get(t) for t in users}
    return set() if None in fs or not fs else fs
order = list(F)
out = []
groups = {}
for line in lines:
    key = tuple(sorted(gate(line), key=order.index))
    groups.setdefault(key, []).append(line.replace('global::VYaml.Serialization.', '').replace('global::Engine.Core.', '').replace('global::System.Text.Json.', '').replace('global::System.', ''))
body = []
for key in sorted(groups, key=lambda k: (len(k), [order.index(x) for x in k])):
    ind = '        '
    if key:
        body.append(ind + 'if (' + ' || '.join('EngineFeatures.' + k for k in key) + ')')
        body.append(ind + '{')
        body += [ind + '    ' + l for l in groups[key]]
        body.append(ind + '}')
    else:
        body += [ind + l for l in groups[key]]
src = '''using System.Text.Json;
using VYaml.Serialization;

namespace Engine.Core;

// Native AOT players register the VYaml formatters here instead of the source generator's module initializer, which
// Engine.Player/EngineFeatures.Substitutions.xml empties in Native AOT (roadmap 15.12). The list is the generator's; a line is
// behind the features whose components and asset formats alone use it, so a game built without them constructs none of
// those formatters and ILC drops their Deserialize code and the types they create. FeatureSwitchChecks compares
// this list with the generated one and checks that no other registered type reaches a gated one.
internal static class PlayerYamlRegistration
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register()
    {
        // JIT (editor, Release players): the generated initializer has registered everything.
        if (System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported) return;
''' + '\n'.join(body) + '''
    }
}
'''
open(sys.argv[2], 'w').write(src)
for k in groups: print(k, len(groups[k]))
