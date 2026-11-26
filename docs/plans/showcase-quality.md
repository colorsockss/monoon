# 시연 샘플 퀄리티업 계획 (2026-11-25)

시연 영상을 샘플마다 따로 찍고 플레이 구간만 잘라 저장하기 전에, 샘플 자체를 다시 정리하는 계획이다. 브랜치 `demo/showcase-art`(`04a2099`)와 1차 녹화본 8개의 프레임을 다시 보고 썼다. 아직 아무것도 고치지 않았다.

## 1. 결론

- **원인**: 지금 샘플은 기능을 검증하려고 만든 시험대다. 1차 작업은 그 시험대의 배치를 그대로 두고 그림만 바꿨다. 그래서 그림은 바뀌었지만 화면 구성, 크기, 움직임, 글자가 여전히 시험대다.
- **방향**: 그림 교체가 아니라 장면을 다시 짠다. 샘플마다 "보여줄 것 한 문장"을 먼저 정하고, 그 문장에 필요 없는 것은 화면에서 뺀다.
- **순서**: KeyRoom 한 화면으로 화풍 기준을 먼저 확정한다. 그 기준이 승인되면 1순위 5개, 2순위 4개 순으로 넓힌다.
- **촬영**: 샘플 하나에 영상 하나. Play를 누른 뒤부터 멈추기 전까지만 잘라 저장한다.

## 2. 지금 무엇이 문제인가

1차 녹화본에서 확인한 것만 적었다.

### 공통

| 문제 | 화면에서 보이는 모습 |
| --- | --- |
| 주인공이 너무 작다 | KeyRoom 플레이어가 800×450 화면에서 40px다. 화면 높이의 9%다. |
| 빈 면적이 크다 | KeyRoom은 같은 잔디 무늬가 화면의 80% 이상이다. ParticleLab·BarrageLab은 검은 바탕이 대부분이다. |
| 픽셀 크기가 제각각이다 | 생성한 원본을 스프라이트마다 다른 비율로 줄였다. 나무는 굵은 픽셀, 플레이어는 가는 픽셀이다. |
| 움직임이 없다 | 캐릭터가 한 장짜리 그림으로 미끄러진다. 줍기·열기·맞기에 반응이 없다. |
| 조명·그림자가 없다 | 물체가 바닥에 붙어 보이지 않는다. 엔진에 있는 2D 조명을 PlatformerLab 횃불 말고는 쓰지 않는다. |
| HUD가 디버그 출력이다 | `Chunk 2, -1  Active 3/9  Loaded 5`, `Slip 0.000 px`, `item-0705`, `Row objects 11 / Items 1000` 같은 줄이 화면을 덮는다. |
| 무엇을 보여주는지 안 쓰여 있다 | 기능 이름이 화면에 없다. BarrageLab의 묶음 제출과 개별 제출은 그림이 같아서 차이가 안 보인다. |
| 게임 화면이 창의 절반이다 | 에디터 창을 찍으면 Hierarchy·Inspector가 40%를 차지한다. Game 뷰가 Fit 배율이라 픽셀이 뭉개진다. |

### 샘플별

| 샘플 | 문제 |
| --- | --- |
| KeyRoom | 벽이 가는 띠 한 줄이다. 방처럼 보이지 않는다. 문이 순간적으로 열린다. 두 번째 방은 잔디밭에 상자 하나다. 안내 상자(`Compass / Exit east`, `Paused`)가 겹쳐 떠 있다. |
| PlatformerLab | 안내 상자 두 개가 화면의 40%를 가린다. 캐릭터(리그)와 지형의 화풍이 다르다. 핵심 기능인 발 IK가 멀어서 안 보인다. |
| ParticleLab | 7개 씬을 4초씩 넘겨서 읽을 시간이 없다. 효과가 작고, 어떤 모듈인지 이름표가 없다. 불·비·연기가 놓일 배경이 없다. |
| BarrageLab | 나선 탄막은 보기 좋다. 표적이 막대사탕 모양이고 맞아도 반응이 없다. 성능이 주제인데 프레임 시간 숫자가 화면에 없다. |
| InventoryLab | 화면 전체가 글자 패널이다. 세계도 인물도 없다. 대화 글자가 찍히는 중간에 컷이 넘어간다. |

Parallax는 PlatformerLab의 한 씬이라 PlatformerLab 영상 안에서 다룬다.

## 3. 품질 기준

샘플 하나를 끝냈다고 말하려면 아래를 모두 만족해야 한다. 녹화 뒤 프레임 시트로 확인한다.

| 기준 | 합격선 |
| --- | --- |
| 주제 | 시작 3초 안에 기능 이름과 한 줄 설명이 화면에 나온다. |
| 구성 | 한 영상에 장면 3개 이하, 20–40초. 장면마다 보여주는 기능이 하나다. |
| 크기 | 주인공 오브젝트가 화면 높이의 12% 이상이다. |
| 빈 면적 | 같은 무늬나 단색이 화면의 1/3을 넘지 않는다. |
| 화풍 | 모든 스프라이트가 같은 픽셀 크기와 같은 팔레트를 쓴다. |
| 움직임 | 움직이는 것에는 애니메이션이 있고, 줍기·충돌·완료에는 반응(입자, 흔들림, 튐)이 있다. |
| 글자 | 자막 한 줄과 조작 안내 한 줄까지만 둔다. 디버그 문자열은 없다. UI가 주제인 샘플은 예외다. |
| 숫자 | 성능이 주제면 프레임 시간이나 개수를 큰 숫자 하나로 보여준다. |

## 4. 공통 작업

### 화풍

- **픽셀 격자 하나로 통일**: 게임 해상도 800×450은 그대로 둔다. 모든 PNG는 아트 픽셀 1칸을 2px로 저장하고 타일은 16×16 아트 픽셀(32px)이다. 화면에서의 크기는 카메라 줌으로 정한다. KeyRoom은 줌 1.5라 아트 픽셀 1칸이 화면 3px이고, 캐릭터(17×28 아트 픽셀)는 화면 높이의 19%다.
- **팔레트 하나로 통일**: 생성한 그림을 모두 `showcase-src/palette.txt`(48색)로 줄인다. 변환 도구는 `showcase-src/pix.swift`다. 32색으로는 나무와 이끼 색이 뭉개져서 48색으로 늘렸다.
- **생성 방식**: 방 전체 콘셉트 그림 한 장(`showcase-src/keyroom/concept.png`)을 먼저 만들고, 그 그림을 `codex exec -i`의 참고 그림으로 넣어 캐릭터·소품·타일 시트를 뽑는다.
- **캐릭터 애니메이션**: 대기 2프레임, 걷기 4프레임을 시트로 만든다. 프레임 사이 모양이 흔들리면 못 쓴다. 그때는 한 장 그림에 엔진의 속성 키프레임(위아래 흔들림, 좌우 반전)을 쓴다.
- **바닥과 벽**: 한 장을 반복하는 방식 대신 타일맵과 오토타일을 쓴다. 풀·돌·금 같은 변형 타일과 소품을 섞는다.
- **깊이**: 물체 아래 둥근 그림자, 발 위치 정렬, Point Light 2D를 쓴다. 따뜻한 횃불빛이 필요한 씬은 Global Light 2D를 두지 않는다. Global Light 2D가 있으면 빛이 어둠을 원래 색까지만 되돌리고 주황빛을 더하지 못한다.

### 반응

모두 엔진에 이미 있는 기능이다. 꾸미는 일이 곧 기능 시연이 된다.

| 상황 | 쓰는 기능 |
| --- | --- |
| 줍기·맞기·완료 | 파티클 프리셋, 월드에 붙는 숫자 |
| 충돌·폭발 | 카메라 흔들림, 줌 펀치, 플래시 |
| 문·상자·UI 등장 | 트윈, 속성 키프레임 |
| 장면 전환 | 카메라 페이드 |

### 화면 글자

- 기존 디버그 HUD는 시연용 씬에서 뺀다. 원래 씬 파일은 그대로 두고 시연용 씬을 따로 만든다.
- 자막 띠 하나를 공용으로 만든다. 왼쪽 아래에 기능 이름과 한 줄 설명, 오른쪽 아래에 조작 키.
- 성능 숫자는 에디터 Game 뷰의 **Stats**(FPS, 프레임 시간, 그리기 호출 수)를 켜서 보여준다. 게임 스크립트에서 같은 값을 읽는 API는 찾지 못했다.

### 소리

1차 녹화는 무음이다. `screencapture`는 시스템 소리를 담지 못한다. RhythmLab과 AudioLab은 소리 없이는 의미가 없어서 3순위로 미뤘다. 소리를 담으려면 macOS ScreenCaptureKit을 쓰는 녹화 도구를 Swift로 새로 써야 한다. 설치는 필요 없다.

## 5. 영상 구성과 우선순위

샘플 16개를 이렇게 나눈다. 2·3순위 샘플은 화면을 아직 직접 보지 않았다. 표의 내용은 씬 파일과 설명서 기준이고, 착수할 때 다시 정한다.

