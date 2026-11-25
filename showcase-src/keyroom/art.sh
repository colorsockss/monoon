#!/bin/sh
# Rebuilds samples/KeyRoom/Assets/Showcase from the generated sheets in this folder.  art.sh <path to pix>
# Scales are source pixels per art pixel, measured with `pix grid`; every PNG comes out at 2 px per art pixel.
set -e
cd "$(dirname "$0")"; pix=$1; out=../../samples/KeyRoom/Assets/Showcase; pal=../palette.txt
mkdir -p $out

# Hero: 3 rows (facing S, N, E) × 4 walk frames. Fixed 240×340 boxes on one baseline per row keep the frames registered.
row=0
for d in S N E; do
  y=$((56 + row * 384)); i=1
  for x in 54 350 654 954; do "$pix" tex herowalk.png $out/HeroWalk$d$i.png 21 30 $pal $x $y 240 340; i=$((i + 1)); done
  cp $out/HeroWalk${d}4.png $out/HeroIdle$d.png          # frame 4 stands with the feet together
  row=$((row + 1))
done
for f in Walk1 Walk2 Walk3 Walk4 Idle; do                # west = east mirrored
  n=$(echo $f | sed 's/\([0-9]\)$/W\1/; s/Idle$/IdleW/; s/^Walk\([0-9]\)W/WalkW/'); e=$(echo $f | sed 's/Walk\([0-9]\)/WalkE\1/; s/Idle/IdleE/')
  sips -f horizontal $out/Hero$e.png --out $out/Hero$(echo $e | sed 's/E/W/').png >/dev/null
done

prop() { "$pix" sprite $1 $out/$2.png @$3 - $pal $4 $5 $6 $7; }
prop props.png Barrel 11.5 84 120 204 264;   prop props.png Pot 11.5 672 132 216 240
prop props.png PotSmall 11.5 1008 192 144 180; prop props.png Chest 11.5 60 528 240 216;    prop props.png ChestOpen 11.5 372 492 216 252
prop props.png Key 11.5 684 576 192 108;     prop props.png Coin 11.5 1032 588 96 84
prop props2.png Plate 11.5 84 552 312 156;   prop props2.png Rug 11.5 852 528 336 204
# Crates: front view like the chest and barrel (the props sheets drew them at an angle).
prop crates.png Crate 18 216 444 336 348;    prop crates.png PushCrate 18 708 444 324 348
prop props2.png CoinPile 11.5 528 936 180 132
# Door, shut and open, on one 33×33 canvas so swapping the sprite does not stretch it. Drawn larger than the sheet scale: it must be taller than the hero.
"$pix" tex props.png $out/Door.png 33 33 $pal 608 848 285 285;  "$pix" tex props.png $out/DoorOpen.png 33 33 $pal 928 848 285 285
"$pix" mirrorx $out/Door.png $out/Door.png; "$pix" mirrorx $out/DoorOpen.png $out/DoorOpen.png   # the generated handles differ left and right
# Torch: the generator moved the sconce a little between its three frames, so the sconce is frame 2's alone and only the
# flame animates. Each flame is cut on its own sconce's centre line (x 240, 627, 1013 in the sheet).
"$pix" tex props2.png $out/TorchSconce.png 16 11 $pal 537 245 180 124
# Flame: the generated frames lean far left and right, which flapped when cycled. Only the upright one (frame 2) is used,
# and two more frames lean its tip by one art pixel each way for a gentle flicker.
"$pix" tex props2.png $out/TorchFlame1.png 16 16 $pal 537 65 180 180
"$pix" sway $out/TorchFlame1.png $out/TorchFlame2.png 6 -1; "$pix" sway $out/TorchFlame1.png $out/TorchFlame3.png 6 1

PIX_GAIN=0.72 "$pix" tiles floor.png $out/Floor.png 128 128 $pal 16 16 0 -3            # 8×8 flagstones, 16 art px each, darker than the walls
"$pix" tiles wallface.png $out/WallFace.png 96 32 $pal 24 8 12 99 0 0 1254 627          # four courses of 24×8 bricks = two tiles high
"$pix" tiles wallcap.png $out/WallCap.png 64 16 $pal 16 16 0 2 0 0 627 157              # four cap blocks
