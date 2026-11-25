#!/bin/sh
# Rebuilds samples/AnimatorLab/Assets/Showcase: the KeyRoom hero's walk in 8 directions (its N/E/S/W frames plus the
# generated diagonals in diagonal.png, mirrored for the west side), the guard and the training dummy from guard.png,
# and the herb yard's grass and props. Every frame is cut from a fixed box on one baseline so the clips do not jitter.
# art.sh <pix>
set -e
cd "$(dirname "$0")"; pix=$1; out=../../samples/AnimatorLab/Assets/Showcase; pal=palette.txt
kr=../../samples/KeyRoom/Assets/Showcase; yard=../../samples/InventoryLab/Assets/Showcase
mkdir -p $out
for d in N E S W; do cp $kr/HeroIdle$d.png $out/; for i in 1 2 3 4; do cp $kr/HeroWalk$d$i.png $out/; done; done
cp $kr/ShadowM.png $kr/ShadowL.png $kr/Barrel.png $kr/Crate.png $out/
for f in Grass Fence Bush Flowers Herb1 Herb2 Well; do cp $yard/$f.png $out/; done
cp ../../samples/PlatformerLab/Assets/Ui/UiShade.png $out/Panel.png
# Diagonals: 266×380 boxes (the hero's 21:30) centred on each frame, bottoms on the row baseline.
i=1; for cx in 270 678 1086 1494; do
  "$pix" tex diagonal.png $out/HeroWalkSE$i.png 21 30 $pal $((cx - 133)) 28 266 380 > /dev/null
  "$pix" tex diagonal.png $out/HeroWalkNE$i.png 21 30 $pal $((cx - 133)) 472 266 380 > /dev/null
  sips -f horizontal $out/HeroWalkSE$i.png --out $out/HeroWalkSW$i.png > /dev/null
  sips -f horizontal $out/HeroWalkNE$i.png --out $out/HeroWalkNW$i.png > /dev/null
  i=$((i + 1)); done
# Guard: 384×326 boxes centred on the sheet's columns (9.6 source px per art px), 40×34 art pixels.
g() { "$pix" tex guard.png $out/$1.png 40 34 $pal $2 $3 384 326 > /dev/null; }
g GuardIdle 9 26;  g GuardWindup 347 26;  g GuardSwing 709 26;  g GuardFollow 1055 26
g GuardHurt 9 374; g GuardWalk1 347 374;  g GuardWalk2 709 374
# Training dummy: 256×326 boxes, 27×34 art pixels.
d() { "$pix" tex guard.png $out/$1.png 27 34 $pal $2 734 256 326 > /dev/null; }
d DummyUp 58; d DummyBack 412; d DummyForward 742
echo done
