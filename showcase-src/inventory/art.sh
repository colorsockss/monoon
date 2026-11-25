#!/bin/sh
# Rebuilds samples/InventoryLab/Assets/Showcase: the herb yard props from yard.png, the herbalist portrait, grass, and the
# KeyRoom showcase hero (same art).  art.sh <pix>
set -e
cd "$(dirname "$0")"; pix=$1; out=../../samples/InventoryLab/Assets/Showcase; pal=palette.txt
src=../../samples/KeyRoom/Assets/Showcase
mkdir -p $out
for d in N E S W; do cp $src/HeroIdle$d.png $out/; for i in 1 2 3 4; do cp $src/HeroWalk$d$i.png $out/; done; done
cp $src/ShadowM.png $src/ShadowL.png $out/
cp ../../samples/PlatformerLab/Assets/Ui/UiShade.png $out/Panel.png   # the HUD panel frame (9-slice, borders 4)
p() { "$pix" sprite yard.png $out/$1.png @$2 - $pal $3 $4 $5 $6 > /dev/null; echo $1; }
p Stall 9 35 84 488 467;          p Herbalist 10.5 585 188 223 362;  p Well 9 898 195 300 362
p Herb1 10.5 139 683 202 174;     p Herb2 10.5 529 669 181 188;      p Herb3 10.5 940 669 153 188
p Fence 10 70 989 348 174;        p Bush 10 508 968 258 209;         p Flowers 10 898 989 272 181
"$pix" tex portrait.png $out/Portrait.png 57 57 $pal > /dev/null; echo Portrait
"$pix" tex ../grass_src.png $out/Grass.png 128 128 $pal > /dev/null; echo Grass
