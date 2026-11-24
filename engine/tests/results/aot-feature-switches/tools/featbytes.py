import sys, re, html, collections
# usage: featbytes.py <map.xml> [<map.xml> ...]: map bytes of nodes whose name matches each feature regex (features_cut.py's)
F = {
 'particles': r'Particle',
 'ui+text': r'ScreenUi|_Ui[A-Z]|UiTheme|RichText|SdlText|TtfText|Brutal_Sdl_Ttf|SdlTtf|Localiz|WorldText|Text2D|DrawDialogue|UpdateUi|Dialogue|Glyph',
 'lighting': r'Light2D|PointLight|GlobalLight|Shadow|DrawLighting|Lighting',
 'navigation': r'Navigation|NavMesh|NavAgent|GridNavigation|GridTurns|Maze|GridRun|Pathfind|NavGrid|NavArea|NavCrowd',
 'tween+cutscene+paths': r'Tween|Cutscene|MotionPath|PathFollower|ComponentAnimation|ComponentTrack|ComponentKey|Timeline',
 'audio': r'Audio|SdlSounds|Brutal_Sdl_Mixer|SdlMixer|Mixer|Rhythm|SyncAudio',
 'physics': r'Rigidbody|Joint2D|Joint_|CharacterMotor|Physics|Box2D|Collider|Contact|CastBody|SweepBody|QueryCast|AddShape|CreateBody|CreateJoint|MovePlatform|Effector|TileShape|AddTileShapes|TileCollision|CollisionGeometry|StepRigidBodies|SceneGame_Body',
 'tilemap': r'Tilemap|TileWorld|Tileset|TileDefinition|TileChunk|RuntimeTiles|SoftwareTileTextures|Autotile|AutoTile|TileAnimation|Terrain|UpdateTileWorlds|AddTileCandidates|TilePage|TileCell|TilePalette',
 'skeleton/rig': r'Skeleton|_Rig[A-Z_]|FootIK|FootChain|BoneKey|ApplySubtreePose',
 'lines/trails': r'Line2D|Lines2D|Trail2D|LineRenderer|TrailRenderer',
}
node = re.compile(r'<(\w+) Name="([^"]*)" Length="(\d+)"')
for mp in sys.argv[1:]:
    tot = collections.Counter(); allb = 0
    for line in open(mp, encoding='utf-8'):
        m = node.search(line)
        if not m: continue
        n, b = html.unescape(m.group(2)), int(m.group(3)); allb += b
        for f, rx in F.items():
            if re.search(rx, n): tot[f] += b
    print(mp.split('/')[-4], f'total {allb:,}', ' '.join(f'{f}={tot[f]:,}' for f in F))
