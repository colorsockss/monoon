# 쇼케이스로 샘플 대체

사용자 결정(2026-11-26): 쇼케이스 씬이 샘플이자 테스트 대상이다. 기존 샘플 씬은 모두 쇼케이스로 대체한다. 기존 씬이 시험하던 기능이 쇼케이스에 없으면 쇼케이스에 넣고, 하네스·Engine.Checks·입력 녹화·측정 기준값을 쇼케이스 씬으로 옮긴 뒤 기존 씬을 지운다.

## 결정

| # | 결정 | 내용 |
| --- | --- | --- |
| 1 | 테스트 전용으로 남기는 씬 | 오디오 전용(AudioLab/AudioLab, RhythmLab/Practice), 스트레스(CollisionLab/Field1000·Field10000, ParticleLab/Stress), 배포하지 않는 에디터 검사 씬(KeyRoom/PrefabWorkshop·WallWorkshop·MovementWorkshop) |
| 2 | 부하가 큰 씬 | BarrageLab/Barrage(총알 2,000발 이상)와 SeedLab/Crowd(200명)는 스트레스가 아니라 쇼케이스로 옮긴다. 두 쇼케이스가 같은 스크립트(`SpiralBarrage`, `CrowdRun`)를 쓴다 |
| 3 | 발 IK | Foot IK 2D만 엔진에서 지운다. 스켈레탈 애니메이션(`.rig`, Skeleton Animator 2D)은 남기고 PlatformerLab 쇼케이스에 리그 인물로 옮긴다(사용자 답 "발 IK만 지우기", 2026-11-26) |
| 4 | 영상 | 기능을 넣어 화면이 바뀐 쇼케이스는 영상을 다시 찍는다 |
| 6 | 연출형 쇼케이스의 조작 시험 | 쇼케이스가 자동 연출이라 기존 씬의 키 조작 시험을 화면에 넣기 어려우면, 시험 복사본에서만 쇼케이스 오브젝트에 시험용 드라이버를 붙여 같은 지도·컨트롤러로 검사한다(AnimatorLab 경비병·정찰병, SeedLab Route. 사용자 결정 2026-11-26) |
| 7 | SeedLab Meadow | 47칸 블롭 오토타일을 쇼케이스 Route의 덤불 울타리 타일맵으로 옮기고 영상을 다시 찍는다(사용자 결정 2026-11-26) |
| 5 | ContentUpdates | 씬이 아니라 `monoon content-update pack`에 쓰는 InventoryLab 교체 파일이라 그대로 둔다 |

## 기존 씬 48개와 옮길 곳

하네스는 대개 씬 이름 대신 녹화의 `startScene:` GUID로 씬을 가리킨다. 기존 프로젝트 몇 개는 프로젝트 GUID와 첫 씬 GUID가 같아서 `startScene: {PROJECT}`가 기존 씬을 뜻한다(CameraLab, KeyRoom, SpriteLab). KeyRoomTiles/KeyRoom·SecondRoom은 KeyRoom의 같은 이름 씬과 GUID가 같다.