| 순위 | 샘플 | 보여줄 것 한 문장 |
| --- | --- | --- |
| 1 | KeyRoom | 스크립트 몇 개로 열쇠·문·퍼즐·저장이 있는 작은 게임이 된다. |
| 1 | PlatformerLab | 경사와 움직이는 발판에서 발이 지면에 맞고, 컷씬과 패럴랙스가 붙는다. |
| 1 | ParticleLab | 불·비·연기·불꽃을 에셋으로 만들고 입자 1만 개를 한 프레임에 그린다. |
| 1 | BarrageLab | 풀과 묶음 제출로 탄 수천 발을 프레임 저하 없이 쏜다. |
| 1 | InventoryLab | 가방·의뢰·대화·저장이 데이터 테이블로 연결된다. |
| 2 | CameraLab | 추적, 분할 화면, 미니맵, 화면 효과. |
| 2 | AnimatorLab | 8방향 걷기와 상태 전환, 콤보. |
| 2 | SeedLab | 시드로 만든 미로와 200명 군중 길찾기. |
| 2 | CollisionLab | 충돌체 1만 개에서의 질의 속도. |
| 3 | CardLab | 카드 덱과 턴 리플레이. 지금 한글 UI다. |
| 3 | RhythmLab, AudioLab | 소리 녹음 도구가 있어야 찍는다. |
| 3 | KeyRoomTiles | 사각·아이소메트릭·육각 타일맵. 타일 칠하기는 편집 기능이라 플레이 영상에 안 들어간다. |
| 3 | AccessLab, DisplayLab, SpriteLab | 설정 화면과 스프라이트 나열이라 볼거리가 적다. |
| 제외 | ContentUpdates | 씬이 없다. |

## 6. 1순위 샘플별 개선안

### KeyRoom

- **무대**: 잔디밭을 돌바닥 방으로 바꾼다. 방이 화면을 채우게 25×14칸(32px)으로 다시 짜고, 벽은 윗면과 앞면이 있는 타일로 두른다. 방 밖은 어둡게 둔다.
- **조명**: 벽 횃불에 Point Light 2D와 불씨 입자. 열쇠와 출구는 스스로 빛난다.
- **캐릭터**: 4방향 걷기 애니메이션. 가방은 등에 붙인다.
- **반응**: 열쇠를 주우면 반짝임과 HUD 아이콘이 튄다. 문은 0.5초 동안 열리고 카메라가 문 쪽을 잠깐 비춘다. 발판은 눌리면 색이 바뀌고 빛난다.
- **빼는 것**: `Compass / Exit east`, `Paused` 상자, 언어·조작 버튼. 언어 전환은 3초짜리 장면 하나로만 보여준다.
- **장면**: 열쇠 줍고 문 열기 → 상자 열어 코인 얻고 저장 → 두 번째 방에서 상자를 발판에 밀어 문 열기.

### PlatformerLab

- **카메라**: 캐릭터를 따라가며 1.5배로 당긴다. 경사 구간에서는 발이 보이게 더 당긴다.
- **캐릭터**: 리그 부품(머리, 몸, 팔, 다리)을 지형과 같은 팔레트로 다시 그린다.
- **지형**: 흙 줄무늬 대신 풀·돌·뿌리 변형 타일. Parallax 씬의 언덕과 나무를 Platform 씬 배경으로도 쓴다.
- **컷씬**: 위아래 검은 띠와 말하는 인물 초상.
- **빼는 것**: 왼쪽 위 상태 상자 전체, 아래 측정값 상자.
- **장면**: 경사 오르기(발 IK 근접) → 셔틀 타고 건너기 → 컷씬 대화 → 패럴랙스 달리기.

### ParticleLab

- **씬 수**: 7개에서 3개로 줄인다.
- **장면 1, 밤 야영지**: 모닥불(불꽃·불씨·연기), 비와 바닥 튐, 반딧불, Space로 터지는 불꽃놀이를 한 화면에 둔다. 효과마다 월드 이름표를 붙인다.
- **장면 2, 입자 1만 개**: Stress 씬에 Stats 숫자를 켠다.
- **장면 3, 플레이 중 고치기**: Play 중에 `.particles` 값을 바꾸면 바로 반영되는 모습. 에디터 패널이 보여야 해서 이 장면만 창 전체를 쓴다.
- **2026-11-25 촬영분**: 장면 1을 `Scenes/Showcase.scene` 한 씬으로 만들었다(`showcase-src/particle/art.sh`, `scene.py`). 주인공이 오른쪽으로 걸으며 모닥불 → 비구름 → 분수 → 불꽃놀이(Space) → 포털의 입자 10,240개 순서로 지나고, 지날 때마다 왼쪽 위 태그가 하나씩 뜬다. 장면 2의 1만 개는 포털에 넣고, 오른쪽 위에 에디터 Stats(fps·draws)를 덧붙였다(폭풍 중 60fps). 반딧불은 벤치와 분수 위에 있다. 장면 3은 Particle 패널을 오른쪽에 둔 창 전체 화면으로 찍어 영상 끝에 붙였다: Play 중에 CampFlame의 크기·중력·방출량을 바꾸면 저장되고 실행 중인 모닥불에 바로 다시 읽힌다. 새 스크립트는 `Walker`·`StationTags`(`MotionFacing`은 PlatformerLab 복사).

### BarrageLab

- **무대**: 별 배경(패럴랙스), 탄을 쏘는 기체, 맞으면 깜빡이고 터지는 표적.
- **탄**: 가산 블렌드로 빛나게 한다.
- **숫자**: Stats를 켜고 묶음 제출 ↔ 개별 제출을 바꿀 때 그리기 호출 수가 바뀌는 것을 보여준다. 이 숫자가 이 샘플의 주제다.
- **빼는 것**: Instantiate/Destroy 모드는 3초만 넣는다. 이 모드에서는 에디터 조작이 막혔었다.
- **장면**: 나선 탄막(묶음 ↔ 개별) → 프리팹 풀로 표적 맞히기 → 레이저와 궤적.
- **2026-11-25 촬영분**: `Scenes/Showcase.scene` 한 씬(`showcase-src/barrage/art.sh`, `scene.py`). 옆으로 진행하는 슈팅 화면이고 카메라 배율은 1, 그림 한 칸은 2 유닛이다. 전함 코어가 기존 `SpiralBarrage`를 쏘고, 기체는 방향키로 움직이며 `ShipPilot`으로 볼트를 자동 발사한다(Rent, 기존 `PooledBullet`). 드론(`Drone`)은 맞으면 흰 실루엣으로 번쩍이고 네 번째에 터진다. B로 개별 제출로 바꾸면 SDL draws가 약 35에서 약 2,480으로 오른다. 이 숫자는 에디터 Game 뷰 Stats에만 있어서, 영상에는 `cut.swift`의 `CUT_INSET`으로 그 부분을 오른쪽 위에 덧붙였다. Space로 전함이 반사 레이저(`BossLaser`, Line 2D)로 바꿔 Trail 2D 혜성을 쏜다. P로 볼트를 Instantiate/Destroy로 잠깐 바꾸면 만든 수가 늘고 fps가 떨어졌다가, 풀로 돌아오면 회복된다.

### InventoryLab

- **무대**: 패널 뒤에 약초상 가판대가 있는 작은 마당을 둔다. UI는 화면의 절반 이하로 줄인다.
- **대화**: 초상이 있는 대화 상자. 글자가 다 찍힌 뒤에 다음으로 넘어간다.
- **의뢰**: 오른쪽 위 작은 추적 표시. 완료되면 체크가 튄다.
- **가방**: 칸을 키우고 아이콘 위주로 보여준다. 끌어 놓기에 트윈을 넣는다.
- **저장 복구**: 저장 → 손상 → 직전 저장에서 이어가기. README가 내세우는 기능인데 1차 영상에 없었다.
- **빼는 것**: `item-0705` 같은 내부 ID, `Row objects 11 / Items 1000`은 자막 한 줄("1,000개 중 보이는 11행만 만든다")로 바꾼다.
- **장면**: 약초상과 대화해 의뢰 받기 → 약초 담고 보상 받기 → 1,000개 목록 정렬과 가방 끌어 놓기.
- **2026-11-25 촬영분**: `Scenes/Showcase.scene` 한 씬(`showcase-src/inventory/art.sh`, `scene.py`). KeyRoom 화풍의 약초 마당에서 Return으로 약초상과 대화한다. 초상화가 있는 대화 상자에 글자가 찍히고, 1–4로 답한다. 의뢰를 받으면 오른쪽 위 추적 표시가 뜬다. 약초를 밟으면 줍고, 가방 칸이 튀며 3개까지 쌓인다. 보상으로 수정을 받는다. 그다음 F5 저장 → 약초 하나 더 → F5 → F7(저장 중 충돌 흉내: 저장 파일을 반으로 자름) → F9 순서로, 직전 정상 저장으로 돌아가는 것을 보여준다. 샘플 패키지(InventoryBag·QuestJournal·DialogueSession·WorkbenchSave)를 그대로 쓰고, 새 스크립트는 `Showcase/HerbYard`·`HerbPickup`·`YardWalker`·`HeroAnimation`(KeyRoom 복사). 가방과 장부는 사용자 의견(2026-11-25: 정렬이 어긋나 보이고, 끌기가 너무 빠르고, 장부가 무엇을 보여주는지 모르겠다)에 따라 별도 씬 `Scenes/ShowcaseStock.scene`(`showcase-src/inventory/stock.py`)과 별도 영상 `showcase-video/InventoryLab-BagLedger.mp4`로 뺐다. 가방과 장부 두 패널은 위·아래 선을 맞췄고, 격자는 레이아웃 여백 0으로 패널 가운데에 둔다. 칸을 집으면(`ScreenUi.Carried`) 아이콘이 칸을 떠나 커서를 따라 움직이고, 놓으면 새 칸에 들어가며 그 칸의 아이템은 원래 칸으로 날아간다(`BagSlots`). 녹화는 클릭으로 집기 → 커서를 천천히 옮기기 → 클릭으로 놓기 순서다. 녹화 도구로는 버튼을 누른 채 천천히 끌 수 없어서다. 장부(`LedgerList`)는 "Rows 993–1,000 of 1,000 · 9 row objects"처럼 보이는 행 범위와 실제 행 오브젝트 수를 보여 주고, 열 머리글이 정렬 탭을 겸한다.

