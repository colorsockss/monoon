# 샘플 아트 생성 기록

Codex 내장 이미지 생성기를 사용했다. 시트는 스프라이트별로 잘라 PNG로 저장하고 게임의 픽셀 크기에 맞춰 줄였다. 생성 원본의 크기는 게임에 필요한 최종 에셋 용량에 포함되지 않는다.

## LedgeRun 스프라이트 시트

Use case: asset-texture. Asset type: source sprite atlas for an original small side-scrolling video game named LedgeRun. Generate one orthographic pixel art spritesheet set on genuine transparent alpha, landscape 4:3 with exactly four equal columns by three equal rows. Keep each object centered in its own grid cell with ample generous transparent gutters, except the grass and rock material samples which should fill a square patch within their cell. Rows from top to bottom, left to right: (1) tiny original auburn-haired trail courier, teal jacket and rust scarf, standing in side profile facing right; same courier running in side profile; same courier jumping; a small round flat gold trail token. (2) stone flagpole base with turquoise pennant blowing right; small light ochre faceted gem; small rounded dark-purple beetle enemy in side view; amber round ember projectile. (3) square meadow earth tile with straight horizontal golden green turf across top and brown rock below; square fully brown earth/stone interior tile; square dark violet cave masonry and copper highlights; square blue-gray tower masonry and ochre highlights. Bold readable 16-bit pixel clusters, hand-placed looking crisp edges, no gradients, no gradients in the alpha, compact palettes, no outlines dividing atlas cells, no text, no lettering, no logos. Each silhouette separate, no visual crossing of cells. Asset motifs must be original. Treat it as cropped individual sprites, not a game screenshot. Do not add floor under characters or other props.

## LedgeRun 초원 원경

Use case: illustration-story. Asset type: one full-bleed parallax background layer for a small original side-scrolling pixel art platformer. Draw a serene golden-hour highland meadow vista, as a distant backdrop viewed horizontally from the side, extremely wide 16:9 composition. Large clear warm pale blue sky in the upper half with a few warm cloud clusters, distant lavender blue alpine silhouettes at center height, rounded moss-green foothills and dark teal conifers only in the bottom third. The sides and horizon must join cleanly as a horizontal repeat, same luminosity and horizon height at both edges. Crisp restrained 16-bit pixel art, broad readable color clusters, medium low contrast so gameplay sprites stand out, warm desaturated sunlight. No players, no platforms, no interface, no text, no logos, no foreground objects, no borders.

## LedgeRun 리메이크 픽셀 아트

기존 초원 원경과 흙·석재의 픽셀 질감을 바탕으로 [샘플의 파이썬 아트 스크립트](../../../samples/LedgeRun/Art/draw.py)에서 새 시트와 PNG를 생성했다. Courier는 적갈색 머리, 산호색 스카프, 청록 코트를 고정하고, 팔·다리 관절과 스카프 실루엣을 프레임마다 바꿨다. 딱정벌레는 보라색 외피, 정상 Warden은 청회색 외피와 황동 볏으로 구분했다. 토큰과 깃발의 밝은 면은 원근과 펄럭임에 맞춰 움직인다.

동굴은 포도색 암벽과 청록 수정, 탑은 낮은 채도의 회청색 석조 아치, 정상은 노을과 황동 신호기를 사용한다. 절벽·발판의 밝은 모서리는 점프 위치를 읽는 기준이다. UI는 어두운 9-slice 패널과 따뜻한 아이콘, 청록색 보조 텍스트로 통일했다. PNG는 게임 픽셀 크기에서 만들고 애니메이션의 같은 소재를 인덱스 색상 시트로 묶었다. 발소리·대시·밟기·예고음은 16 kHz 모노 파형을 합성했다.

2차에서는 초원에 뿌리가 있는 잔디 표면과 흙 속 자갈, 바위·덤불·들풀을 더했다. 골짜기 그림자는 시작점에서 옅게 들어와 아래쪽에서 어두워진다. 탑 장식은 노을빛 아치 창과 담쟁이, 황동 승강기와 이동 레일이다. 보석 시트에는 간헐적으로 짧은 별 모양 반짝임을 넣었다.

3차에서는 Warden의 낮은 돌진 자세, 황동색 바닥 충격파와 왕관 파편을 같은 픽셀 팔레트로 구성했다. 돌진은 기존 Warden 스프라이트를 눌러 바닥에 붙이고, 충격파는 23×11px PNG를 좌우로 뒤집어 사용한다.

### 4차 Courier 시안

고글을 단 비행 배달부, 챙 모자를 쓴 산길 우편배달부, 깃 장식을 단 달리기 선수의 세 시안을 만들었다. 산길 우편배달부를 골랐다. 모자의 긴 챙과 몸 옆의 큰 가방은 실루엣만으로 구분되고, 가방 밖으로 나온 봉인 편지는 역할을 바로 보여 준다. 노란 모자는 동굴과 탑에서, 산호색 스카프는 초원과 노을 배경에서 움직임을 읽는 데 도움이 된다. [Courier 시안 비교](../../../engine/tests/results/ledgerun/courier-concepts.png)

