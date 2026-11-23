#!/bin/sh
# Mac side of the phase1 Linux x64 guest run (cwd-independent). Templates/kit come from build-players.sh (basics step).
set -eux
G=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/guest
cd /Users/paare/codex_project/brutal_engine/.worktrees/phase1
python3 engine/scripts/package-editor.py linux-x64
python3 engine/scripts/package-editor.py keyroom
dotnet build engine/src/Engine.Cli -c Release -m:1 -p:NuGetAudit=false
# Probes one by one, each DLL copied at once (EditorProbe is a shared output name).
rm -rf $G/probes; mkdir -p $G/probes
for pair in PlatformerLab.Joints:JointsProbe KeyRoom.ScriptWaits:EditorProbe KeyRoom.ScriptReload:ReloadProbe InventoryLab.RichText:RichTextProbe \
            PlatformerLab.Paths:PathsProbe SpriteLab.Aseprite:AsepriteProbe KeyRoom.EditorExtensions:ExtensionsProbe KeyRoom.CopyPaste:CopyPasteProbe \
            PlatformerLab.TileCollision:TileContactProbe Samples.GameText:GameTextProbe; do
  h=${pair%%:*}; p=${pair##*:}
  dotnet build engine/tests/$h -c Release -m:1 -p:UsedAvaloniaProducts= -p:NuGetAudit=false
  mkdir -p $G/probes/$h; cp engine/.artifacts/bin/$p/release/$p.dll $G/probes/$h/
done
# 11.12 packages (KeyRoom copy with an icon, as in the chapter-11 run).
rm -rf $G/pkg; mkdir -p $G/pkg
rsync -a --exclude .monoon samples/KeyRoom $G/pkg/ && echo 'icon: fb750913-c06a-50c4-beae-cb27d559af91' >> $G/pkg/KeyRoom/GameSettings
rsync -a --exclude .monoon samples/InventoryLab $G/pkg/
for g in KeyRoom InventoryLab; do ./monoon build $G/pkg/$g release --target linux-x64 --profile distribution --package on --output $G/pkg/out-$g; done
python3 $G/pack.py
echo MAC-GUEST-BUILD-DONE
