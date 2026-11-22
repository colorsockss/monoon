# MoNoon Engine 범용 2D 로드맵

하나의 장르에 맞춘 완성형 시스템보다 여러 장르가 함께 쓸 실행 기능을 먼저 만든다. 아래 항목은 기존 기능을 확장하는 경우까지 포함한 개발 순서다. 장르 규칙(예: 상자 열기, 코인 획득)은 엔진 내장이 아니라 프로젝트 스크립트로 둔다.

| 순서 | 범위 | 구현할 기능 |
| --- | --- | --- |
| 1.1 | 런타임 오브젝트 | 프리팹 생성, 부모 및 위치 지정, 활성화·비활성화, 자식까지 제거, Play 중 Hierarchy·Inspector 관찰. [구현·조작법: KeyRoom 상자와 코인](manual/runtime-prefabs.md). |
| 1.2 | [Transform 2D](manual/transform-2d.md) | 로컬·월드 좌표 변환, 회전·스케일·피벗, 부모 변경 시 월드 변환 유지 |
| 1.3 | [입력 액션](manual/input-actions.md) | 구현 완료: 프로젝트 액션 편집·재바인딩, 패드·포인터, 기기별 조작 안내. [KeyRoom 검증](../engine/tests/results/input-actions/README.md) |
| 1.4 | [스크립팅](manual/scripting.md) | 구현 완료: 생명주기 콜백, 타입별 컴포넌트 조회·참조, 공용 C# 파일과 타입. [KeyRoom 검증](../engine/tests/results/scripting/README.md) |
| 1.5 | [시간 제어](manual/time.md) | 구현 완료. 일시정지와 한 틱 진행, 배속, 게임·UI 시간별 타이머. [KeyRoom 검증](../engine/tests/results/time/README.md) |
| 1.6 | [데이터와 저장](manual/player-data.md) | 구현 완료. YAML 데이터 에셋과 Inspector 참조, 세이브 슬롯·설정, 버전별 마이그레이션. [KeyRoom 검증](../engine/tests/results/player-data/README.md) |
| 1.7 | [씬과 리소스](manual/scene-streaming.md) | 구현 완료. 비동기 씬 전환, 같은 월드에 씬 추가·제거, 전환 화면과 공유 에셋 해제. [KeyRoom 검증](../engine/tests/results/scene-streaming/README.md) |
| 2.1 | [스프라이트](manual/sprites.md) | 구현 완료. 반전·색상·레이어·피벗·픽셀 정렬·PNG 알파 마스크, 아틀라스 일괄 임포트·트림·재패킹과 참조 유지. [SpriteLab 검증](../engine/tests/results/sprites/README.md) |
| 2.2 | [애니메이션](manual/sprites.md#파라미터로-애니메이션-전환하기) | 구현 완료. 파라미터 조건으로 상태와 클립 전환, 프레임 이벤트 편집·스크립트 콜백, 컨트롤러 미리보기. [AnimatorLab 검증](../engine/tests/results/animator/README.md) |
| 2.3 | [충돌](manual/collision.md) | 구현 완료. 레이어·양방향 마스크, 원·볼록 다각형, 겹침·레이캐스트·스윕 질의. [CollisionLab 검증](../engine/tests/results/collision/README.md) |
| 2.4 | [카메라](manual/cameras.md) | 구현 완료. 플레이어 추적·보간, 맵 경계, 분할 화면, 시야가 표시되는 미니맵. [CameraLab 검증](../engine/tests/results/cameras/README.md) |
| 2.5 | [오디오](manual/audio.md) | 구현 완료. WAV 스트리밍과 샘플 단위 루프·페이드, Master/Music/Effects 믹서, 플레이어 리스너 기준 공간 음향. [AudioLab 검증](../engine/tests/results/audio/README.md) |
| 2.6 | [UI·접근성](manual/game-ui.md) | 구현 완료. 가로·세로 자동 배치와 스크롤, 한글 IME, 다국어 레이블·줄바꿈, 글자 크기·고대비, 키보드·패드 탐색. [AccessLab 검증](../engine/tests/results/accessibility/README.md) |
| 3.1 | [씬 편집](manual/editor.md#다중-선택과-기즈모) | 구현 완료. 다중 선택과 월드 기즈모·스냅, 프리팹 속성 오버라이드·중첩·변형 및 Undo. [KeyRoom 검증](../engine/tests/results/scene-editing/README.md) |
| 3.2 | [프로젝트](manual/project-assets.md) | 구현 완료. 프로젝트 전체 검색·일괄 임포트, 양방향 의존성과 누락 참조 검사, 데이터 필드·타입별 테이블 편집. [KeyRoom 저장·재실행 검증](../engine/tests/results/project-assets/README.md) |
| 3.3 | [실행 진단](manual/play-diagnostics.md) | 구현 완료. Play 인스펙터, 충돌체·질의 표시와 레이 탐침, 실시간 콘솔, 프레임 시간·GC·드로우콜 프로파일러. [KeyRoom 검증](../engine/tests/results/play-diagnostics/README.md) |
| 3.4 | [작업 보존·확장](manual/workspace.md) | 구현 완료. 씬 복구·3방향 병합, 데이터·테이블·스크립트·설정 Undo, 마이그레이션 백업, 프리팹 배치 확장 명령, 패키지·프로젝트 템플릿. [KeyRoom 검증](../engine/tests/results/workspace/README.md) |
| 4.1 | [물리·월드](manual/platformer-physics.md) | 1차 구현: 강체, 거리 조인트, 캐릭터의 경사·점프·이동 발판. [PlatformerLab 검증](../engine/tests/results/platformer/README.md). 2차 구현: [오토타일·청크·타일 월드 스트리밍](manual/tilemaps.md#오토타일-브러시). [PlatformerLab 왕복 검증](../engine/tests/results/tile-world/README.md). 3차 구현: [보행 내비게이션·AI](manual/navigation.md). A* 지면 경로, 가시성 검사, 순찰·추적·복귀. [PlatformerLab 실행 검증](../engine/tests/results/navigation/README.md). |
| 4.2 | 연출 | 1차 구현. [파티클·스프라이트 셰이더·점광원](manual/effects.md) 및 [PlatformerLab 착지·횃불 검증](../engine/tests/results/effects/README.md). 2차 구현. [위치 트윈·스켈레탈 애니메이션·발 IK](manual/rig-animation.md) 및 [왕복 발판·경사 보행 검증](../engine/tests/results/rig-animation/README.md). 3차 구현. [컷신·대화와 조작 복귀](manual/cutscenes.md) 및 [탑승 컷신의 두 종료 경로 검증](../engine/tests/results/cutscenes/README.md). |
| 4.3 | 장르별 실행 | 1차 구현: [절차 생성·시드](manual/generation.md). 결정적 난수 스트림, 연결된 미로 생성, 실행 중 타일과 충돌 교체. [SeedLab 실행 검증](../engine/tests/results/generation/README.md). 2차 구현. [턴 이동과 리플레이](manual/turn-replay.md) 및 [SeedLab 턴별 좌표 비교](../engine/tests/results/turn-replay/README.md). 3차 구현. [리듬 타이밍](manual/rhythm.md)과 [RhythmLab의 Mac 판정 비교](../engine/tests/results/rhythm/README.md). 4차 구현. [대량 스프라이트 배칭·풀링](manual/sprite-pools.md)과 [BarrageLab 드로우콜·GC 검증](../engine/tests/results/sprite-pools/README.md). |
| 4.4 | 멀티플레이 | **보류.** 로컬 협동, 네트워크 복제·동기화. |
| 4.4 | 선택형 패키지 | 1차 구현. [인벤토리 패키지](manual/inventory.md)와 [InventoryLab의 Mac 실행 검증](../engine/tests/results/inventory/README.md). 2차 구현. [수집 퀘스트 패키지](manual/quests.md)와 [진행도 저장·보상 재수령 검증](../engine/tests/results/quests/README.md). 3차 구현. [덱 패키지](manual/decks.md)와 [CardLab의 셔플·저장 복원 검증](../engine/tests/results/decks/README.md). 4차 구현. [대사·선택지 편집과 대화 패키지](manual/dialogue.md) 및 [InventoryLab 약초상 분기·저장 검증](../engine/tests/results/dialogue/README.md). |
| 5.1 | 빌드·회귀 | 1차 [빌드 CLI와 로컬 회귀 검사](manual/build.md#터미널에서-빌드와-회귀-검사) 및 [InventoryLab 실행 검증](../engine/tests/results/build-cli/README.md), 2차 [개발용·배포용 프로필과 별도 디버그 심볼](manual/build.md#개발용배포용-프로필과-디버그-심볼) 및 [Mac 배포본 실행·심볼 검증](../engine/tests/results/build-profiles/README.md) 완료. 3차 [헤드리스 입력 기록·재생과 저장 비교](manual/build.md#헤드리스-입력-리플레이), [InventoryLab 실제 플레이와 Release·Native AOT 저장 비교](../engine/tests/results/input-replay/README.md) 완료. 4차 [기존 Windows·Linux x64 VM에서 InventoryLab Release·Native AOT 빌드와 Mac 기준 저장 비교](../engine/tests/results/input-replay-x64/README.md) 완료. 추가 샘플 회귀 범위는 확장 가능 |
| 5.2 | 배포·유지보수 | [데스크톱 3종 로컬 검증](manual/build.md#로컬-회귀-검사), [로컬 콘텐츠 번들](manual/build.md#콘텐츠-번들), [데이터 모드](manual/build.md#데이터-모드), [콘텐츠 업데이트](manual/build.md#콘텐츠-업데이트), [크래시 진단](manual/build.md#크래시-진단) 구현. [InventoryLab 번들 검증](../engine/tests/results/content-bundles/README.md) · [모드 전환 검증](../engine/tests/results/data-mods/README.md) · [업데이트 적용과 실패 복구 검증](../engine/tests/results/content-updates/README.md) · [Release와 Native AOT의 오류 줄 대조](../engine/tests/results/crash-diagnostics/README.md). [InventoryLab API 문서](manual/api.md)와 [기존 프로젝트 업그레이드 가이드](manual/upgrade.md) 작성 완료. [콘텐츠 버전 31 → 35 macOS·Windows·Linux 빌드·저장 검증](verification/inventorylab-upgrade.md) |
| 5.2 | 모바일·웹 타깃 | **보류.** BRUTAL이 Android, iOS, 웹을 각각 공식 지원할 때 해당 타깃의 개발을 재개한다. |

## 6. 데스크톱 게임 제작에서 남은 기능

| 순서 | 기능 | 상태와 완료 기준 |
| --- | --- | --- |
| 6.1 | 런타임 컴포넌트 속성 변경 | 구현 완료. 스크립트에서 Sprite Renderer의 PNG·색상과 2D 충돌체의 활성 상태를 바꾼다. KeyRoom 문은 활성 상태로 남고 Stop 후에는 원본으로 돌아간다. [API와 조작법](manual/scripting.md#플레이-중-스프라이트와-충돌체-바꾸기) · [Mac에서 세 실행 경로 검증](../engine/tests/results/runtime-components/README.md) |
| 6.2 | 참조를 보존하는 에셋 이동·이름 변경 | 구현 완료. Project 패널에서 파일·폴더를 옮기거나 이름을 변경하면 PNG와 스크립트 참조가 새 경로를 가리킨다. 변경마다 Undo 한 번으로 복원한다. [조작법](manual/project-assets.md#에셋-이동과-이름-변경) · [Mac KeyRoom 검증](../engine/tests/results/project-moves/README.md) |
| 6.3 | 외부 IDE 게임 스크립트 디버깅 | 구현 완료. 게임별 C# 솔루션을 생성하고 VS Code에서 Scene Play에 연결해 줄 중단점을 걸고 변수를 조회한 뒤 재개한다. [연결 방법](manual/scripting.md#외부-ide로-scene-play-디버깅) · [Mac KeyRoom 검증](../engine/tests/results/ide-debugging/README.md) |
| 6.4 | 충돌·트리거의 진입·유지·이탈 이벤트 | 구현 완료. 충돌·트리거의 Enter·Stay·Exit를 양쪽 오브젝트에 전달한다. KeyRoom 두 번째 방에서 플레이어와 상자가 압력판 문을 열고 마지막 물체가 나가면 닫힌다. [스크립트 API와 조작법](manual/collision.md#진입유지이탈-이벤트) · [Mac Scene Play·Release·Native AOT 검증](../engine/tests/results/contact-events/README.md) |
| 6.5 | 입력 모드 전환과 복귀 | 구현 완료. KeyRoom 첫 방에서 인벤토리와 나침반 확인창을 겹쳐 열고, 맨 위 창을 닫으면 이전 포커스와 조작이 돌아온다. [입력 모드 API와 조작법](manual/input-actions.md#입력-모드-전환과-복귀) · [Mac 실행 검증](../engine/tests/results/input-modes/README.md) |
| 6.6 | Y좌표 정렬과 스프라이트 정렬 그룹 | 구현 완료. KeyRoom 첫 방에서 발의 월드 Y에 따라 나무와 플레이어의 순서가 바뀌고, 몸·가방·버클은 한 그룹으로 그린다. [Inspector 설정](manual/sprites.md#발-위치로-앞뒤-정렬하기) · [Mac 실행 검증](../engine/tests/results/y-sorting/README.md) |
| 6.7 | 공용 번역 테이블과 게임 폰트 에셋 | 구현. UI·아이템·의뢰·컷신 대사는 공용 번역 테이블을 사용한다. KeyRoom의 기본·대체 한글 폰트를 macOS Scene Play와 데스크톱 3종의 Release·Native AOT에서 확인했다. [사용법](manual/game-ui.md#공용-번역-테이블과-게임-폰트) · [Mac 검증](../engine/tests/results/localization/README.md) · [Windows·Linux x64 검증](../engine/tests/results/localization/x64/README.md) |
| 6.8 | Windows·Linux 에디터 패키징과 조작 회귀 | 구현 완료. Windows·Linux x64 배포 에디터에서 KeyRoom 열기, Transform 편집, Play, 저장·재열기를 검증했다. 편집한 씬과 열쇠 획득 저장은 Mac Scene Play·Release·Native AOT와 일치했다. [패키지와 조작법](manual/editor.md#데스크톱-배포-에디터) · [실행 기록](../engine/tests/results/editor-packaging/README.md) |

각 단계는 에디터 조작과 실제 빌드에서 같은 결과를 내는 작은 샘플로 검증한다. KeyRoom 다음에는 플랫포머로 물리·카메라를, 보드 퍼즐로 입력·UI·저장을 검증한다.

## 7. 범용 게임 제작과 배포 고도화

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 7.1 | 스크립트 Inspector | 구현 완료. 오브젝트·에셋 참조 목록의 드래그 추가·정렬·제거, enum 드롭다운과 Vector2 좌표 편집을 지원한다. [조작법](manual/scripting.md#에셋-참조-목록과-enum-vector2) · [InventoryLab 선택지 검증](../engine/tests/results/script-reference-lists/README.md) · [KeyRoom 문 스프라이트·위치 검증](../engine/tests/results/script-asset-fields/README.md) |
| 7.2 | 프리팹 원본 편집 | 구현 완료. Project에서 원본을 단독 편집하고 Inspector에서 인스턴스의 개별 속성을 적용·복원한다. [KeyRoom 나무 두 그루로 따라 하기](manual/editor.md#프리팹-원본과-속성별-오버라이드) · [Mac 실행 검증](../engine/tests/results/prefab-source/README.md) |
| 7.3 | 게임 정의 이벤트 | 구현 완료. Inspector에서 C# 이벤트와 수신 메서드를 연결한다. KeyRoom 열쇠와 문으로 저장·재열기, 제거·재진입 후 호출 횟수를 비교했다. [사용법](manual/scripting.md#게임-정의-이벤트) · [Mac 검증](../engine/tests/results/game-events/README.md) |
| 7.4 | 월드와 런타임 프리팹 상태 저장 | 구현 완료. 주 씬별로 저장할 오브젝트와 속성을 등록한다. KeyRoom에서 상자의 자식으로 생성한 코인의 ID·이동 위치와 획득 후 제거 상태를 F5·F9로 복원한다. macOS arm64·Windows x64·Linux x64에서 Release와 Native AOT의 저장·복원을 검증하고 `main`에 반영했다. [데스크톱 3종 검증 기록](../engine/tests/KeyRoom.WorldCheckpoints/README.md#데스크톱-3종-검증-기록) [사용법](manual/player-data.md#월드와-런타임-프리팹-저장) · [Mac 실행 기록](../engine/tests/results/world-checkpoints/README.md) · [체크아웃별 검증](../engine/tests/results/world-checkpoints/README.md#깨끗한-체크아웃-검증) |
| 7.5 | 인게임 UI 테마와 9-slice | 구현 완료. KeyRoom의 공용 버튼 테마와 9-slice 안내 패널을 한글 200%로 표시하고, 세 Mac 실행 경로에서 창을 줄인 뒤 버튼 안팎을 눌러 확인했다. [사용법](manual/game-ui.md#버튼-테마와-9-slice) · [검증 기록](../engine/tests/results/ui-theme/README.md) |
| 7.6 | 대형 프로젝트의 번들 로딩·진단 | 구현 완료. 배포 번들의 검증·압축 해제를 진단하고 검증된 콘텐츠를 재사용한다. InventoryLab의 시장 배경음 번들로 macOS Release·Native AOT에서 캐시 비활성·최초 생성·재사용을 비교했다. [사용법](manual/build.md#대형-번들-로딩과-시작-진단) · [측정값](../engine/tests/results/bundle-loading/README.md) |
| 7.7 | 배포용 셰이더 프레임 사전 생성 | 구현 완료. 씬·프리팹에서 참조한 셰이더와 내장 광원·입자 이미지를 빌드 시 PNG로 생성한다. PlatformerLab 횃불의 32프레임과 루프 경계에서 macOS Scene Play·Release·Native AOT의 깜빡임과 밝기를 비교했다. [사용법](manual/effects.md#배포용-프레임-시트) · [Mac 검증](../engine/tests/results/baked-shaders/README.md) |
| 7.8 | 압축 배경음악 | 구현 완료. Ogg Vorbis를 임포트해 스트리밍 재생한다. AudioLab에서 WAV 대비 배경음 용량이 94.0% 줄었고 Mac의 Scene Play·Release·Native AOT에서 루프 주기와 페이드 길이가 일치했다. [사용법](manual/audio.md) · [출력·배포 용량 검증](../engine/tests/results/vorbis/README.md) |

## 8. 데스크톱 제작 도구와 런타임 개선

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 8.1 | 런타임 Transform과 부모 변경 | 구현 완료. 스크립트에서 로컬 위치·회전·스케일을 바꾸고, 부모 변경 시 월드 변환이나 로컬 변환을 유지한다. KeyRoom에서 코인을 들고 내려놓은 뒤 F5·F9로 부모·변환·충돌 영역을 복원했다. Mac Scene Play·Release·Native AOT의 저장 결과와 Stop 뒤 원본 씬을 확인했다. [사용법](manual/transform-2d.md#play-중-변환과-부모-변경) · [실행 기록](../engine/tests/results/runtime-transforms/README.md) |
| 8.2 | 외부에서 수정한 에셋 자동 재임포트 | 구현 완료. 외부에서 KeyRoom의 `Door.png`를 덮어쓰면 Project 미리보기, `Door.clip`의 연결된 시트 셀과 타임라인, Scene의 문이 갱신된다. macOS arm64와 Windows·Linux x64 배포 에디터에서 포커스 복귀와 재실행 후 네 셀의 참조·재생 시간을 확인했다. [사용법](manual/project-assets.md#외부-png-자동-재임포트) · [화면 검증](../engine/tests/results/external-sprite-reimport/README.md) |
| 8.3 | Inspector 다중 편집 | 구현 완료. 공통 컴포넌트의 서로 다른 값은 Mixed로 표시한다. KeyRoom의 WallWorkshop 씬에서 벽 10개의 충돌 마스크를 함께 편집하고 Undo 한 번으로 각 인스턴스의 값과 프리팹 오버라이드를 복원한다. [사용법](manual/editor.md#inspector에서-충돌-마스크-함께-편집하기) · [검증 기록](../engine/tests/results/inspector-multi-edit/README.md) |
| 8.4 | 월드 오브젝트 클릭·드래그 | 구현 완료. KeyRoom의 PointerWorkshop에서 겹친 상자 중 앞의 상자만 움직인다. 드래그를 시작한 카메라와 잡은 지점을 유지하며 UI 버튼이 뒤의 상자 입력을 차단한다. macOS arm64와 Windows·Linux x64 배포 에디터에서 확인했다. [사용법](manual/input-actions.md#월드-오브젝트-클릭과-드래그) · [검증 기록](../engine/tests/results/world-pointer/README.md) |
| 8.5 | 플레이어 저장 슬롯 복구 | 구현 완료. 슬롯별 직전 정상본을 보존하고 최신 파일이 누락되거나 손상되면 불러올 때 복구한다. InventoryLab에서 약초 의뢰 보상 수령 후 서로 다른 두 저장을 만들고, 직전 저장의 수량·수령 상태 복원과 보상 재청구를 검사한다. [사용법](manual/player-data.md#직전-저장으로-복구하기) · [검증 기록](../engine/tests/results/slot-recovery/README.md) |
| 8.6 | 인게임 공통 UI 위젯 | 구현 완료. 슬라이더·토글·드롭다운이 테마와 마우스·키보드·게임패드 탐색을 공유한다. AudioLab에서 세 입력으로 음악을 60%로 맞추고 효과음 75% 유지와 재실행 후 복원을 macOS arm64·Windows x64·Linux x64에서 확인했다. [위젯 사용법](manual/game-ui.md#슬라이더토글드롭다운) · [세 OS 검증 기록](../engine/tests/results/ui-controls/README.md) |
| 8.7 | 데스크톱 화면 설정 | 구현 완료. DisplayLab에서 창 모드·테두리 없는 전체 화면과 VSync를 바꾸고 재실행 후 복원한다. macOS arm64와 Windows·Linux x64에서 전체 화면을 나왔을 때 720×540 창 크기, 16:9 게임 영역, 버튼 안팎 클릭을 확인했다. [사용법](manual/game-ui.md#데스크톱-화면-설정) · [플랫폼별 검증 현황](../engine/tests/results/desktop-display/README.md) |
| 8.8 | 대형 씬의 물리 질의 최적화 | 구현 완료. 공간 격자로 겹침·레이캐스트·스윕 후보를 줄인다. CollisionLab의 충돌체 1천 개·1만 개 씬에서 전체 순회와 적중 대상·순서를 대조한다. [샘플 조작법](manual/collision.md#대형-씬의-공간-인덱스) · [세 OS의 후보 수와 p95](../engine/tests/results/spatial-queries/README.md) |

## 9. 게임 제작 반복 작업과 대형 씬 개선

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 9.1 | Play Inspector 실시간 편집과 선택 적용 | 구현 완료. Play 중 스크립트 숫자와 로컬 Transform을 조정한다. KeyRoom에서 Speed 240→320을 시험하고 Stop하면 240으로 돌아온다. Speed만 선택 적용하면 320이 씬에 저장되고 Undo 한 번으로 복원된다. [사용법](manual/play-diagnostics.md#play-중-값-조정과-선택-적용) · [배포 에디터 검증](../engine/tests/results/play-inspector/README.md) |
| 9.2 | 태그·그룹 기반 오브젝트 조회 | 구현 완료. Inspector에서 태그와 그룹을 지정하고, 스크립트와 Play의 Debug 패널에서 멤버를 조회한다. KeyRoom의 코인이 생성되면 수집품 목록에 들어오고 획득·씬 이탈 시 빠진다. [사용법](manual/runtime-prefabs.md#태그그룹으로-오브젝트-찾기) · [배포 에디터 검증](../engine/tests/results/object-groups/README.md) |
| 9.3 | 카메라별 렌더링 후보 축소 | 구현 완료. 카메라별 공간 격자에서 후보를 골라 기존 Y 정렬과 그룹 순서로 그린다. CollisionLab의 1만 개 필드에 기본 카메라와 미니맵을 배치했다. [샘플 조작법](manual/cameras.md#대형-씬에서-두-카메라-확인하기) · [화면 대조와 CPU 측정](../engine/tests/results/render-candidates/README.md) |
| 9.4 | 콘텐츠 증분 빌드와 재빌드 사유 | 구현 완료. 글리프·셰이더 시트·타일 청크·번들의 입력을 추적해 다시 만든 이유를 표시한다. InventoryLab 대사 한 줄 변경 후 전체 빌드의 콘텐츠 해시와 약초 의뢰 리플레이를 대조한다. [사용법](manual/build.md#콘텐츠-증분-빌드와-재빌드-사유) · [검증 기록](../engine/tests/results/incremental-content/README.md) |
| 9.5 | 부모 크기에 맞춰 늘어나는 UI와 해상도 미리보기 | 구현 완료. UI Rect에서 네 변의 여백을 각각 고정하고 Scene 편집·Play에서 해상도와 언어·글자 배율을 미리 본다. KeyRoom 가방으로 부모 폭 변경과 한글 200%에서의 클릭 경계를 검사한다. [사용법](manual/game-ui.md#가장자리-고정과-해상도-미리보기) · [검증 기록](../engine/tests/results/ui-layout/README.md) |
| 9.6 | 컴포넌트 속성 키프레임 애니메이션 | 구현 완료. 타임라인에서 로컬 회전·X/Y 스케일·색·불투명도 키를 편집한다. KeyRoom의 상자 뚜껑과 코인을 각 클립의 다섯 시점에서 비교한다. [사용법](manual/sprites.md#컴포넌트-속성-키프레임) · [검증 기록](../engine/tests/results/component-keys/README.md) |
| 9.7 | 타일별 충돌 도형과 단방향 발판 | 구현 완료. 타일별 볼록 다각형과 방향이 있는 선분을 편집한다. PlatformerLab에서 경사 타일 5개를 왕복하고 발판 아래에서 뛰어올라 착지한다. [사용법](manual/tilemaps.md#타일별-충돌-도형과-단방향-발판) · [검증 기록](../engine/tests/results/tile-collision/README.md) |
| 9.8 | 탑다운 그리드 길찾기와 이동 비용 | 구현 완료. 4·8방향 A* 탐색과 칸별 진입 비용을 지원한다. SeedLab 배달원이 진흙을 우회하고, 이동 중 문을 열면 현재 칸에서 지름길을 다시 찾는다. [샘플 조작법](manual/navigation.md#탑다운-그리드-길찾기) · [검증 기록](../engine/tests/results/grid-navigation/README.md) |

## 10. 프로젝트 데이터 보존과 제작·실행 개선

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 10.1 | 스크립트 필드 이름 변경 시 데이터 보존 | 구현 완료. 이전 이름을 선언하면 씬·프리팹과 인스턴스 오버라이드가 새 필드로 이어진다. KeyRoom의 원본 240과 개별 설정 320을 저장·재열기와 데스크톱 3종의 Scene Play·Release·Native AOT에서 확인했다. [사용법](manual/scripting.md#inspector-필드-이름-바꾸기) · [검증 기록](../engine/tests/results/script-field-renames/README.md) |
| 10.2 | 플레이어 조작키 설정과 영구 저장 | 게임 안에서 키·패드 바인딩을 교환·취소·복원한다. KeyRoom을 재실행해 상자 열기 입력과 안내가 선택한 바인딩을 함께 따르는지 확인한다. |
| 10.3 | 느린 프레임의 원인을 찾는 프로파일러 | 프레임을 선택해 스크립트·물리·UI 작업의 호출 수와 시간을 읽는다. CollisionLab의 질의 부하를 Scene Play와 개발용 게임에서 추적한다. |
| 10.4 | 프리팹 구조 변경의 개별 적용·복원 | 자식·컴포넌트 추가와 삭제를 하나씩 원본에 적용하거나 복원한다. KeyRoom에서 자물쇠만 적용하고 효과음은 해당 상자에 남기는 동작과 Undo를 확인한다. |
| 10.5 | 데이터로 채우는 인게임 목록 UI | 데이터와 행 모양을 연결하고 화면에 보이는 행을 재사용한다. InventoryLab의 아이템 1,000개를 패드로 탐색한 뒤 정렬해도 선택·사용 대상 ID가 유지돼야 한다. |
| 10.6 | 타일셋 지형 정보와 런타임 셀 편집 | 타일셋에 지형 비용을 저장하고 게임에서 셀을 읽거나 바꾼다. SeedLab에서 진흙을 칠하거나 문 한 칸을 바꾸면 표시와 이동 경로가 함께 갱신돼야 한다. |
| 10.7 | 큰 에셋 로딩을 여러 프레임에 나누기 | 백그라운드 디코딩과 프레임당 업로드 예산을 적용한다. 방을 읽는 동안 최장·95백분위 프레임 시간, 전체 로딩 시간 및 취소 후 자원 해제를 비교한다. |
| 10.8 | 충돌체와 스크립트를 가진 프리팹 풀링 | 반환된 프리팹의 소유자·충돌·이벤트 상태를 초기화한다. BarrageLab에서 재사용한 탄환의 명중 횟수와 생성·제거 대비 GC 할당 및 프레임 시간을 비교한다. |
