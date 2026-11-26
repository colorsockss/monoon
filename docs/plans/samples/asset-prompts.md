# 샘플 아트 생성 기록

Codex 내장 이미지 생성기를 사용했다. 시트는 스프라이트별로 잘라 PNG로 저장하고 게임의 픽셀 크기에 맞춰 줄였다. 생성 원본의 크기는 게임에 필요한 최종 에셋 용량에 포함되지 않는다.

## LedgeRun 스프라이트 시트

Use case: asset-texture. Asset type: source sprite atlas for an original small side-scrolling video game named LedgeRun. Generate one orthographic pixel art spritesheet set on genuine transparent alpha, landscape 4:3 with exactly four equal columns by three equal rows. Keep each object centered in its own grid cell with ample generous transparent gutters, except the grass and rock material samples which should fill a square patch within their cell. Rows from top to bottom, left to right: (1) tiny original auburn-haired trail courier, teal jacket and rust scarf, standing in side profile facing right; same courier running in side profile; same courier jumping; a small round flat gold trail token. (2) stone flagpole base with turquoise pennant blowing right; small light ochre faceted gem; small rounded dark-purple beetle enemy in side view; amber round ember projectile. (3) square meadow earth tile with straight horizontal golden green turf across top and brown rock below; square fully brown earth/stone interior tile; square dark violet cave masonry and copper highlights; square blue-gray tower masonry and ochre highlights. Bold readable 16-bit pixel clusters, hand-placed looking crisp edges, no gradients, no gradients in the alpha, compact palettes, no outlines dividing atlas cells, no text, no lettering, no logos. Each silhouette separate, no visual crossing of cells. Asset motifs must be original. Treat it as cropped individual sprites, not a game screenshot. Do not add floor under characters or other props.

## LedgeRun 초원 원경

Use case: illustration-story. Asset type: one full-bleed parallax background layer for a small original side-scrolling pixel art platformer. Draw a serene golden-hour highland meadow vista, as a distant backdrop viewed horizontally from the side, extremely wide 16:9 composition. Large clear warm pale blue sky in the upper half with a few warm cloud clusters, distant lavender blue alpine silhouettes at center height, rounded moss-green foothills and dark teal conifers only in the bottom third. The sides and horizon must join cleanly as a horizontal repeat, same luminosity and horizon height at both edges. Crisp restrained 16-bit pixel art, broad readable color clusters, medium low contrast so gameplay sprites stand out, warm desaturated sunlight. No players, no platforms, no interface, no text, no logos, no foreground objects, no borders.

## IsoBand 시트

Use case: illustration-story. Asset type: game source spritesheet for an original isometric party action RPG, genuine transparent background, precisely four columns and four rows of equal cells, no borders. Center each separate sprite in its own cell with generous empty transparent margins. Four even rows left to right: (1) four low 2:1 isometric ground diamond tiles: moss meadow turf with pale flecks; warm limestone flagstones; calm deep teal water; dark plum dungeon brick. No visible side thickness on this row, just top face of each diamond. (2) standing full-body three-quarter chibi pixel art original fantasy party: female captain with deep teal jacket and silver saber; broad amber-cloaked shield bearer with round shield; chestnut hair ivory-cloaked archer; dark-skinned mint-cloaked healer with pale jade staff. (3) four separate original isometric creatures: round red-orange horned marsh sprite; dark lilac robed wisp with floating turquoise eyes; stocky purple ochre cave brute; imposing slate stag guardian with bright amber antlers. (4) four separate original scenery objects: full stout pine; crumbled gray square stone pillar; small glimmering turquoise portal arch; tiny peach orange campfire. Cozy adventuring feel, restrained saturated colors, hard crisp readable game pixels, 16-bit handheld videogame scale, limited shading, no words, logos, grid lines, extra tiles, scenery behind objects, or shadows extending between cells. Same visual scale and consistent high three-quarter angle.

## HexRealm 솔트 해안

해상 지도와 도색한 보드게임 말을 기준으로 자체 팔레트를 만들었다. 청회녹색 바다 위에 소금기 어린 해안, 이끼색 평야, 청록 전나무, 눈 덮인 산맥, 구릿빛 동부 구릉을 배치했다. 조각은 아군 청록과 상대 주홍으로 구분하며 동일한 실루엣에서 발, 망토, 무기 위치를 바꿔 프레임을 그렸다.

[samples/HexRealm/Tools/art.py](../../../samples/HexRealm/Tools/art.py)의 좌표와 시드가 그림의 원본이다. 파이썬으로 그린 48×56 지형 변형, 길·해안·국경 조각, 지도 바탕과 UI 장식을 PNG로 묶는다. Idle/Walk/Strike의 네 프레임을 말 하나당 시트 한 장에 넣는다. 별도의 6프레임 시트에 타격 섬광과 전사 연기를 그려 같은 애니메이터로 재생한다. 미탐험 해역과 육지를 청회색 양피지 섬유 무늬로 덮는다. 선택·이동·타격·건설·턴 전환·승리 효과음은 16 kHz 모노 PCM으로 합성한다.

지도 제작자의 책상에는 밝은 36×32 양피지 9-slice와 세피아 글자, 창작 밀랍 인장, 청록·주홍 리본을 쓴다. 국경 애니메이션은 48×56 한 칸을 채우며 번지는 잉크 8프레임으로 그렸다. 글꼴은 저장소의 GowunDodum-Regular를 SDL_ttf로 그린다.

```sh
python3 -B samples/HexRealm/Tools/build_board.py
```