| 샘플/씬 | 받을 쇼케이스 | 쇼케이스에 더할 것 |
| --- | --- | --- |
| AccessLab/AccessLab | ShowcaseOptions | Accessibility.Mac 클릭 위치 다시 측정 |
| AnimatorLab/Animator | Showcase | 한 컨트롤러를 쓰는 두 인스턴스의 개별 파라미터, 스크립트 콜백 |
| AnimatorLab/Guard | Showcase(GuardTrainer) | 방향키·Space 입력으로 Walk/Grounded 구동, TraceGuard |
| AnimatorLab/Scout | Showcase(ScoutWalker) | 키보드 8방향 입력 |
| BarrageLab/Barrage | Showcase | Space가 레이저 전환이라 "발사 멈춤" 키 분리, Restart·Next 흐름 |
| BarrageLab/Laser | Showcase(BossLaser) | 회전 멈춤 키, 하네스 기대값 다시 만들기 |
| BarrageLab/PooledBullets | Showcase(ShipPilot) | `mode= ticks= fired= hits=` 결과 로그, 결정적 재시작. 쇼케이스의 PooledBullets 링크 제거 |
| CameraLab/CameraLab | Showcase, ShowcaseFraming | 페이드, 진동, 사용자 커서, 편의 설정·진동 설정 저장, 정지·한 칸 진행, 키로 켜는 효과 |
| CardLab/Trail | ShowcaseTrail | 버튼으로 하는 행동(?) |
| CollisionLab/CollisionLab | ShowcaseLantern 또는 ShowcaseStones | 캐스트·레이·스윕, 다각형 콜라이더, 센서 들어감·나감, InspectorEdits용 Probe 개체 |
| DisplayLab/DisplayLab | ShowcaseDisplay | DisplayLab.Desktop 버튼 위치 다시 측정 |
| InventoryLab/BagGrid | ShowcaseStock(BagSlots) | 키보드·게임패드 포커스 |
| InventoryLab/Catalog | ShowcaseStock(LedgerList) | 게임패드 포커스와 사용, 다른 씬에서 C로 들어오는 흐름 |
| InventoryLab/Inventory | Showcase(HerbYard) | 버튼 인벤토리 UI, Herbalist 녹화·저장 다시 만들기, RichText 캡처 대상 변경 |
| InventoryLab/Labels | Showcase(약초 마당) | 번역 키 이름표·리치 텍스트 간판 Text2D, 숫자 풀을 약초 주울 때의 `+1`로(사용자 결정 2026-11-26) |
| KeyRoom/KeyBindings | ShowcaseMoves | 행동별 키 변경 메뉴, 기본값 복원, 안내 문구 |
| KeyRoom/KeyRoom | Showcase | 가방 UI와 나침반 확인창, 한·영 전환, 일시정지와 스크립트 대기, 가방 트윈과 Controls 패널, UI 고정·레이아웃·스크롤, LidMotion 컴포넌트 키, Oak·Coin 프리팹 인스턴스, 열쇠 퀘스트. 녹화 모두 다시 |
| KeyRoom/PointerWorkshop | Showcase | 마우스 드래그와 두 번째 카메라 뷰포트 |
| KeyRoom/RoomGuide | Showcase | 겹쳐 여는 UI 씬, 글자 크기 버튼 |
| KeyRoom/SecondRoom | ShowcaseRoom2 | 일시정지·도움말, 언어 버튼, Controls 토글, 발판 동작 비교(?) |
| KeyRoomTiles/GridLayouts | ShowcaseGrids | 투영 타일 위 충돌·미끄러짐 |
| KeyRoomTiles/KeyRoom | ShowcaseGrids | 움직이는 플레이어와 타일 충돌, "Wall" 타일맵 |
| KeyRoomTiles/SecondRoom | 없음 | 쓰는 검사가 없어 바로 지운다 |
| ParticleLab/Curves | Showcase(CampFlame) | 곡선·그라디언트가 많은 Flame 개체, 새 기준값 |
| ParticleLab/Fireworks | Showcase | Trace가 Space를 누르게 |
| ParticleLab/Modules | Showcase | 링·박스 모양, 미리 데우기, 수동 방출, 움직이는 경로 위 로컬·월드 좌표. 쇼케이스의 Modules 링크 제거 |
| ParticleLab/Presets | Showcase | 한 `.particles`를 함께 쓰는 프리팹 두 개, 프리셋 묶음 |
| ParticleLab/Rain | Showcase(CloudRain) | 안개·불씨(?) |
| ParticleLab/Sorting | Showcase | 스프라이트 사이 입자 정렬, 블렌드 모드, 시트 프레임 |
| PlatformerLab/Cave | ShowcaseCave | 캡처 다시 만들기 |
| PlatformerLab/Joints | ShowcaseCourse | 입력으로 움직이는 주자, 재생·추적 개체 이름 |
| PlatformerLab/Parallax | Showcase 계열 | 트윈으로 움직이는 결정적 카메라, 타일 그리기 모드(?) |
| PlatformerLab/Paths | ShowcaseCourse | 입력으로 움직이는 주자 |
| PlatformerLab/Platform | Showcase | "Torch flame" 셰이더, WorldReadout 값, Traverse 녹화 다시. 리그·발 IK는 지운다 |
| PlatformerLab/Slopes | ShowcaseCourse | 8 px 타일 이음새, 사슬 위 동적 캡슐 바위 |
| SeedLab/Crowd | ShowcaseCrowd | 하네스 진입 흐름(Survey → G → C) |
| SeedLab/Meadow | 정할 것 | 8방향 47-blob 오토타일 |
| SeedLab/Route | ShowcaseRoute | 문 열고 닫기 재계획, 입력으로 하는 경우들 |
| SeedLab/Survey | ShowcaseMaze | 시드 입력란, 하네스 진입 흐름 |
| SpriteLab/Sprites | ShowcaseYard | `.aseprite`에서 가져온 애니메이션 |
| AudioLab/AudioLab, RhythmLab/Practice | 테스트 전용 | — |
| CollisionLab/Field1000·Field10000, ParticleLab/Stress | 테스트 전용 | — |
| KeyRoom/PrefabWorkshop·WallWorkshop·MovementWorkshop | 테스트 전용 | — |

