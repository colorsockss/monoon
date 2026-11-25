import re, sys, collections
# Group Engine.Core/Engine.Desktop/Brutal native code (MethodCode) by engine feature using type-name patterns.
groups = [
 ('physics', r'Rigidbody2D|Joint2D|CharacterMotor|ScenePhysics|Physics2D|Box2D|ColliderGrid|SceneContacts|CollisionGeometry|TileCollision|PhysicsMaterial|Collider2D|BoxCollider|CircleCollider|PolygonCollider|CapsuleCollider|Brutal_Box2D'),
 ('audio', r'Audio|SdlSounds|MacAudioOutput|Brutal_Sdl_Mixer|SdlMixer|RhythmTiming'),
 ('ui', r'ScreenUi|Ui[A-Z]|UiTheme|RichText|SdlText|TtfText|Brutal_Sdl_Ttf|SdlTtf|GameLocalization|Localization'),
 ('tilemap', r'Tilemap|TileGrid|TileWorld|Tileset|TileDefinition|TilePalette|RuntimeTiles|SoftwareTileTextures|TileChunk|RenderGrid'),
 ('skeleton', r'Skeleton|Rig[A-Z]|RigBone|SkeletonPose'),
 ('fx', r'Particle|PointLight|Line2D|Lines2D|Trail2D|Effects2D|ScreenEffects|ShaderClip|SpriteShader|BakedShader|ShaderSheets'),
 ('cutscene', r'Cutscene|MotionPath|PathFollower|Tween2D|ComponentAnimation'),
 ('navigation', r'Navigation|GridNavigation|GridTurns|MazeLayout|GridRunRules'),
 ('replay/crash/profile', r'InputReplay|ReplayInput|ReplayCapture|PlayerCrashReport|CrashFrame|CrashDiagnostics|FrameProfile|PlayDiagnostics'),
 ('mods/updates/bundles', r'GameMods|ContentUpdates|ContentBundle|BundleMountCache'),
]
pat = re.compile(r'<MethodCode Name="([^"]*)" Length="(\d+)"')
sums = collections.Counter(); total = 0
for line in open(sys.argv[1], encoding='utf-8'):
    m = pat.search(line)
    if not m: continue
    name, n = m.group(1), int(m.group(2))
    if not name.startswith(('Engine_', 'Brutal_')): continue
    total += n
    for g, rx in groups:
        if re.search(rx, name):
            sums[g] += n; break
    else: sums['(other engine)'] += n
print('engine+brutal method code', total)
for k, v in sums.most_common(): print(f'{v:9d} {v/1024:7.0f} KiB {k}')
