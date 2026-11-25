#!/bin/sh
# Rebuilds samples/PlatformerLab/Assets/Showcase from the generated sheets in this folder.  art.sh <pix> <spr>
# One art pixel is one world unit here (the camera zooms 2×); every PNG is stored at 2 px per art pixel.
# Scales are source pixels per art pixel, measured with `pix grid`. Boxes come from `pix boxes`.
set -e
cd "$(dirname "$0")"; pix=$1; spr=$2; out=../../samples/PlatformerLab/Assets/Showcase; pal=palette.txt; tmp=$(mktemp -d)
mkdir -p $out
[ -f $pal ] || "$pix" palette concept.png $pal 48 props.png guard.png dirt.png sky.png ../keyroom/herowalk.png

# Hero: the KeyRoom hero's side-view walk (row 3 of its sheet), four frames on one baseline; frame 4 stands for idle.
# West is east mirrored. (No skeletal rig or foot IK in the showcase: user decision 2026-11-25.)
i=1; for x in 54 350 654 954; do
  "$pix" tex ../keyroom/herowalk.png $out/HeroWalkE$i.png 21 30 $pal $x 824 240 340
  sips -f horizontal $out/HeroWalkE$i.png --out $out/HeroWalkW$i.png > /dev/null; i=$((i + 1))
done
cp $out/HeroWalkE4.png $out/HeroIdleE.png; cp $out/HeroWalkW4.png $out/HeroIdleW.png
# Guard: four walk frames cut on one baseline.
i=1; for x in 42 354 654 954; do "$pix" tex guard.png $out/Guard$i.png 21 35 $pal $x 436 240 404; sips -f horizontal $out/Guard$i.png --out $out/GuardW$i.png > /dev/null; i=$((i + 1)); done

prop() { "$pix" sprite props.png $3 @$2 - $pal $4 $5 $6 $7 > /dev/null; echo "$1"; }
prop deck 11.5 $tmp/deck.png 0 192 336 120;    "$pix" slice9 $tmp/deck.png $out/Deck.png 100 10 6 0 6 0
prop post 11.5 $tmp/post.png 396 84 168 288
prop raft 11.5 $tmp/raft.png 612 156 324 192;  "$pix" slice9 $tmp/raft.png $out/Raft.png 118 17 7 0 7 0
prop beam 11.5 $tmp/beam.png 576 600 360 96;   "$pix" slice9 $tmp/beam.png $out/Beam.png 116 8 6 0 6 0
"$pix" sprite props.png $out/Ball.png 22 22 $pal 36 516 240 252
"$pix" sprite props.png $out/ChainLink.png @11.5 - $pal 384 480 108 118
"$pix" sprite props.png $out/Pennant.png @11.5 - $pal 996 468 228 348
"$pix" sprite props.png $out/Sign.png @11.5 - $pal 960 912 264 300
# Deck legs: the post's upright, repeated down to the ground.
"$pix" tex props.png $tmp/upright.png 6 25 $pal 396 84 70 288
"$pix" slice9 $tmp/upright.png $out/DeckLeg.png 6 80 0 2 0 2; "$pix" slice9 $tmp/upright.png $out/BeamPost.png 6 150 0 2 0 2
"$pix" tex $out/Deck.png $tmp/plank.png 16 10 $pal 40 0 32 20; "$pix" slice9 $tmp/plank.png $out/Ledge.png 16 6 0 3 0 3   # one seamless tile of the jump-through ledge (from the deck's middle)
# Torch: the sconce from frame 1 alone; each flame cut on its own post's centre (x 139, 454, 728).
"$pix" tex props.png $out/TorchPost.png 23 16 $pal 9 1030 260 180
# Flame: KeyRoom's upright flame (same 3-game-pixel art pixel; this sheet's flames were small and ragged). Frames 2 and 3
# lean its tip one art pixel each way, for a gentle flicker instead of the generated left/right frames' flapping.
"$pix" tex flame-source.png $out/TorchFlame1.png 16 16 $pal 537 65 180 180
"$pix" sway $out/TorchFlame1.png $out/TorchFlame2.png 6 -1; "$pix" sway $out/TorchFlame1.png $out/TorchFlame3.png 6 1

# Terrain from the dirt texture: an edge-masked autotile sheet (16 art px tiles), the incline and the landing pillar.
"$spr" auto dirt.png $tmp/auto.png 32 32 2 > /dev/null;     "$pix" tex $tmp/auto.png $out/Ground.png 64 64 $pal
"$spr" slope dirt.png $tmp/slope.png 380 180 2 > /dev/null; "$pix" tex $tmp/slope.png $out/Incline.png 190 90 $pal
"$spr" block dirt.png $tmp/pillar.png 132 360 2 1 > /dev/null; "$pix" tex $tmp/pillar.png $out/Pillar.png 66 180 $pal
"$pix" tex sky.png $out/Sky.png 456 228 $pal
rm -rf $tmp