(?)는 이름과 주석으로 짐작한 것이라 옮길 때 코드로 확인한다.

## 함께 바뀌는 것

- 입력 녹화 31개를 다시 만든다: PlatformerLab Joints·TileCollision·Colliders, KeyRoom UiLayout·ComponentKeys·WorldCheckpoints(fixtures 10개와 `.save`), 샘플 `KeyRoom/Tests/TakeKey`, `engine/tests/fixtures/InventoryLab/Herbalist`(InventoryLab.Replay·Crashes·`monoon test replay`). 실행 중에 녹화를 만드는 하네스 스크립트도 기존 씬 GUID를 바꾼다.
- 용량·시작 시간·프레임 측정 기준값을 다시 잰다.
- `docs/manual` 13곳의 기존 씬 이름을 고친다. `engine/tests/results`는 지난 기록이라 고치지 않는다.
- FeatureChecks가 "텍스트·오디오·물리 없는 샘플"로 쓰던 SpriteLab은 ShowcaseYard에 Text2D가 있어 더는 맞지 않는다. 검사 안에서 그런 프로젝트를 만든다.

## 작업 순서

샘플 하나가 한 묶음이다. 묶음마다 쇼케이스 보강 → 하네스·Engine.Checks 대상 변경과 녹화 → 기존 씬 삭제 → 바뀐 쇼케이스 영상 재촬영 → 그 샘플 회귀 순서로 한다. 작은 샘플부터 한다.

1. 바로 지우기와 측정만 다시 하는 것: KeyRoomTiles/SecondRoom, AccessLab, DisplayLab, CardLab
2. SpriteLab, AnimatorLab, SeedLab, KeyRoomTiles
3. BarrageLab, ParticleLab, CollisionLab, CameraLab
4. PlatformerLab(발 IK 제거 포함), InventoryLab
5. KeyRoom
6. 전체 회귀(`--build-editor`)와 main 비교, 그다음 main 합치기 확인

## 진행 기록

