# 쇼케이스로 샘플 대체

사용자 결정(2026-11-26): 쇼케이스 씬이 샘플이자 테스트 대상이다. 기존 샘플 씬은 모두 쇼케이스로 대체한다. 기존 씬이 시험하던 기능이 쇼케이스에 없으면 쇼케이스에 넣고, 하네스·Engine.Checks·입력 녹화·측정 기준값을 쇼케이스 씬으로 옮긴 뒤 기존 씬을 지운다.

## 결정

| # | 결정 | 내용 |
| --- | --- | --- |
| 1 | 테스트 전용으로 남기는 씬 | 오디오 전용(AudioLab/AudioLab, RhythmLab/Practice), 스트레스(CollisionLab/Field1000·Field10000, ParticleLab/Stress), 배포하지 않는 에디터 검사 씬(KeyRoom/PrefabWorkshop·WallWorkshop·MovementWorkshop) |
| 2 | 부하가 큰 씬 | BarrageLab/Barrage(총알 2,000발 이상)와 SeedLab/Crowd(200명)는 스트레스가 아니라 쇼케이스로 옮긴다. 두 쇼케이스가 같은 스크립트(`SpiralBarrage`, `CrowdRun`)를 쓴다 |
| 3 | 발 IK | PlatformerLab/Platform의 Runner 리그와 Foot IK 2D는 쇼케이스로 옮기지 않고 이번 작업에서 엔진 기능과 함께 지운다. 손댈 곳은 [showcase-quality.md](showcase-quality.md#촬영이-끝난-뒤-할-일-발-ik-기능-제거) |
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
| InventoryLab/Labels | 정할 것 | 번역 키·리치 텍스트 Text2D, 데미지 숫자 풀 |
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
| 2026-11-26 | 공통 | FeatureChecks(쇼케이스에서 글자를 뺀 SpriteLab), FeatureSwitchChecks(카메라 없는 추가 로딩 씬 제외), AssetReportChecks(KeyRoom 쇼케이스 기준 이유·기능), Dialogue·Inventory·Quests 패키지를 바뀐 InventoryLab 데이터로 다시 만듦, EngineFeatures1의 정수 배율 예시를 AudioLab 설정으로 |

### `./monoon test`에 남은 실패 (2026-11-26, 단계별로 계속 실행해 모음)

| 검사 | 첫 실패 | 처리할 묶음 |
| --- | --- | --- |
| RichTextChecks | InventoryLab `Inventory.scene`의 ko-KR 약초상 대사 | InventoryLab |
| CameraChecks | 이동 후 추적 한 틱 201 vs 200.9752 | CameraLab |
| InputInteractionChecks | KeyRoom 설정을 그대로 읽는지 | KeyRoom |
| EngineFeatures1Checks | Parallax 씬의 Far hills 이동량(PlatformerLab이 정수 배율을 켬) | PlatformerLab |
| ProjectCatalogChecks | 폴더를 넘는 형식 검색 | KeyRoom(확인 필요) |
| HistoryChecks | PlatformerLab `Showcase.scene` 컴포넌트 활성 상태 | PlatformerLab |
| BuildSizeChecks | 빌드 용량 기준값 | 마지막 전체 회귀 때 다시 측정 |