## 6-2. 2순위 샘플별 개선안

2026-11-25에 네 샘플의 편집 화면과 스크립트를 보고 정했다. 네 샘플 모두 단색 사각형·원과 숫자 읽기값뿐이라, 1순위처럼 KeyRoom 화풍(그림 한 칸 = 2유닛)의 장면에 원래 기능을 그대로 얹는다. 영상은 샘플마다 Game 뷰만, 플레이 구간만 남긴다.

### CameraLab

- **지금 화면**: 청록 바탕에 모서리 표시만 있는 1600×896 맵, 좌우 분할 화면, 미니맵, 1~8 키로 켜는 화면 효과. 무엇을 따라가는지 보이지 않는다.
- **무대**: 맵 전체를 마을 장터로 만든다(잔디, 돌 광장, 1순위 샘플의 소품). 주인공과 동료(같은 그림, 옷 색만 다르게)가 각자 화면을 갖는다.
- **보여줄 것**: 좌우 분할 화면에서 두 카메라가 각자 따라가고 맵 가장자리에서 멈춘다. 오른쪽 위 미니맵은 세 번째 카메라가 맵 전체를 그린다. 화면 효과는 키 대신 장소에서 터진다: 화약통(흔들림 + 줌 펀치 + 섬광), 저녁 구역(색 보정).
- **빼는 것**: 좌표 읽기값, 1~8 키 줄, Comfort·Rumble·커서 설정(자막 한 줄로).
- **2026-11-25 촬영분**: `Scenes/Showcase.scene`(`showcase-src/camera/art.py`, `scene.py`), `showcase-video/CameraLab.mp4`(15.0초). 그림은 1순위 샘플 것을 모아 썼고, 동료는 주인공 그림에서 머리·옷 색만 바꿨다. 주인공은 왼쪽 끝(카메라가 경계에서 멈춤) → 화약통(흔들림·줌 펀치·섬광·파편) → 저녁 광장(색 보정) 순서로 걷고, 동료는 오른쪽 화면에서 순찰한다. 미니맵에는 두 카메라의 영역 사각형이 보인다. 새 스크립트는 `Showcase/FieldWalker`·`PatrolWalker`·`FxZone`이다. 미니맵 테두리는 선 4개로 그린다(채운 패널은 맵 카메라 그림을 덮어 어둡게 만든다).
- **2026-11-25 다시 찍음**: 사용자 의견(분할 화면에서는 효과가 그 화면에만, 둘이 만나면 합쳐졌다 멀어지면 갈라져야 한다)에 따라 엔진 기능을 main 기준 `feat/camera-split-fx`로 만들어 데모 브랜치에 합쳤다(`Session.Screen.For(카메라)`, Camera 2D `splitWith`·`mergeDistance`·`splitDistance`·`blendSeconds`, schemaVersion 43; 결과 `engine/tests/results/camera-split-fx/`). 시연은 왼쪽 끝 → 화약통(섬광·흔들림이 주인공 화면에만) → 동료에게 다가가 한 화면으로 합쳐짐 → 멀어져 다시 갈라짐 순서다(`CameraLab.mp4` 18.1초). 합쳐진 동안 가운데 선과 Partner 이름표는 `Showcase/SplitHud`가 숨긴다.

### AnimatorLab

- **지금 화면**: 청록 마름모(Pulse), 사각형 Guard·Scout. 상태 이름이 글자로만 나온다.
- **무대**: 훈련장. Scout는 8방향으로 걷는 주인공, Guard는 목각 인형을 치는 경비병이다(3단 콤보, 피격, 점프·착지).
- **보여줄 것**: 화면 구석에 상태 그래프(상자와 화살표)를 그리고 지금 상태를 밝힌다. 전환 조건(파라미터, 종료 시간)이 바뀌는 순간이 보이게 한다.
- **빼는 것**: Pulse 마름모 데모. 8방향 그림이 새로 필요하다(지금 주인공은 4방향).
- **2026-11-25 촬영분**: `Scenes/Showcase.scene`(`showcase-src/animator/art.sh`, `scene.py`), `showcase-video/AnimatorLab.mp4`(15.0초, 사용자 의견으로 공격 사이·피격 뒤 여유를 늘리고 공격·피격 클립을 느리게 함). 대각선 걷기(오른쪽 아래·위)와 경비병·허수아비는 새로 생성했고, 왼쪽 대각선은 좌우 반전이다. 주인공이 네 대각선으로 걷고, 경비병이 Space 세 번으로 허수아비를 친다. Swing 이벤트마다 허수아비가 젖혀지고 짚 조각이 튀며, 세 번째 뒤에는 허수아비가 튕겨 돌아와 경비병이 Hurt로 간다(Any State). 오른쪽 위 상태 그래프의 금색 틀이 지금 상태를 따라간다. 경비병 애니메이터는 샘플의 Guard.animator 전환을 그대로 쓰고 클립만 바꿨다. 새 스크립트는 `Showcase/ScoutWalker`·`GuardTrainer`·`StateGraph`다.

### SeedLab

- **지금 화면**: 빈 격자와 시드 숫자, 오른쪽 조작 목록.
- **무대**: 시드로 만든 던전 미로를 KeyRoom 바닥·벽 타일로 그린다. 군중 장면은 마을 광장에서 주민 200명이 문을 지나 자기 자리로 간다.
- **보여줄 것**: 같은 시드는 같은 미로, 다음 시드는 다른 미로(N을 누를 때마다 미로가 다시 깔린다). 저장한 이동을 다시 재생하는 리플레이. 문을 닫으면 200명이 그 자리에서 길을 다시 찾는 모습.
- **빼는 것**: 시드 입력 칸과 긴 조작 목록(자막으로).
- **2026-11-25 촬영분**: `Scenes/ShowcaseMaze.scene`·`ShowcaseCrowd.scene`(`showcase-src/seed/art.py`, `scene.py`), `showcase-video/SeedLab.mp4`(44.8초, 두 장면을 이음). 미로는 `Showcase/DungeonMaze`(MazeRun을 KeyRoom 32유닛 타일로 옮긴 복사본, 같은 MazeLayout·GridTurnRun)다. N·N·P·P로 시드를 바꿨다 되돌리면 상태 줄의 레이아웃 코드(통로 칸의 FNV 해시)가 "Seen before: same layout #6426"으로 같게 나와 같은 시드가 같은 미로임을 보인다. Return은 최단 경로를 금색 점으로 깔고 밟는 대로 지우며 상자까지 걷고(66턴), K 저장, T 리플레이는 저장된 경로를 다시 점으로 깔고 2배속으로 따라간다(66턴 모두 일치). 군중은 샘플의 CrowdRun을 그대로 쓰고 마당만 새로 그렸다. 문이 닫혀 돌아가던 200명이 O로 문을 열면 그 자리에서 길을 다시 찾고, 200명이 모두 도착해 대열을 다시 이룰 때(15.6초)까지 보여 준 뒤 끝난다. 바닥은 KeyRoom 바닥 원본을 더 밝게 다시 뽑았고(`seed/FloorLight.png`), 벽은 어둡게 했다. 상자는 KeyRoom 그림(20×17 아트 픽셀)이 32유닛 칸보다 커서 벽으로 삐져나왔으므로, 바깥 나무 띠와 판자 한 줄씩을 빼 16×16으로 다시 만들었다.

### CollisionLab