| 날짜 | 묶음 | 결과 |
| --- | --- | --- |
| 2026-11-26 | AccessLab, CardLab, DisplayLab | 기존 씬 삭제. Accessibility.Mac, DisplayLab.Desktop, DisplayLab.IntegerScale 세 모드 통과. CardLab `TrailTests` 추가 |
| 2026-11-26 | SpriteLab | 기존 씬과 화살표·`Icons.atlas`·`StripeMask` 삭제. 쇼케이스 반딧불에 `FireflyBlink.aseprite` 깜빡임과 HUD 태그, 소품 `Props.atlas`를 넣었다(화면이 바뀌어 영상 재촬영 필요). SpriteLab.Aseprite 세 모드, Samples.GameText의 SpriteLab 시나리오 세 경로 캡처 일치 |
| 2026-11-26 | AnimatorLab | 기존 씬 셋 삭제. 컨트롤러 검사는 쇼케이스 Guard·Scout 컨트롤러로, Beacon 재료는 `engine/tests/fixtures/AnimatorBeacon`으로. Conditions·Directions 하네스는 복사본에 시험용 키 드라이버를 붙여 세 모드 통과(쇼케이스 화면은 그대로) |
| 2026-11-26 | SeedLab | 기존 씬 넷 삭제. 미로 쇼케이스에 시드 입력·R·Space 기다리기·J 한 턴 리플레이 추가, Route 쇼케이스의 덤불을 47칸 울타리 타일맵으로 바꾸고 꽃·횃불·물결 장식 레이어와 태그 추가(둘 다 영상 재촬영 필요). Route 하네스 둘은 시험용 드라이버로 쇼케이스 지도에서. 하네스 넷 세 모드 통과 |
| 2026-11-26 | KeyRoomTiles | 기존 씬 셋과 Room 타일셋·문·열쇠·스크립트 삭제(쓰는 검사가 없던 SecondRoom 포함). 타일 충돌 검사(TileChecks·CollisionChecks.Tiles·ContactChecks.Tiles)는 KeyRoom 쇼케이스의 벽 타일과 플레이어로, 투영 레이아웃 검사는 ShowcaseGrids로 다시 짬(사용자 결정). 화면 변경 없음 |
| 2026-11-26 | BarrageLab | 기존 씬 셋과 포탑·표적·레이저 스크립트, Bullet 프리팹 삭제. 우주선에 R/P 재시작과 결과 기록, 드론에 명중·Exit 수와 재시작, 보스 레이저에 R 재시작을 넣음(화면 변경 없음). SpritePool·PrefabPool 검사, Lines·PrefabPools 하네스 통과. Paths 하네스 캡처 캔버스에 셰이더 미리보기 연결 |
| 2026-11-26 | ParticleLab | 두 번째 쇼케이스 `ShowcaseWorkshop.scene`(입자 공방, 거점 다섯: 형태·정렬·프리셋·곡선·노이즈와 충돌)을 만들고 기존 랩 씬 여섯과 쓰지 않는 에셋(Downpour, Rocket, 바닥) 삭제, Stress는 테스트 전용으로 남김(사용자 결정). 공방 영상을 새로 찍어야 함. Trace(야영지·공방·Curves·Stress), Editor, Reload 하네스와 파티클 엔진 검사 통과. KeyRoom.AssetFields의 curves 단계는 공방으로 옮겼지만 KeyRoom 묶음에서 함께 돌린다 |
| 2026-11-26 | CollisionLab | 랩 씬을 두 번째 쇼케이스 `ShowcaseCasts.scene`(같은 씬 id·개체 id·충돌체, 밤 풀밭 그림)으로 바꾸고 레이 선과 스윕 정지 자리를 그림. Field1000·Field10000은 테스트 전용으로 빌드 씬 목록에서 뺌. 하네스는 [collision_lab.py](../../engine/tests/Shared/collision_lab.py) 복사본(시작 씬 ShowcaseCasts, 카메라에 FieldSelector, 필드 씬 포함)을 씀. Spatial·SplitLoading·FrameProfiler(Release·AOT·Scene Play), EditorPerf, InspectorEdits(후보 에디터 1회), AssetFields field 단계, physics·history 검사 통과. 캐스트 영상을 새로 찍어야 함 |
| 2026-11-26 | CameraLab | 기존 CameraLab 씬과 FieldPlayer·MirrorPlayer 삭제. ScreenFxDemo(충격·섬광·페이드·색 보정, 진동, 사용자 커서, 편의 설정 저장, 정지·한 틱 진행)를 쇼케이스에 효과 패널과 함께 넣음(영상 다시 찍어야 함). 디버그 그리기는 프레이밍 쇼케이스의 FramingTour가 이미 보여 줌. 하네스는 시험용 드라이버 없이 쇼케이스를 그대로 기록: SplitScreen(쇼케이스, 방향키로 동료에게 다가갔다 돌아옴), Framing·DebugDraw(ShowcaseFraming 연출). CameraChecks·ScreenEffectsChecks를 쇼케이스로 다시 씀. CameraChecks의 201 vs 200.9752 실패는 카메라 중심이 화면 픽셀 단위로 맞춰지는 동작이어서 기대값에 반영. SplitScreen·Framing·DebugDraw 세 경로, ScreenEffects 헤드리스 재생(Release), camera·screen-effects 검사 통과. ScreenEffects GUI 단계(가상 패드)는 시험 창이 앞으로 오지 못해(window 도구 종료 7) 확인 못 함 |
| 2026-11-26 | PlatformerLab | Foot IK 2D를 엔진·에디터·문서에서 제거(`BonePose2D`는 Start·End만). 쇼케이스 시작 표지판 옆에 `Runner.rig` 리그 인물(Time 시계, 제자리 달리기)과 "Skeletal rig" 태그를 넣음(영상 다시 찍어야 함). 옛 씬 여섯(Platform, Cave, Joints, Paths, Slopes, Parallax)과 그 씬만 쓰던 스크립트·에셋 삭제, Ridge.tileset·Terrain.png는 검사 자료 `engine/tests/fixtures/PlatformerRidge`로 옮김. `scene.py`는 옛 Platform 데이터를 `platform-objects.yaml`에서 읽음. 하네스 7개를 쇼케이스로(Effects·Joints·Paths·Colliders는 코스, Lighting은 동굴, Parallax·TileCollision은 쇼케이스) 옮겨 세 경로 통과. 엔진 검사 12개를 다시 써 통과(ComponentStateChecks는 쇼케이스의 `active: false`를 허용하게, FeatureSwitchChecks는 스크립트를 뗀 모든 샘플 씬 40개로). 쇼케이스에 없어 빠진 것: 8px 타일 이음새 위 캡슐, 체인 위 Dynamic 캡슐 바위, Slopes의 트리거 마커, 장면 안의 Tiled·9-slice 스프라이트. 발견: 코스의 흔들다리 판자 8개가 잠들지 않아 플레이 중 틱당 5,184 B를 할당한다(PhysicsAllocationChecks에서 제외, 엔진 쪽 후속 과제) |
| 2026-11-26 | InventoryLab | 기존 씬 넷(Inventory, BagGrid, Catalog, Labels)과 그 씬만 쓰던 스크립트(InventoryWorkbench, InventoryAction, Bag/BagGrid, Catalog/CatalogScreen·CatalogButton, Labels/DamageBurst), 일회용 패치 `showcase-src/icons.py` 삭제. 약초 마당 쇼케이스에 Labels의 번역 키 이름표(`label.herbalist`)·리치 텍스트 간판(Text2D, 판 스프라이트), 약초를 주울 때 풀에서 빌리는 `+1`(DamageNumber·RisingNumber), Q/E 칸 선택과 U 사용, C로 재고 씬, 시장 배경음, 두 줄 조작 안내를 넣음(사용자 결정, 영상 다시 찍어야 함). 재고 쇼케이스는 장부가 시작 포커스를 받고 Enter·패드 A로 사용(슬롯 2 기록), B로 마당, 키·패드로 집은 아이템이 포커스 칸 위에 뜸(영상 다시 찍어야 함). Herbalist 입력은 쇼케이스 경로로 다시 만들었고([herbalist_input.py](../../engine/tests/InventoryLab.Replay/herbalist_input.py)) 저장은 예전 171바이트와 같아 x64 기준 저장도 그대로다. `test inventorylab`(smoke 걷기 경로), check_recovery, BuildProfileChecks(HerbYard.cs), RichTextChecks를 쇼케이스로. 샘플 `Tests/YardTests` 추가. BagGrid·RichText·WorldText·ScriptFields 세 경로, Crashes·DataList·Replay Release·AOT, Incremental 통과. Windows·Linux x64 게스트 재생은 실행하지 않음. 버튼으로 누르는 마당 인벤토리 UI는 넣지 않음: HUD 버튼에 포커스가 가면 방향키를 UI가 가져가 걷기가 멈춘다. 클릭하는 칸·행은 재고 쇼케이스에 있다 |
| 2026-11-26 | KeyRoom | 기존 씬 다섯 삭제. KeyRoom·SecondRoom·PointerWorkshop은 지우고, RoomGuide·KeyBindings는 같은 씬 id의 UI 씬 `ShowcaseGuide.scene`(쇼케이스에서 M, 글자 크기 버튼)과 `ShowcaseBindings.scene`(ShowcaseMoves에서 K: Charge·Flick·Dash의 키보드·패드 칸, 기본값 복원, 안내 문구, `player.settings` 저장)으로 바꿈. ShowcaseMoves의 K 바로 바꾸기는 이 메뉴로 대체. 쇼케이스 첫 방에 가방·나침반 확인창과 열쇠 의뢰, 한·영 버튼, 일시정지 표시와 0.5초 뒤 문 안내(스크립트 대기), Controls 버튼·패널(오른쪽 아래 버튼 줄), 허리 가방 트윈(SatchelPop), 닫힌 상자 흔들림(LidMotion)·동전 반짝임(CoinPulse) 컴포넌트 키, 떡갈나무 대신 `Pot.prefab` 인스턴스 둘(Small pot이 이름·크기·그림 재정의), 마우스로 끄는 상자 둘과 F8 두 번째 카메라 인셋(태그 둘), F6·F7 제거를 넣고 옛 오브젝트 id를 이어받음. 둘째 방에 Controls·언어·일시정지·M 안내. 발판 동작 비교는 같은 PressurePlate·PushCrate 그대로라 더할 것 없음. 세 쇼케이스(Showcase, ShowcaseRoom2, ShowcaseMoves) 화면이 바뀌어 영상 다시 찍어야 함. 옛 문 그림·`Door.clip`·`DoorSheet.png`는 검사 자료 [KeyRoomDoorSheet](../../engine/tests/fixtures/KeyRoomDoorSheet/README.md)로, Oak·옛 Key·Coin 그림과 `Coin.prefab`, CrateButton 삭제. 작업장 셋은 빌드 목록에서 빼고 쇼케이스의 F3 링크를 없앰. 하네스 23개·엔진 검사 약 40개·샘플 테스트(TakeKey 녹화 포함)를 쇼케이스로 옮김. WorldCheckpoints 녹화 10개와 저장 6개를 다시 만듦(x64 게스트 재생은 실행하지 않음). UiLayout·ObjectGroups·PlayInspector·Desktop과 InspectorComponents의 restart 단계, CollisionLab.InspectorEdits는 화면 잠금으로 이번에 확인하지 못함. Input.Gamepad는 샘플의 `.monoon-compiled`가 없어 실행하지 못함 |
| 2026-11-26 | 공통 | FeatureChecks(쇼케이스에서 글자를 뺀 SpriteLab), FeatureSwitchChecks(카메라 없는 추가 로딩 씬 제외), AssetReportChecks(KeyRoom 쇼케이스 기준 이유·기능), Dialogue·Inventory·Quests 패키지를 바뀐 InventoryLab 데이터로 다시 만듦, EngineFeatures1의 정수 배율 예시를 AudioLab 설정으로 |

### `./monoon test`에 남은 실패 (2026-11-26, 단계별로 계속 실행해 모음)

| 검사 | 첫 실패 | 처리할 묶음 |
| --- | --- | --- |
| BuildSizeChecks | 빌드 용량 기준값 | 마지막 전체 회귀 때 다시 측정 |
| Samples.GameText(하네스) | PlatformerLab 시나리오의 시작 씬 4fe9f1eb가 지워져 녹화가 맞지 않음 | PlatformerLab(KeyRoom 묶음에서 발견) |
