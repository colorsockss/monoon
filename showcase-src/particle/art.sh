#!/bin/sh
# Rebuilds samples/ParticleLab/Assets/Showcase: camp props from props.png, plus the PlatformerLab showcase's ground, sky,
# moon and hero walk (same art, same palette).  art.sh <pix>
set -e
cd "$(dirname "$0")"; pix=$1; out=../../samples/ParticleLab/Assets/Showcase; pal=../platformer/palette.txt
src=../../samples/PlatformerLab/Assets/Showcase
mkdir -p $out
for f in Ground.png Sky.png Moon.png HeroIdleE.png HeroIdleW.png HeroWalkE1.png HeroWalkE2.png HeroWalkE3.png HeroWalkE4.png \
         HeroWalkW1.png HeroWalkW2.png HeroWalkW3.png HeroWalkW4.png; do cp $src/$f $out/$f; done
cp ../../samples/PlatformerLab/Assets/Ui/UiShade.png $out/Panel.png   # the HUD panel frame (9-slice, borders 4)
prop() { "$pix" sprite props.png $out/$1.png @11.5 - $pal $2 $3 $4 $5 > /dev/null; echo $1; }
prop Campfire 60 168 312 204
# Tent, cloud and portal at exactly twice the sheet scale (5.75 = 11.5 / 2): the sheet drew them smaller than the hero.
big() { "$pix" sprite props.png $out/$1.png @5.75 - $pal $2 $3 $4 $5 > /dev/null; echo $1; }
big Tent 432 96 384 276;       big Cloud 876 108 336 180   # (the cloud without its drawn drops: the particles rain)
prop Fountain 36 516 360 276;  prop Launcher 468 504 288 300; big Portal 828 444 384 360
prop Bench 48 960 360 228;     prop Pine 504 876 252 312;  prop Lantern 900 888 228 300