- **지금 화면**: 원·막대·삼각형과 "Press Right" 문구.
- **무대**: 밤 들판에 충돌체 10,000개(돌·풀·반딧불 표식)를 깐다. 탐사 원이 들판을 지나간다.
- **보여줄 것**: 탐사 원 안에 걸린 충돌체만 빛나고, 질의 한 번에 걸린 시간과 개수를 보여준다. 10,000개를 단계 로딩할 때의 진행 막대.
- **빼는 것**: 마스크 게이트·경사 실험 화면은 태그 하나로 줄인다.
- **2026-11-25 촬영분**: `Scenes/ShowcaseLantern.scene`·`ShowcaseStones.scene`(`showcase-src/collision/art.py`, `scene.py`), `Scripts/Showcase/LanternQuery.cs`, `showcase-video/CollisionLab.mp4`(17.3초). 밤 들판에서 L을 누르면 돌 충돌체 1만 개(원·상자·삼각형, 5×5 그림)를 `AddSceneAsync`로 업데이트당 0.5 ms씩 만들며 진행 막대를 채운다. 그동안 등불은 계속 움직인다(약 0.7초). 그다음 고정 스텝마다 등불 원 하나로 Overlap 질의를 공간 격자와 전체 검사로 한 번씩 하고, 격자 답에 든 돌만 불을 켠다. 패널은 20스텝 평균으로 검사한 충돌체 수(약 500–750 대 10,000)와 시간(약 0.06 대 0.21 ms), 두 답이 같은지를 보여 준다. 마스크 게이트·경사 실험 화면은 넣지 않았다. **에디터 Play가 아니라 Save & Run 창을 녹화했다.** 에디터 Scene Play에서는 1만 개 방이 연결되는 프레임에 Play Hierarchy를 다시 만드느라 약 5초 멈춘다([split-loading 결과](../../engine/tests/results/split-loading/README.md#에디터-scene-play)). 게임 경로에서는 그 프레임이 수십 ms라서, 분할 로딩을 보여 주려면 게임 창이 맞다. 창 제목 막대와 둥근 모서리는 `CUT_CROP=16,60,1568,882`로 잘랐다.

## 6-3. 3순위 샘플

### CardLab

- **지금 화면**: 한글 UI의 카드 목록과 글자 버튼뿐이다.
- **무대**: 위는 돌다리 25칸이 놓인 마당과 끝의 상자, 아래는 카드 테이블(뽑을 더미, 손패 6칸, 버린 더미)이다. 카드 앞면은 9-slice 틀과 rich text `<icon=…>`로 그린 UI다. 아이콘은 InventoryLab 아이템 아이콘을 다시 썼다.
- **보여줄 것**: 샘플의 `CardDeck`(시드별 번호 셔플, 세 더미, Capture/Restore)을 그대로 쓰고, 카드 이름과 걸음 수는 `ShowcaseTrail.table` 한 파일에서 읽는다. 카드를 내면 버린 더미로 날아가고 손패가 당겨지며, 주인공이 그 걸음 수만큼 걷는다.
- **2026-11-25 촬영분**: `Scenes/ShowcaseTrail.scene`, `Assets/Showcase/ShowcaseTrail.table`(`showcase-src/card/art.py`, `scene.py`), `Scripts/Showcase/CardTrail.cs`, `showcase-video/CardLab.mp4`(37.4초). D 네 번 → 2·1로 두 장 내고 5칸 → S 저장 → 두 장 더 내고 9칸 → D로 더미가 비자 버린 카드를 다시 섞음(셔플 2) → L로 손패·더미·5칸이 그대로 돌아옴 → R(같은 시드 42) 후 D 세 번이 처음과 같은 Bearing·Shortcut·Climb 순서(정보 패널의 "Last run"과 나란히 보임). 태그는 켜지는 순서대로 데이터 테이블 → 저장 → 시드다.

### KeyRoomTiles

- **지금 화면**: GridLayouts 씬의 세 타일 레이어(작은 회색·흰 칸)와 사각형 플레이어뿐이다. 칠하기는 편집 기능이라 플레이 영상에 안 들어간다.
- **무대**: 사각(32×32)·아이소메트릭(48×24)·육각 pointy(36×42)·육각 flat(42×36) 격자마다 섬 하나. 섬 둘레는 세 장짜리 움직이는 물 타일, 안쪽은 세 그림 중 무작위로 고르는 풀, 길은 지형 브러시(4변 16칸, 육각 6변 64칸)다. 타일셋 그림은 `showcase-src/tiles/art.py`가 레이아웃마다 이웃 방향에 맞춰 길을 그려 만든다(생성 이미지 없음).
- **2026-11-25 촬영분**: `Scenes/ShowcaseGrids.scene`(`showcase-src/tiles/scene.py`), `Assets/Showcase/{Square,Iso,HexPointy,HexFlat}.tileset`, `Scripts/Showcase/GridPainter.cs`, `showcase-video/KeyRoomTiles.mp4`(14.3초). 네 섬에 0.45초마다 `SetTile`로 길을 한 칸씩 동시에 깔고(커서가 그 칸의 모양을 따라감, 패널에 호출 표시), 가운데 길 칸을 풀로 되돌렸다가 다시 깔아 이웃이 끊기고 다시 이어지는 것을 보인 뒤, 각 섬에서 사람이 칸 중심을 따라 길을 걷는다. 태그는 Four grids → Animated water → Runtime SetTile 순서로 켜진다. 길찾기(`TileNavigation`)와 청크 스트리밍은 넣지 않았다.

### AccessLab

- **지금 화면**: 글자 버튼뿐인 설정 목록(언어 순환, 글자 크기, 고대비, 저장)이다.
- **무대**: 왼쪽은 밤 축제 마당, 오른쪽은 플레이어 설정 패널(이름, 언어 드롭다운, 글자 크기·흔들림·번쩍임 슬라이더, 고대비 토글, 저장)이다. ParticleLab의 로켓을 두 발사대가 번갈아 쏘고, 터질 때 화면이 흔들리고 번쩍인다. 로켓은 이 장면에 맞춰 더 높이 오른다(속도 235–250). 로켓의 Burst 하위 효과는 씬에 Burst 발사기가 있어야 입자가 로드된다.
- **2026-11-25 촬영분**: `Scenes/ShowcaseOptions.scene`(`showcase-src/access/art.py`, `scene.py`), `Scripts/Showcase/OptionSetting.cs`·`FireworkShow.cs`, `showcase-video/AccessLab.mp4`(35.2초). 키보드만으로 Tab → 이름 "모눈" 입력 → 언어 English → 日本語 → 한국어(패널, 말풍선 모두 바뀜) → 글자 크기 200%(패널이 다시 배치되고 스크롤)에서 125% → 고대비 켜기 → 흔들림·번쩍임 0%(이후 불꽃은 화면을 흔들지 않음) → 저장(버튼 글자가 "저장했습니다 · 모눈 · 125%"로 바뀜). 태그·자막·말풍선은 `scaleHorizontalWithText`로 글자 배율을 따른다. 200%에서 태그 줄 간격이 잠깐 빠듯하다. 데모를 위해 프로젝트 기본 언어를 `en`으로 바꿨다(원래 `ko-KR`).
- **입력 주의**: computer-use의 낱자 키 입력은 macOS 한글 입력기에서 조합되지 않고 낱자(□로 보임)로 들어간다. 이름은 `type` 동작으로 완성된 글자를 넣었다. 그래서 영상에 조합 중 밑줄은 나오지 않는다.

### DisplayLab

- **지금 화면**: 토글 두 개와 클릭 버튼이 있는 설정 화면이다. 에디터 Play에서는 `Session.Display`가 없어 아무것도 바뀌지 않는다.
- **무대**: KeyRoom 그림으로 만든 방 한쪽(횃불이 흔들리고 주인공이 왔다 갔다 한다)과 화면 패널(테두리 없는 전체 화면·VSync 토글은 샘플의 `DisplayOption`, 초당 프레임, 12×12 픽셀 배지, 클릭 버튼). `integerScale: true`를 켰다.
- **2026-11-25 촬영분**: `Scenes/ShowcaseDisplay.scene`(`showcase-src/display/scene.py`, KeyRoom 그림 복사), `Scripts/Showcase/FrameMeter.cs`, `showcase-video/DisplayLab.mp4`(24.8초). **Save & Run 창**을 상태마다 창 ID로 따로 녹화해(`raw/displayA–D.mov`) 화면 크기 비율대로 검은 캔버스에 놓았다(`cut.swift`의 새 `CUT_PLACES`). 순서: 800×450 창에서 버튼 두 번 클릭 → 제목 막대 더블클릭으로 창을 키우면 2배 정수 배율과 여백 → VSync 끄면 120에서 약 500 fps, 다시 켜면 120 → 테두리 없는 전체 화면 → 끄면 원래 800×450 창으로 돌아온다.
- **녹화 주의**: 화면 영역 녹화(`screencapture -R`)는 Claude 창과 바탕화면까지 찍혀서 쓰지 않았다(그 녹화는 지웠다). 창 모서리를 끌어 크기를 바꾸는 동작은 바탕화면에서 끝나 computer-use가 막으므로 제목 막대 더블클릭(확대)을 썼다. 작은 창은 실제 비율 그대로라 영상에서 작게 보인다.

### SpriteLab

- **지금 화면**: 화살표 세 장(원본, 반전·색·마스크, 애니메이션)뿐이다.
- **무대**: 마당 한 장에 Sprite Renderer 기능을 모았다. 같은 SignBoard PNG를 Sliced로 세 크기(96×56·160×56·232×76)에 그린 간판, 발 위치로 정렬되는 우물과 주인공, 속성 키 클립으로 흔들리는 등불(Rotation)과 깜빡이는 동전(Tint·Opacity), Trail 2D를 남기는 반딧불, 연못 둑에서만 보이는 반사(같은 프레임을 Flip Y·Tint·줄무늬 마스크로 그림). 마스크는 스프라이트를 따라 움직이므로 물 모양으로 자르지 못한다. 그래서 연못 위쪽 둑을 곧게 만들어 주인공이 그 직선 구간만 걷게 하고, 연못 테두리를 반사보다 위(order -14)에 따로 그려 반사가 수면선에서 시작하게 했다. 동전도 발 Y로 정렬해 주인공이 앞을 지나면 가려진다.
- **2026-11-25 촬영분**: `Scenes/ShowcaseYard.scene`(`showcase-src/sprite/art.py`, `scene.py`), `Assets/Showcase/LanternSwing.clip`·`CoinPulse.clip`, `Scripts/Showcase/SpriteTour.cs`, `showcase-video/SpriteLab.mp4`(19.0초, 한 바퀴). 주인공이 우물 뒤(발 Y 205)로 지나며 가려지고, 연못 둑에서 반사가 생기고, 우물 앞(발 Y 290)으로 지나 다시 우물을 가린다. 태그는 켜지는 순서대로 9-slice → Y 정렬 → 속성 키 → Trail 2D → 반사다. 아틀라스·Aseprite 가져오기는 편집 기능이라 넣지 않았다.

## 6-4. 하네스 기준 누락 기능 (2026-11-25 점검)

샘플 단위로만 확인해 조명·곡선 같은 기능이 빠진 것을 사용자가 지적했다. `engine/tests/harnesses.json`의 70개 하네스를 기능 목록으로 보고 쇼케이스 씬·스크립트와 대조했다. 에디터 전용 하네스(Inspector, 도킹, 프로파일러, 필드 이름 바꾸기 등), 게임패드(실물 패드 필요), Foot IK(제거 예정)는 플레이 영상 대상에서 뺐다.

| 기능 (하네스) | 지금 쇼케이스 | 추가 계획 |
| --- | --- | --- |
| Global Light 2D 어둠·그림자, Sprite Shader 2D (PlatformerLab.Lighting·Effects·Shaders) | 횃불 가산광만 | PlatformerLab 동굴 쇼케이스 |
| 베지어 Line 2D, Path Follower 2D 곡선 경로 (BarrageLab.Lines, PlatformerLab.Paths) | 직선 레이저만 | PlatformerLab 경로·관절 쇼케이스 |
| 흔들다리·도르래 관절 (PlatformerLab.Joints) | 거리 관절 사슬만 | 같은 쇼케이스 |
| 캡슐 캐릭터·체인 경사 (PlatformerLab.Colliders), 뼈대 애니메이션 | 없음 | 같은 쇼케이스 |
| 데드존·앞보기·여러 대상 프레이밍, 디버그 그리기 (CameraLab.Framing·DebugDraw) | 없음 | CameraLab 두 번째 영상 |
| 홀드·탭·더블탭, 조작키 재설정 (KeyRoom.InputInteractions·Rebinding) | 없음 | KeyRoom 조작 영상 |
| 입력 기록·재생 (InventoryLab.Replay) | 없음 | 영상에서 뺌(사용자 결정 2026-11-26). `./monoon test replay`와 CI가 검증한다 |
| 지형 비용 길찾기, 실행 중 셀 바꾸기, 청크 스트리밍 (SeedLab.TileTerrain·GridNavigation) | 없음 | SeedLab 세 번째 장면 |
| 오디오 (AudioLab.*), RhythmLab | 찍지 않음 | 보류(사용자 결정 2026-11-25). 소리 녹음 도구 결정 뒤 |

### 2026-11-25 추가 결과

| 쇼케이스 씬 (생성 스크립트) | 보여 주는 기능 | 영상 | 확인 |
| --- | --- | --- | --- |
| PlatformerLab `ShowcaseCave` (`platformer/cave.py`, `CaveNight.cs`) | 해 질 녘 유적에 밤(Global Light 2D)이 오면 랜턴만 켜지고, 주인공이 지나가며 횃불 셋에 차례로 불을 붙인다(불꽃 입자 `Ignite.particles`, Sprite Shader 2D 불꽃, 그림자 드리우는 Point Light 2D). 그림자가 횃불마다 늘고, 돌아올 때 쓸려 간다. 달은 조명 안 받는 레이어 | `PlatformerLab-Lights.mp4` 19.3초 | 에디터 Play 녹화, `Tests/LightsTests.cs`(랜턴만 켜진 밤, 횃불이 순서대로 켜짐) 통과 |
| PlatformerLab `ShowcaseCourse` (`platformer/course.py`, `CourseRunner.cs`) | Showcase 주인공(옆모습 걷기, 리그·발 IK 없음)을 캡슐 + Character Motor 2D로 움직여 회전 관절 흔들다리, 슬라이더 관절 도르래, 베지어 Path Follower 2D 승강대, Edge Collider 2D 언덕을 지나며 동전 21개를 줍고 깃발에서 뛰어오른다 | `PlatformerLab-Course.mp4` 19.0초 | 에디터 Play 녹화, `Tests/CourseTests.cs`(동전 모두, 기능 태그 다섯) 통과 |
| PlatformerLab `ShowcaseStream` (`platformer/stream.py`, `StreamRunner.cs`) | 타일 청크 스트리밍: 7청크(32×32칸) 길이 능선의 Ground 레이어를 반경 1로 주인공을 따라 올리고 내린다. 위쪽 미니맵 카메라에 지형이 앞에서 생기고 뒤에서 사라지며, 모서리 패널이 `IsTileChunkLoaded`로 "CHUNKS IN MEMORY 3 / 7"과 칸을 켠다. 주인공은 `TryGetTile`로 앞 땅을 읽어 구덩이와 오르막에서 뛴다 | `PlatformerLab-Stream.mp4` 24.4초 (처음 판은 깃발 64 단위 앞에서 멈춰 어색하다는 지적으로 깃대를 멈추는 지점에 붙였다. 이전 판 `raw/PlatformerLab-Stream-v1.mp4`) | 에디터 Play 녹화, `Tests/StreamTests.cs`(7청크를 모두 건너고, 메모리엔 최대 3청크, 발밑 청크는 늘 올라옴) 통과. Release 빌드가 7청크를 굽는다 |
| InventoryLab Release·Native AOT 배포본 (`showcase-src/inventory/replay.command`, `showcase-src/sidebyside.swift`) | 입력 기록·재생: 터미널에서 `--record-input`으로 연 게임 창을 직접 플레이(약초 의뢰 수락, 약초 세 개, 보상, F5 저장, Esc)하고, 같은 `.inputreplay`를 Release와 Native AOT가 창 없이 재생해 저장 파일을 비교한다. 왼쪽 게임 창과 오른쪽 터미널을 같은 시간축으로 나란히 붙였다(사용자 결정) | 영상에서 뺌(사용자 결정 2026-11-26: 게임 화면 기능이 아니라 테스트·CI 기능). 만든 영상은 `raw/InventoryLab-Replay.mp4`로 옮겨 두었다 | 입력 43개. `slot-001.save`가 기록·Release·Native AOT 모두 SHA-256 `c89c3cd1d71a335e…`로 같았다(앞서 한 번 더 한 기록도 같은 해시). 첫 녹화는 플레이가 게임 녹화 시간보다 길었고, 전체 화면 조작 중 허가 목록 밖의 터미널이 숨겨져 검게 찍혀 다시 했다 |

PlatformerLab 두 씬 다시 만듦(2026-11-25, 사용자 지적: 화풍이 다르다, 투명 픽셀이 빛을 받는다, 코스 캐릭터가 IK를 쓴다).

- **화풍**: 처음 판은 `Ground.png` 시트 전체를 반복해 칸마다 풀 줄이 생겼고, 기둥 그림을 반 너비로 찌그러뜨렸고, 하늘이 단색 띠였고, 조명 씬은 KeyRoom 벽·나무통과 InventoryLab 수정을 썼다. 이제 `showcase-src/platformer/blocks.py`가 `Ground.png` 4비트 자동 타일 규칙(위·오른쪽·아래·왼쪽 이웃)으로 크기마다 윗면 풀과 테두리가 맞는 덩어리 그림을 만든다. `Deck.png`·`DeckLeg.png`는 가장자리를 남기고 줄인다. 언덕은 곡선을 따라 풀을 깐다. 하늘·산·달은 Showcase 씬과 같은 `Sky.png`·`Moon.png`·`#3E2C7C`이고, 두 씬 모두 Showcase 씬과 같은 텍셀당 1.5 화면 픽셀(1 텍셀 = 1 단위, 카메라 zoom 1.5)이다. 새 그림 17개는 합쳐서 64 KB다.
- **투명 픽셀이 밝게 남던 것**: 엔진 조명은 그림자를 콜라이더에서 광원을 등진 변부터 만든다(설명서 effects.md). 둥근 나무통의 사각형 콜라이더 안쪽이 밝게 남아 투명한 모서리가 사각형으로 비쳤다. 그림자를 드리우는 물체를 그림 전체가 불투명한 것(바위 기둥, 들보, 상자)으로 바꿨다. 주인공은 콜라이더가 없다. 엔진은 바꾸지 않았다.
- **밝기**: 밤이 되기 전에도 조명이 켜져 하얗게 번지던 것을 고쳤다. 이제 조명은 Global Light와 함께 켜진다. Point Light 세기는 최대 1이라, 반경을 200·160으로 늘리고 Ambient를 `#2A2C50`으로 낮췄다.
- **IK**: 코스 러너가 `Runner.rig`(발 IK 포함)를 쓰던 것을 Showcase 주인공 스프라이트와 `Hero.animator`·`MotionFacing`으로 바꿨다. 태그도 "Skeleton Animator 2D"에서 "Character Motor 2D"로 바꿨다. 이제 어떤 쇼케이스 씬도 리그를 쓰지 않는다.
- 이전 영상은 `showcase-video/raw/PlatformerLab-Lights-v1.mp4`·`PlatformerLab-Course-v1.mp4`로 옮겨 두었다.
- 두 번째 품질 지적 뒤(같은 날): 조명 장면은 모든 빛이 한꺼번에 켜져 반응이 없었다. 이제 주인공이 횃불에 하나씩 불을 붙이고, 들보에 깃발을 달았다. 두 번째 판은 `raw/PlatformerLab-Lights-v2.mp4`. 코스 장면은 장식이 없고 반응이 착지 먼지뿐이었다. 출발 표지판, 양 끝 횃불, 덤불·꽃(SeedLab 그림)을 더했고, 길 위 동전 21개(KeyRoom 그림, 주울 때 `CoinSpark.particles`)와 "COINS" 숫자, 깃발에서 뛰어오르며 터지는 `Confetti.particles`를 넣었다. 두 번째 판은 `raw/PlatformerLab-Course-v2.mp4`. 더 쓰지 않는 PlatformerLab `Assets/Showcase`의 `Barrel.png`·`Crystal.png`·`WallFace.png`·`WallCap.png`는 지우지 않았다.
| CameraLab `ShowcaseFraming` (`camera/framing.py`, `FramingTour.cs`) | 약초 줍기: 모닥불 옆 세 개는 데드존 안이라 카메라가 가만히 있고, 동쪽 약초 길을 달리면 앞보기로 카메라가 앞서고, 마지막 약초에서 Frame 카메라로 바뀌어 멀리 있던 동료와 만나 약초상에게 간다 | `CameraLab-Framing.mp4` 15.3초 | 에디터 Play 녹화, `Tests/FramingTests.cs`(일곱 개를 모두 줍고 둘이 약초상 앞에 섬) 통과 |
| CameraLab `ShowcaseFraming` + G (`FramingTour.cs`의 `#if MONOON_DEBUG_DRAW`) | 스크립트 디버그 그리기 `tick.Debug`: 줍기 반경 원, 다음 목적지 선, 남은 약초 번호, 데드존 사각형, 무리 프레이밍 상자, 좌표 글자. Scene Play·개발용 빌드에서만 그린다 | `CameraLab-DebugDraw.mp4` 16.6초 | 에디터 Play에서 G를 눌러 녹화(사용자 결정: 약초 장면에 G 토글). 배포·개발용 빌드 모두 통과 |

CameraLab-Framing 다시 만듦(2026-11-25, 품질 지적). 첫 판은 데드존 옆에 "Deadzone 120 × 80" 디버그 글자가 있었고, 이동에 목적이 없었고, 끝에서 둘이 맵 양 끝으로 흩어져 화면이 크게 줌아웃되며 인물이 점처럼 작아졌다. 이제 목적은 약초 일곱 개(`Herb1~3.png`, 주울 때 `HerbPick.particles`)와 "HERBS 0 / 7" 숫자 하나다. 동료는 처음부터 약초상 밭에 있어 만날 때만 조금 줌아웃되고, 둘이 함께 걸어 끝난다. 데드존 테두리는 옅은 금색 선만 남겼다. 이전 영상은 `raw/CameraLab-Framing-v1.mp4`로 옮겼다.
| SeedLab `ShowcaseRoute` (`seed/route.py`, `RouteCourier.cs`) | 타일셋 지형 비용(길 1, 풀 3, 진흙 8, 다리 1)으로 `TileNavigation.FindPath`, 실행 중 `SetTile`(비 → 진흙, 새 다리)로 다시 계산. 비용 22 → 35 → 11 | `SeedLab-Route.mp4` 14.5초 | 에디터 Play 녹화, `Tests/RouteTests.cs`(진흙 앞에서 돌아서 남쪽 다리로 건너 상자 옆에 섬) 통과 |

SeedLab-Route 다시 만듦(2026-11-25, 품질 지적). 첫 판은 지형 타일을 단색 점무늬로 새로 그려 SeedLab의 다른 장면과 따로 놀았고, 택배원이 화면 높이의 6%였으며, 범례·상태창 글자가 많았다. 이제 타일은 샘플 그림에서 만든다: 풀은 `Grass.png`, 흙길과 진흙은 그 풀을 갈색 계열로 다시 칠한 것(CardLab 디딤돌 하나), 물은 SpriteLab 연못, 다리는 `Plank.png` 색의 판자다. 덤불(`Bush.png`)이 테두리와 막힌 칸에 서고 꽃을 흩었다. 지도는 씬의 타일 청크에 있고(스크립트의 `ReplaceTiles` 제거), 주인공은 KeyRoom 걷기 프레임으로 한 칸씩 걸으며 Tween으로 미끄러지듯 움직인다(화면 높이의 17%). 비는 반복 입자(`Rain.particles`)로 북쪽에 내리고 진흙이 한 칸씩 번진다. 다리는 판자가 한 칸씩 놓이며 먼지(`Build.particles`)가 인다. 도착하면 상자가 튀고 반짝인다. 글자는 Maze 장면의 SEED 패널과 같은 "ROUTE COST" 큰 숫자 하나와 자막 두 줄이다. 비가 온 뒤에는 풀밭 우회와 남쪽 길로 돌아가 7열 풀밭을 오르는 길이 비용이 같아 남쪽이 골라진다. 그래서 주인공이 진흙 앞에서 돌아서서 남쪽 길로 가고, 바로 앞 여울에 놓인 다리를 건넌다. 이전 영상은 `raw/SeedLab-Route-v1.mp4`로 옮겼다.
| KeyRoom `ShowcaseMoves` (`keyroom/moves.py`, `MoveTrainer.cs`) | GameSettings 상호작용 Hold 30·Tap 12·DoubleTap 15, K로 Dash 키 실행 중 재설정 | `KeyRoom-Moves.mp4` 27.2초 | 에디터 Play에서 직접 입력해 녹화(짧은 J, 긴 J, U 탭, 긴 U, O 더블탭, 느린 O 두 번, K → P 재설정, P 더블탭). `Tests/MoveTests.cs` 4개 통과(홀드로 상자가 부서지는지 포함) |

KeyRoom-Moves 다시 만듦(2026-11-25, 사용자: 이번 영상들이 예전보다 품질이 떨어진다). 첫 판은 빈 바닥에 규칙표가 화면 1/3을 차지했고, 결과가 주인공이 조금 움찔하는 정도라 3장의 빈 면적·글자·움직임 기준을 못 넘었다. 이제 Showcase 방(벽, 횃불, 통, 단지, 깔개, 문)에서 문·열쇠·상자 스크립트와 HUD를 뺀 장면이다. Charge는 머리 위 미터를 채우고 앞 상자를 흔든 뒤 나무 조각(`CrateBits.particles`)과 화면 흔들림으로 부순다. Flick은 동전을 포물선으로 허수아비에 던진다. Dash는 잔상과 함께 달려가 허수아비를 밀치고 걸어서 돌아온다. 잔상은 대시 동안만 켜는 자식 오브젝트에 있어 돌아올 때는 없다(끄면 점이 지워진다, 사용자 지적). 규칙을 못 맞춘 입력은 머리 위에 한 단어(Hold longer, Too long for a tap, Too slow)로 보인다. 재설정 안내는 위쪽 작은 창으로 띄우고, 바뀐 키는 조작 안내 줄에 반영한다. Hero.animator는 걷는 동안에만 방향을 바꿔 정면을 보고 서 있었으므로, 이 장면에서는 애니메이터를 빼고 스크립트가 스프라이트를 고른다. 이전 영상은 `raw/KeyRoom-Moves-v1.mp4`로 옮겼다.

- 다섯 씬 모두 포함 씬에 넣었고 `.monoon-assets`를 다시 만들었다. 네 샘플(CameraLab, KeyRoom, PlatformerLab, SeedLab) Release 빌드와 에셋 보고서를 확인했다. KeyRoom 기존 테스트 9개도 통과했다.
- 아직 없음: 입력 기록·재생 영상, 카메라 디버그 그리기, 타일 청크 스트리밍, 오디오.
- 에디터 문제: File → Open Project로 다른 프로젝트를 열면 그 프로젝트의 스크립트가 Play에서 실행되지 않는다(콘솔 오류 없음, ShowcaseMaze도 같음). 에디터를 그 프로젝트 경로로 새로 띄우면 정상이다. `showcase-art` 에디터 빌드에서 확인했고 원인은 찾지 않았다.

## 7. 촬영과 컷 편집

2026-11-26부터 main 기준으로 입력 재생 촬영을 한다. 1차 녹화본(`showcase-video/*.mp4`, 에디터 창 녹화)은 데모 워크트리를 지울 때 함께 사라졌다. 아래 표와 이 문서의 1차 영상 설명은 그때 기록이다.

- **촬영**: `python3 showcase-src/film.py showcase-src/shots/<영상>.json`. 샘플을 복사해 그 장면을 시작 씬으로 Release 빌드하고, 촬영 파일의 입력(키 누름·탭, 글자, 왼쪽·오른쪽 클릭, 끌기)을 창 없이 재생하며 프레임을 PNG로 저장한 뒤 `encode.swift`로 mp4를 만든다. 영상은 GitHub README와 매뉴얼에 넣을 것이라(사용자 결정 2026-11-26) 기본값이 1280×720·30fps이고, GitHub 첨부 한도 10MB 아래로 비트레이트를 맞춘다(`--height`, `--fps`, `ENCODE_MAX_MB`). 게임은 출력 높이 이상으로 크게 그려(`MONOON_REPLAY_SNAPSHOT_SCALE`) 줄이므로 글자가 고해상도 창처럼 선명하다. 화면 녹화·키 대신 누르기가 없어 결과가 매번 같고, main이 바뀌면 같은 명령으로 다시 찍는다.
- **파일**: 영상은 저장소와 워크트리 밖 `~/Movies/MoNoon-showcase/<영상>.mp4`에 둔다. 저장소에는 촬영 파일과 도구만 넣는다. README·매뉴얼에는 mp4 대신 애니메이션 WebP를 넣는다(사용자 결정 2026-11-26). GitHub는 저장소의 이미지는 바로 보여 주지만 mp4는 재생하지 않고, `user-attachments` 업로드는 영상을 바꿀 때마다 손으로 올리고 게시해야 한다. `film.py`가 영상 옆에 `<영상>.webp`(560px, 12fps, `showcase-src/preview.py`)와 대표 이미지 `<영상>.jpg`를 만든다. 다시 찍은 뒤 기능 쇼케이스의 두 파일을 `docs/media/showcase/`에 복사해 커밋한다. README는 페이지가 무거워지지 않게 맨 위 KeyRoom만 WebP이고 갤러리는 대표 이미지에서 매뉴얼 장으로 연결한다. 영상마다 `engine/.artifacts/film/runs/<영상>/sheet.png`에 9장 시트가 생긴다.
- **한계**: SDL 소프트웨어 렌더러로 그린 게임 화면만 담긴다. 에디터 화면(Stats의 fps·draw call, Particle 패널 실시간 편집), 실제 창 기능(DisplayLab의 전체 화면·VSync·창 크기), OS 커서, 진동, 소리는 담기지 않는다. Debug draw는 part에 `"profile": "development"`를 주면 나온다.

### 2026-11-26 촬영분 (prep/showcase-film, 1280×720·30fps, 모두 10MB 미만)

| 영상 | 길이 | 비고 |
| --- | --- | --- |
| KeyRoom | 14.8초 | 첫 방 → 실제 문 → 두 번째 방, 태그 7개. 크레이트 끌기 포함 |
| KeyRoom-Moves | 18.7초 | 홀드·탭·두 번 탭, K로 조작키 재설정(ShowcaseBindings) |
| KeyRoomTiles | 12.4초 | 입력 없음 |
| AccessLab | 22.5초 | 이름 칸만 클릭, 나머지 키보드. 글자 200%에서 넘치던 태그·말풍선·캡션·패널을 고친 뒤 촬영 |
| PlatformerLab | 11.4초 | 태그 5개. 리그 러너를 뺐고(사용자 결정) 뗏목을 걸어서 건넌다 |
| PlatformerLab-Lights · -Course · -Stream | 16.5 · 18.8 · 23.7초 | 입력 없음 |
| SpriteLab | 16.3초 | |
| ParticleLab · ParticleLab-Workshop | 23.0 · 35.5초 | Workshop은 새 영상 |
| BarrageLab | 24.0초 | draw call 숫자가 없어 B 전환의 차이가 화면에 안 보인다 |
| CameraLab · CameraLab-Framing | 36.8 · 15.0초 | 커서·진동은 문구로만. Framing은 development 빌드로 Debug draw 포함, 줌아웃 이음매 수정 뒤 촬영 |
| AnimatorLab | 12.2초 | 상태 그래프 선택 테두리 정렬 수정 뒤 촬영 |
| InventoryLab · InventoryLab-BagLedger | 26.4 · 20.4초 | F7 손상 → F9 복구 포함. 가방 아이콘을 칸 가운데에 맞추고, 장부 정렬 열·방향·캡션과 태그를 넣었다 |
| CardLab | 20.3초 | |
| SeedLab · SeedLab-Crowd · SeedLab-Route | 20.9 · 16.0 · 11.7초 | 군중은 따로 뺐다(사용자 결정) |
| CollisionLab · CollisionLab-Casts | 8.7 · 12.2초 | 아트 다시 만듦(밤 들판·돌 9종 아틀라스, 횃불 복도). Casts는 따로 뺐고(사용자 결정), 레이어 이름표와 마스크가 다른 레이 세 개로 문이 무엇을 보여 주는지 드러낸다 |
| 게임 샘플 LedgeRun · IsoBand · HexRealm | 53.1 · 56.5 · 50.1초 | HexRealm 유닛이 경로를 따라 움직이고 공격 때 들이받는다. 나머지 퀄리티업은 나중에, README·매뉴얼에는 기능 샘플 영상만 넣는다(사용자 결정) |

못 찍은 것: DisplayLab(헤드리스에서는 표시 설정을 바꿀 수 없음). 실제 창 녹화로 보충할 후보: DisplayLab, BarrageLab의 draw call, ParticleLab의 Play 중 편집.

## 8. 작업 순서

| 단계 | 내용 | 끝나면 받는 확인 |
| --- | --- | --- |
| 0 | KeyRoom 첫 방 한 화면을 새 기준(격자, 팔레트, 조명, 캐릭터 크기)으로 만든다. 2026-11-25에 만들었다: `samples/KeyRoom/Scenes/Showcase.scene`, 캡처 `showcase-src/keyroom/style-frame.png`. 승인 대기 중이다. | 스크린샷 한 장으로 화풍 승인 |
| 1 | 공용 도구: 격자·팔레트 변환, 자막 띠, 샘플별 녹화·컷 스크립트 | — |
| 2 | KeyRoom을 끝까지 만들고 찍는다. 2026-11-25에 찍었다: `showcase-video/KeyRoom.mp4`(23.5초, Game 뷰만). 씬은 `Showcase.scene`과 `ShowcaseRoom2.scene`, 새 스크립트는 `HeroAnimation`·`FlagFx`·`DoorwayFade` 세 개다. 승인 대기 중이다. | 영상 한 편으로 방향 승인 |
| 3 | PlatformerLab → ParticleLab → BarrageLab → InventoryLab. PlatformerLab은 2026-11-25에 찍었다: `showcase-video/PlatformerLab.mp4`. 사용자 결정으로 발 IK를 빼고 주인공은 옆모습 걷기 스프라이트다(아래 "발 IK 기능 제거"). ParticleLab·BarrageLab·InventoryLab도 같은 날 찍고, 빠졌던 장면을 채워 다시 찍었다: `ParticleLab.mp4`(26.6초), `BarrageLab.mp4`(18.8초), `InventoryLab.mp4`(28.7초, 마당) + `InventoryLab-BagLedger.mp4`(19.5초). | 샘플마다 영상 |
| 4 | 2순위 4개. 착수 전에 화면을 보고 개선안을 이 문서에 추가한다. | 샘플마다 영상 |
| 5 | 3순위. 소리 녹음 도구가 정해지면 시작한다. | — |

0단계에서 화풍이 승인되지 않으면 1단계 이후를 시작하지 않는다. 1차 작업이 그 확인 없이 5개 샘플로 넓혀서 전부 다시 하게 됐다.

## 촬영이 끝난 뒤 할 일: 영상 사이 겹치는 기능 조사

사용자 지적(2026-11-25): SeedLab·길찾기처럼 이미 다른 영상에 나온 기능이 새 영상에 또 나오는 것 같다. 촬영이 모두 끝나면 전체 영상의 장면과 태그를 기능별로 모아 겹치는 것을 찾고, 영상마다 겹침을 줄이거나 합칠지 정한다. 그 전까지는 남은 영상 촬영을 먼저 끝낸다. 지금 알려진 후보: SeedLab 길찾기(`SeedLab.mp4` 미로·군중 길찾기, `SeedLab-Route.mp4` 지형 비용), KeyRoomTiles 격자 길찾기, PlatformerLab 관절(`PlatformerLab.mp4` 사슬, `PlatformerLab-Course.mp4` 흔들다리·도르래), 조명(KeyRoom 횃불, `PlatformerLab-Lights.mp4`).

### 2026-11-26 조사 결과

28개 쇼케이스 씬이 화면에 띄우는 기능 태그(`showIf` 패널의 굵은 이름)와 영상 21편(1차 합본 `MoNoon-showcase.mp4` 제외)을 기능별로 모았다. 같은 기능을 두 영상이 내세우는 것과, 이름만 같고 뜻이 다른 것을 나눴다. 결정은 사용자 몫이고, 태그를 바꾸면 그 영상은 다시 녹화해야 한다.

| # | 겹침 | 영상 | 성격 | 제안 |
| --- | --- | --- | --- | --- |
| 1 | 같은 장면 두 편 | `CameraLab-Framing.mp4`, `CameraLab-DebugDraw.mp4` | 같은 약초 장면이고 DebugDraw 판은 G로 기즈모를 켤 뿐이다 | 하나만 남긴다. DebugDraw 판에 프레이밍 태그가 모두 있으므로 그것을 `CameraLab-Framing.mp4`로 쓴다 |
| 2 | 미니맵 카메라 | `CameraLab.mp4`(Minimap), `PlatformerLab-Stream.mp4`(Minimap camera) | Stream에서는 청크를 보여 주는 도구일 뿐이다 | Stream의 태그를 "Tile reads: TryGetTile answers only for loaded chunks"처럼 스트리밍 쪽으로 바꾼다 |
| 3 | Runtime SetTile | `KeyRoomTiles.mp4`(이웃이 다시 이어짐), `SeedLab-Route.mp4`(비 → 진흙) | API는 같고 보여 주는 결과가 다르다 | Route 태그를 "Re-plan: every tile change re-plans the route"로 바꾼다 |
| 4 | 격자 길찾기 | `SeedLab.mp4` 미로(상자까지 경로 점), `SeedLab-Route.mp4`(비용 경로) | 같은 `GridNavigation`이다. Route는 지형 비용과 재계획이 새롭다 | 그대로 두거나, Route를 SeedLab 세 번째 장면으로 합치는 대신 미로의 경로 점 장면을 줄인다(SeedLab.mp4가 이미 44.8초) |
| 5 | Trail 2D | `BarrageLab.mp4`(혜성 꼬리), `SpriteLab.mp4`(반딧불 길) | 같은 컴포넌트를 두 영상이 태그로 내세운다. KeyRoom-Moves의 대시 잔상은 태그 없음 | SpriteLab 태그를 빼고 Barrage에 남긴다 |
| 6 | Particles | `ParticleLab.mp4`, `BarrageLab.mp4`("Particles: drones flash") | 입자는 ParticleLab의 주제다 | Barrage 태그를 "Hit flash: a burst on every drone hit" 같은 게임 반응으로 바꾸거나 뺀다 |
| 7 | Character Motor 2D | `PlatformerLab.mp4`(달리기·점프 전체), `PlatformerLab-Course.mp4`(태그) | Course 태그가 본편의 기본 이동과 겹친다 | Course 태그를 "Capsule Collider 2D"로 바꾼다 |
| 8 | 이름만 같음: Tile collision | `PlatformerLab.mp4`(경사 타일 다각형), `ParticleLab.mp4`(빗방울이 땅에서 튐) | 뜻이 다르다 | ParticleLab 쪽을 "Particle collision"으로 바꾼다 |
| 9 | 시드 결정성 | `SeedLab.mp4`(같은 시드 같은 던전), `CardLab.mp4`(같은 시드 같은 셔플) | 개념이 같고 결과물이 다르다 | 그대로 둔다(던전 생성과 카드 덱) |
| 10 | 저장 | `CardLab.mp4`(정확한 저장), `InventoryLab.mp4`(깨진 저장 복구), `SeedLab.mp4`(Save K, 리플레이) | 각자 다른 면이다 | 그대로 둔다 |
| 11 | 조명 | KeyRoom·PlatformerLab 본편의 횃불 Point Light, `PlatformerLab-Lights.mp4` | 본편은 태그 없이 배경으로만 쓴다 | 그대로 둔다 |
| 12 | 관절 | `PlatformerLab.mp4` 사슬(Distance Joint), `PlatformerLab-Course.mp4` 흔들다리·도르래(Revolute·Slider) | 다른 관절이다 | 그대로 둔다 |
| 13 | 나눠 불러오기 | `CollisionLab.mp4`(Split loading, 객체 1만 개), `PlatformerLab-Stream.mp4`(타일 청크) | 다른 기능이다(오브젝트 vs 타일) | 그대로 둔다 |

1–3과 5–8은 태그 글자나 영상 한 편을 고르는 일이라 손이 적게 든다. 4는 SeedLab 영상 구성을 다시 정할지에 달렸다.

## 촬영이 끝난 뒤 할 일: 데모 브랜치 정리

사용자 결정(2026-11-25): 모든 샘플을 다 찍은 뒤, 데모 브랜치에서 1차 데모를 걷어내고 새 데모(Showcase 씬)만 남긴다.

- **남김**: 샘플마다 `Scenes/Showcase*.scene`·`Assets/Showcase/`·`Scripts/Showcase/`. 새 데모가 쓰는 1차 결과물인 InventoryLab 아이콘 13개, `Items.table`·`Collection.table`·`Herbalist.data`, `UiSlot`·`UiButton`, PlatformerLab `Assets/Ui/UiShade.png`, BarrageLab `Comet.prefab`. 이것들을 만든 `showcase-src`의 icons·uiskin·english 스크립트와 원본 그림도 남긴다. 목록 행 아이콘 엔진 수정(`fix/list-row-icons`)은 main 쪽 브랜치로 정식으로 넣는다.
- **main 상태로 되돌림**: 1차 커밋 `023b236`이 샘플 원래 씬과 그림에 한 나머지 변경.
- **삭제**: 1차 영상(`01-keyroom.mov`…`08-baggrid.mov`), `MoNoon-showcase.mp4`, 미리보기 png, `showcase-src`의 1차 검토 캡처와 안 쓰는 원본 그림·스크립트. 원본 녹화 `showcase-video/raw/`(약 1.4GB)는 다시 자를 일이 없을 때 지운다.
- **방법**: main에서 새로 갈라진 데모 브랜치에 남길 것만 담는다(되돌리기 커밋을 쌓지 않는다). 커밋은 사용자가 말할 때만 한다.
- **정리 전 확인할 빠진 것**: 1차 `03-parallax`(시차 스크롤 전용)는 새 PlatformerLab 영상이 하늘·달 두 겹만 보여 대체가 약하다. 샘플 안 다른 씬 기능(KeyRoom 키 재설정·작업장, PlatformerLab 동굴·관절·경로, ParticleLab Presets·Sorting, InventoryLab Labels)은 계획 범위 밖이라 영상이 없다.

### 2026-11-25 정리 결과

- **새 브랜치** `demo/showcase`(워크트리 `.worktrees/showcase`)를 main `7b6068d`에서 만들고 `fix/list-row-icons`·`feat/camera-split-fx`를 이 브랜치에만 병합했다(main은 그대로, 사용자 결정). 기존 `demo/showcase-art`와 그 워크트리는 확인이 끝날 때까지 그대로 둔다.
- **담은 것**: 14개 샘플의 `Scenes/Showcase*`·`Assets/Showcase/`·`Scripts/Showcase/`와 `GameSettings`(시작 씬, 포함 씬, 입력, `integerScale`, AccessLab 기본 언어 `en`), 쇼케이스 씬이 참조를 따라 쓰는 바깥 파일. 바깥 파일은 1차 쇼케이스 스크립트(BarrageLab `ShipPilot`·`Drone`·`BossLaser`, KeyRoom `DoorwayFade`·`FlagFx`·`HeroAnimation`, ParticleLab `Walker`·`StationTags`·`MotionFacing`, PlatformerLab `BeatFlags`·`ChainLinks`·`MotionFacing`), 1차 결과물(InventoryLab 아이콘 13개·`UiSlot`·`UiButton`·`Items.table`·`Collection.table`·`Herbalist.data`·새 `Herb.png`, PlatformerLab `UiShade.png`, BarrageLab·ParticleLab `UiStripTop/Bottom.png`, BarrageLab `Target.png`·새 `Pellet.png`), 영어 문구로 쓰는 `SpiralBarrage.cs`, `HoldSeconds`를 더한 KeyRoom `KeySequence.cs`다. 각 샘플의 `.monoon-assets`는 `.meta`와 파일 해시에서 다시 만들었다.
- **main 그대로 둔 것**: 1차 커밋이 영어로 바꾼 나머지 원래 씬·스크립트·프리팹(BarrageLab `Barrage`·`Laser`·`PooledBullets` 씬, `BulletTurret`·`LaserSweep`, `Bullet`·`Comet` 프리팹의 그룹 이름, ParticleLab 원래 씬 7개, KeyRoom·PlatformerLab·InventoryLab 원래 씬 등). 쇼케이스는 이들을 장면 전환 참조로만 가리킨다.
- **`showcase-src`**: 이전 브랜치에서도 git에 없었다. 스크립트·프롬프트·팔레트 같은 글 파일만 커밋하고, 생성 원본 그림(PNG·JPG, 약 47MB)은 디스크에 두고 커밋하지 않았다.
- **지우지 않음**(사용자: 모두 확인하기 전까지 지우지 않는다): 1차·새 영상, `showcase-video/raw/`, 1차 검토 캡처, 기존 브랜치.
- **확인**: 14개 샘플 모두 `./monoon build <샘플> release`로 스크립트 컴파일과 에셋 참조를 확인했다. 처음에 InventoryLab만 실패했다. `GameSettings`가 쓰는 1차 버튼 테마 `Assets/Ui/Theme.table`을 더했고, `HerbYard.ProjectId`(편집기 저장 폴더용 프로젝트 ID)를 `Guid`에서 문자열로 바꿨다(빌드가 `Guid` 필드를 오브젝트 참조로 검사한다). 에디터 Play는 돌려 보지 않았다. 이전 브랜치에서 KeyRoom·PlatformerLab은 Showcase 씬을 에디터에서 직접 열어 찍었고 시작 씬·포함 씬은 원래 씬 그대로였다. 새 브랜치에서는 두 샘플도 Showcase를 시작 씬으로 하고 포함 씬에 넣었다(KeyRoom은 `ShowcaseRoom2`까지). 14개 Release 빌드의 에셋 보고서에 각 샘플의 Showcase 씬이 들어간 것을 확인했다.
- **남은 것**: RhythmLab·AudioLab(소리 녹음 필요)은 찍지 않았다.

## 촬영이 끝난 뒤 할 일: 발 IK 기능 제거

사용자 결정(2026-11-25): 시연 영상에서 Foot IK 2D를 뺐고, 시연 촬영이 모두 끝나면 엔진에서 발 IK 기능 자체를 제거한다. 그 전까지는 기능을 건드리지 않는다.

- 시연 영상: PlatformerLab 시연 씬은 스켈레탈 리그·발 IK 대신 KeyRoom 주인공의 옆모습 걷기 스프라이트를 쓴다. 경사 장면의 태그는 "Foot IK 2D" 대신 경사 타일의 다각형 충돌을 보여 준다.
- 제거할 때 손댈 곳(2026-11-25 `demo/showcase-art` 기준 검색): `Engine.Core`의 `Skeleton2D.cs`·`SkeletonPose.cs`(발 체인과 `TryGetBonePose`의 발 정보), `SceneComponent.cs`·`SceneDocument.cs`·`SceneGame.cs`·`RuntimeObjects.cs`·`PlayerYamlRegistration.cs`(Foot IK 2D 컴포넌트와 등록), 에디터 `ComponentPicker.cs`·`MainWindow.Presentation.cs`·`EditorTokens.cs`, 샘플 `PlatformerLab/Scenes/Platform.scene`·`Runner.rig`의 `feet`와 `Runner.cs`의 발 측정, `docs/manual/rig-animation.md`, 결과 기록 `engine/tests/results/rig-animation/`, 배포 기능 분석 도구(`aot-feature-switches`, `aot-size-2`, `trim/features.py`). 스켈레탈 애니메이션 자체를 남길지는 그때 정한다.
- 제거 뒤 PlatformerLab 회귀와 Native AOT 용량 변화를 기록한다.

## 9. 결정이 필요한 것

| # | 결정 | 추천 |
| --- | --- | --- |
| 1 | 영상에 에디터 창 전체를 담을지, Game 뷰만 잘라 담을지 | Game 뷰만. 게임이 화면을 다 쓰고 플레이 구간만 남긴다는 요청과 맞다. 에디터가 보여야 하는 ParticleLab 장면 3만 창 전체로 둔다. |
| 2 | 화풍 | 16px 격자 픽셀 아트, 32색 팔레트. 1차 그림과 이어지고 작은 해상도에서 선명하다. |
| 3 | 범위 | 1순위 5개를 먼저 끝낸다. 2·3순위는 그 결과를 보고 정한다. |
| 4 | 소리 | 지금은 무음으로 간다. RhythmLab·AudioLab을 찍기로 하면 그때 녹음 도구를 만든다. |
| 5 | 브랜치 | `demo/showcase-art`에서 계속한다. main의 하네스 75개 중 14개가 원래 샘플의 색과 문구를 확인하므로 main에는 합치지 않는다. |

## 10. 확인하지 않은 것

- 2·3순위 샘플 11개는 실행 화면을 보지 않았다.
- 생성 그림으로 프레임이 일관된 걷기 애니메이션이 나오는지 시험하지 않았다.
- 시연용 씬을 따로 추가했을 때 `.monoon-assets`와 빌드 용량이 어떻게 바뀌는지 재지 않았다.
- 에디터 Stats 표시가 녹화 화면에서 읽을 만한 크기인지 확인하지 않았다.
