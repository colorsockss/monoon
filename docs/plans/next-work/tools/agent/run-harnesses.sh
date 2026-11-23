#!/bin/sh
# Usage: run-harnesses.sh <checkout> <result-root> [harness...]  — runs every editor.py harness (editor part only), each under gui-lock.
S=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad
LOCK="sh $S/gui-lock.sh"
cd "$1" || exit 2
R=$2; shift 2
mkdir -p "$R"
E=editor/MoNoon.app/Contents/MacOS/MoNoon
P=engine/.artifacts/bin
all="AudioLab.Vorbis BarrageLab.Lines CollisionLab.FrameProfiler CollisionLab.SplitLoading InventoryLab.RichText KeyRoom.ComponentKeys KeyRoom.CopyPaste KeyRoom.EditorDocking KeyRoom.EditorExtensions KeyRoom.EditorPanelsDetail KeyRoom.EditorTheme KeyRoom.FieldRename KeyRoom.PrefabStructure KeyRoom.Rebinding KeyRoom.ScriptReload KeyRoom.ScriptWaits PlatformerLab.Joints PlatformerLab.Paths PlatformerLab.TileCollision Samples.GameText SeedLab.GridNavigation SeedLab.TileTerrain SpriteLab.Aseprite"
[ $# -gt 0 ] && all="$*"
probe() { case $1 in
  AudioLab.Vorbis) echo AudioProbe;; BarrageLab.Lines|PlatformerLab.Paths) echo PathsProbe;; CollisionLab.FrameProfiler) echo ProfilerProbe;;
  CollisionLab.SplitLoading) echo SplitLoadingProbe;; InventoryLab.RichText) echo RichTextProbe;; KeyRoom.ComponentKeys) echo ComponentKeysProbe;;
  KeyRoom.CopyPaste) echo CopyPasteProbe;; KeyRoom.EditorDocking) echo DockingProbe;; KeyRoom.EditorExtensions) echo ExtensionsProbe;;
  KeyRoom.EditorPanelsDetail) echo PanelsDetailProbe;; KeyRoom.EditorTheme) echo EditorThemeProbe;; KeyRoom.FieldRename) echo FieldRenameProbe;;
  KeyRoom.PrefabStructure) echo PrefabStructureProbe;; KeyRoom.Rebinding|KeyRoom.ScriptWaits) echo EditorProbe;; KeyRoom.ScriptReload) echo ReloadProbe;;
  PlatformerLab.Joints) echo JointsProbe;; PlatformerLab.TileCollision) echo TileContactProbe;; Samples.GameText) echo GameTextProbe;;
  SeedLab.GridNavigation) echo RouteProbe;; SeedLab.TileTerrain) echo TerrainProbe;; SpriteLab.Aseprite) echo AsepriteProbe;; esac; }
sample() { case $1 in AudioLab*) echo AudioLab;; BarrageLab*) echo BarrageLab;; CollisionLab*) echo CollisionLab;; InventoryLab*) echo InventoryLab;;
  KeyRoom*) echo KeyRoom;; PlatformerLab*) echo PlatformerLab;; SeedLab*) echo SeedLab;; SpriteLab*) echo SpriteLab;; esac; }
for h in $all; do
  qa=engine/tests/$h; w=$R/$h; rm -rf "$w"; mkdir -p "$w"; dll=$P/$(probe $h)/release/$(probe $h).dll; s=$(sample $h)
  start=$(date +%s)
  (
  set -e
  case $h in KeyRoom.Rebinding|KeyRoom.ScriptWaits) dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts= -p:NuGetAudit=false -v:q -nologo;; esac
  case $h in
    AudioLab.Vorbis) cp -R samples/AudioLab "$w/AudioLab"; $LOCK python3 $qa/editor.py $E "$w/AudioLab" $dll "$w/scene-play";;
    CollisionLab.FrameProfiler) $LOCK python3 $qa/editor.py $E samples/CollisionLab $dll "$w/scene-play";;
    CollisionLab.SplitLoading) python3 $qa/replay.py prepare "$w/CollisionLab"; $LOCK python3 $qa/editor.py $E "$w/CollisionLab" $dll "$w/scene-play" 3;;
    KeyRoom.EditorDocking|KeyRoom.EditorExtensions|KeyRoom.EditorPanelsDetail|KeyRoom.EditorTheme|KeyRoom.ScriptReload)
      $LOCK python3 $qa/editor.py $E samples/KeyRoom $dll "$w/out";;
    Samples.GameText) for g in KeyRoom PlatformerLab; do mkdir -p "$w/projects"; rsync -a --exclude .monoon samples/$g "$w/projects/";
      $LOCK python3 $qa/editor.py $g $E "$w/projects/$g" $dll "$w/runs/$g/scene-play"; done;;
    *) python3 $qa/verify.py prepare samples/$s "$w/$s"; $LOCK python3 $qa/editor.py $E "$w/$s" $dll "$w/scene-play";;
  esac
  ) > "$w/run.log" 2>&1
  code=$?
  echo "$h $code $(( $(date +%s) - start ))s" | tee -a "$R/summary.txt"
done