기준 자세의 몸통·부츠·편지 가방·머리를 공유하고 팔다리와 스카프 좌표를 바꿔 6프레임 대기와 8프레임 달리기를 만들었다. 같은 팔레트와 1px 외곽선으로 점프 준비·상승·정점·하강, 착지, 벽 미끄러짐, 대시, 피격, 밟기, 깃발 승리 자세를 그렸다. UI는 밝은 노랑과 하늘색, 2px 어두운 외곽선과 아래 그림자를 쓴다. 텍스트는 저장소의 고운돋움 TTF를 런타임에서 그린다. [픽셀 파츠와 포즈](../../../samples/LedgeRun/Art/arcade.py)

5차에서는 납작한 정수리, 짧은 챙, 봉투 휘장의 청록색 천 우편 모자를 골랐다. 짧은 챙은 얼굴을 덜 가리고 같은 청록색 윤곽을 타이틀 삽화와 결산 도장에서도 읽을 수 있다. 로고의 산길과 봉투는 64×42px PNG로 그리고, 워드마크의 외곽선과 그림자는 기존 고운돋움으로 그린다. [최종 애니메이션 시트](../../../engine/tests/results/ledgerun/courier-animation-4x.png)

## IsoBand 시트

Use case: illustration-story. Asset type: game source spritesheet for an original isometric party action RPG, genuine transparent background, precisely four columns and four rows of equal cells, no borders. Center each separate sprite in its own cell with generous empty transparent margins. Four even rows left to right: (1) four low 2:1 isometric ground diamond tiles: moss meadow turf with pale flecks; warm limestone flagstones; calm deep teal water; dark plum dungeon brick. No visible side thickness on this row, just top face of each diamond. (2) standing full-body three-quarter chibi pixel art original fantasy party: female captain with deep teal jacket and silver saber; broad amber-cloaked shield bearer with round shield; chestnut hair ivory-cloaked archer; dark-skinned mint-cloaked healer with pale jade staff. (3) four separate original isometric creatures: round red-orange horned marsh sprite; dark lilac robed wisp with floating turquoise eyes; stocky purple ochre cave brute; imposing slate stag guardian with bright amber antlers. (4) four separate original scenery objects: full stout pine; crumbled gray square stone pillar; small glimmering turquoise portal arch; tiny peach orange campfire. Cozy adventuring feel, restrained saturated colors, hard crisp readable game pixels, 16-bit handheld videogame scale, limited shading, no words, logos, grid lines, extra tiles, scenery behind objects, or shadows extending between cells. Same visual scale and consistent high three-quarter angle.

### IsoBand 리메이크

같은 인물과 생물로 보이도록 원본의 몸통과 다리 픽셀을 분리해 대기·걷기·공격 프레임을 만들었다. 피격 흰색 실루엣과 낙하를 포함해 아군·적 각각을 팔레트 아틀라스로 묶었으며, 좌우는 같은 프레임을 반전한다. 64×32 지형, 노출된 둑 면, 건물·묘비·봉인, HUD와 파티 초상은 저장소의 제작 스크립트로 그렸다. Haven 건물은 64×32 타일과 같은 2:1 투영식으로 벽·처마·용마루·굴뚝을 계산한다. 배경 불씨와 안개는 팔레트 클립을 재생한다. 역할별 타격·회복·영혼과 Pulse 빛기둥은 엔진 파티클로 그리며 검과 깃발은 4프레임 시트를 공유한다. HUD는 작은 팔레트 PNG와 저장소의 GowunDodum-Regular TTF를 쓴다. 오디오 9종은 12 kHz 8비트 모노 PCM으로 합성했다. [아트 생성 스크립트](../../../samples/IsoBand/Tools/art.py) · [씬 배치](../../../samples/IsoBand/Tools/scenes.py)

## HexRealm 솔트 해안

해상 지도와 도색한 보드게임 말을 기준으로 자체 팔레트를 만들었다. 청회녹색 바다 위에 소금기 어린 해안, 이끼색 평야, 청록 전나무, 눈 덮인 산맥, 구릿빛 동부 구릉을 배치했다. 조각은 아군 청록과 상대 주홍으로 구분하며 동일한 실루엣에서 발, 망토, 무기 위치를 바꿔 프레임을 그렸다.

[samples/HexRealm/Tools/art.py](../../../samples/HexRealm/Tools/art.py)의 좌표와 시드가 그림의 원본이다. 파이썬으로 그린 48×56 지형 변형, 길·해안·국경 조각, 지도 바탕과 UI 장식을 PNG로 묶는다. Idle/Walk/Strike의 네 프레임을 말 하나당 시트 한 장에 넣는다. 별도의 6프레임 시트에 타격 섬광과 전사 연기를 그려 같은 애니메이터로 재생한다. 미탐험 해역과 육지를 청회색 양피지 섬유 무늬로 덮는다. 선택·이동·타격·건설·턴 전환·승리 효과음은 16 kHz 모노 PCM으로 합성한다.

지도 제작자의 책상에는 밝은 36×32 양피지 9-slice와 세피아 글자, 창작 밀랍 인장, 청록·주홍 리본을 쓴다. 국경 애니메이션은 48×56 한 칸을 채우며 번지는 잉크 8프레임으로 그렸다. 글꼴은 저장소의 GowunDodum-Regular를 SDL_ttf로 그린다.

```sh
python3 -B samples/HexRealm/Tools/build_board.py
```
