#!/bin/sh
# Rebuilds samples/BarrageLab/Assets/Showcase from ships.png and space.png (generated, see ../generation_prompts.txt).
# One art pixel = 2 world units (camera zoom 1).  art.sh <pix>
set -e
cd "$(dirname "$0")"; pix=$1; out=../../samples/BarrageLab/Assets/Showcase; pal=palette.txt
mkdir -p $out
s() { "$pix" sprite ships.png $out/$1.png @11 - $pal $2 $3 $4 $5 > /dev/null; echo $1; }
s Ship 56 98 376 237;    s DroneA 515 84 286 237;  s DroneB 906 63 293 286
"$pix" sprite ships.png $out/Boss.png @9.5 - $pal 56 411 1143 404 > /dev/null; echo Boss
s Asteroid 56 913 293 272
"$pix" white $out/DroneA.png $out/DroneAFlash.png; "$pix" white $out/DroneB.png $out/DroneBFlash.png
# Backdrop: 160×90 art pixels drawn at 5 world units each (coarser than the ships: it reads as far away).
"$pix" sprite space.png $out/Space.png 160 90 $pal > /dev/null; echo Space
cp ../../samples/PlatformerLab/Assets/Ui/UiShade.png $out/Panel.png   # the HUD panel frame (9-slice, borders 4)
