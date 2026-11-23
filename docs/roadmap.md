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
| 10.2 | 플레이어 조작키 설정과 영구 저장 | 구현 완료. KeyRoom 첫 방에서 K로 조작키 화면을 열어 키보드·패드 바인딩을 교환·취소·복원하고, 바뀐 액션만 `player.settings`에 저장한다. 재시작 후 상자 열기 입력과 HUD 안내가 선택한 키(C)와 패드(North)를 따르는 것을 macOS arm64의 Scene Play·Release·Native AOT에서 재생 입력으로 확인했다. Windows x64·Linux x64 QEMU 게스트의 Release·Native AOT는 세 트레이스와 저장 파일이 macOS와 바이트 단위로 같았다. 게스트 에디터 Scene Play는 에뮬레이션에서 조작키 화면이 늦게 열려 원래 검사에 실패했다(대기를 늘린 진단 실행은 통과). [사용법](manual/input-actions.md#플레이어-조작키-설정과-저장) · [검증 기록](../engine/tests/results/player-rebinding/README.md) |
| 10.3 | 느린 프레임의 원인을 찾는 프로파일러 | 구현 완료. Scene Play 그래프에서 프레임을 골라 스크립트·물리·UI·렌더 작업의 호출 수와 Total·Self 시간을 읽는다. 개발용 게임은 프레임별 CSV와 `monoon profile`로 같은 표를 읽는다. CollisionLab 1만 개 씬의 질의 프레임에서 겹침·레이캐스트·스윕 각 1,344회가 프레임 시간의 81–97%를 차지함을 macOS arm64의 에디터·개발용 Release·Native AOT에서 확인했다. Windows x64·Linux x64 QEMU 게스트의 에디터·Release·Native AOT에서도 같은 호출 수와 질의 비중을 확인했다(시간은 에뮬레이션 값). [사용법](manual/play-diagnostics.md#느린-프레임의-작업-분석) · [검증 기록](../engine/tests/results/frame-profiler/README.md) |
| 10.4 | 프리팹 구조 변경의 개별 적용·복원 | 구현 완료. 인스턴스에서 추가·삭제한 자식과 컴포넌트가 Overrides… 목록에 한 행씩 표시되고, 각 행을 원본에 적용하거나 인스턴스에서 되돌린다. 원본과 씬은 Undo 한 번으로 함께 복원된다. KeyRoom PrefabWorkshop에서 Lock만 적용하면 세 상자 모두 자물쇠를 갖고 효과음은 Chest 2에만 남는 것을 macOS arm64의 에디터·Release·Native AOT에서 확인했다. Windows x64·Linux x64 QEMU 게스트의 에디터·Release·Native AOT에서도 결과가 macOS와 같았다. [사용법](manual/editor.md#자식컴포넌트-추가와-삭제의-개별-적용) · [검증 기록](../engine/tests/results/prefab-structure/README.md) |
| 10.5 | 데이터로 채우는 인게임 목록 UI | 구현 완료. Scroll View의 첫 자식을 행 템플릿으로 쓰고 `BindList`로 게임 데이터를 연결하면 화면을 채울 만큼의 행만 만들어 재사용한다. 선택은 아이템 ID로 유지된다. InventoryLab 보관함에서 아이템 1,000개를 행 11개로 표시하고, 패드로 `item-0042`를 고른 뒤 정렬해도 macOS arm64 Release·Native AOT 모두 `item-0042`를 사용했다. Windows x64·Linux x64 QEMU 게스트의 Release·Native AOT에서도 같은 결과와 저장 파일을 확인했다. [사용법](manual/game-ui.md#데이터로-채우는-목록) · [검증 기록](../engine/tests/results/data-list-ui/README.md) |
| 10.6 | 타일셋 지형 정보와 런타임 셀 편집 | 구현 완료. 타일마다 지형 이름과 이동 비용을 저장하고, 게임 스크립트가 셀을 읽거나 한 칸씩 바꾸면 그림·충돌·길찾기 비용이 함께 갱신된다. SeedLab에서 진흙을 칠하거나 문 한 칸을 바꾸면 경로가 바뀌고, 걷는 중이면 현재 칸에서 다시 찾는다. macOS arm64의 Scene Play·Release·Native AOT에서 7개 경우를 확인했다. Windows x64·Linux x64 QEMU 게스트의 Scene Play·Release·Native AOT에서도 7개 경로와 트레이스가 macOS와 같았다. [사용법](manual/tilemaps.md#지형-비용과-실행-중-셀-바꾸기) · [검증 기록](../engine/tests/results/tile-terrain/README.md) |
| 10.7 | 큰 에셋 로딩을 여러 프레임에 나누기 | 구현 완료. 비동기 씬 로딩이 PNG·오디오를 워커 스레드에서 디코딩하고 오브젝트 생성·업로드에 업데이트당 4 ms 예산을 쓴다. CollisionLab 1만 개 방과 4096² 배경 4장을 읽을 때 최장 프레임이 Release 81→30 ms, Native AOT 79→22 ms로 줄었다. 취소하면 텍스처·디코딩 버퍼 수가 시작 상태로 돌아온다(macOS arm64). Windows x64·Linux x64 QEMU 게스트는 에디터 Scene Play가 통과했고, Release·Native AOT는 에뮬레이션이 느려 300프레임 재생 검사에 실패했다. 1,200프레임 진단 실행에서는 네 배포본 모두 4 ms 로딩의 최장 프레임이 더 짧았고 취소 후 시작 상태로 돌아왔다. [사용법](manual/scene-streaming.md#큰-방을-여러-프레임에-나눠-읽기) · [검증 기록](../engine/tests/results/split-loading/README.md) |
| 10.8 | 충돌체와 스크립트를 가진 프리팹 풀링 | 구현 완료. 충돌체와 스크립트가 있는 프리팹 인스턴스를 `Rent`·`Return`으로 재사용하고, 반환할 때 부모·변환·활성 상태·충돌 접촉·태그·그룹·이벤트·저장 추적을 초기화하며 `OnRent`·`OnReturn`을 호출한다. BarrageLab 탄환은 macOS arm64 Release·Native AOT에서 풀링과 생성·제거 모두 7,200발 중 2,338발이 명중했고 프레임당 관리 할당은 약 841 KB에서 58 KB로 줄었다. Windows x64·Linux x64 QEMU 게스트의 Release·Native AOT에서도 명중·재사용 수가 macOS와 같았다. [사용법](manual/sprite-pools.md#충돌체와-스크립트가-있는-프리팹-풀) · [검증 기록](../engine/tests/results/prefab-pools/README.md) |

## 11. 게임 제작에서 아직 없는 범용 기능

2026-11-23 조사. 엔진 소스와 매뉴얼에 없는 기능 중 여러 장르가 함께 쓰는 것만 골랐다. 보류한 멀티플레이와 모바일·웹은 제외했다. 각 항목은 기존 샘플에서 Scene Play·Release·Native AOT로 확인하고, 게임 런타임에는 에디터 코드와 SkiaSharp를 넣지 않는다.

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 11.1 | 스크립트 대기와 순차 실행 | 예정. 게임 스크립트에서 게임 시간·UI 시간·프레임 수·조건을 기다린 뒤 이어서 실행한다. 오브젝트가 비활성화·제거되거나 씬이 바뀌면 대기를 함께 취소한다. KeyRoom 문이 열린 뒤 0.5초 기다려 안내를 띄우고, 대기 중 일시정지·한 틱 진행이 게임 시간을 따르는 것을 재생 입력으로 확인한다. |
| 11.2 | Play 중 스크립트 다시 불러오기 | 예정. Scene Play 중 게임 스크립트를 저장하면 다시 컴파일하고 필드 값을 유지한 채 새 코드로 바꾼다. 컴파일 오류가 나면 기존 코드로 계속 실행하고 Console에 줄 위치를 표시한다. 에디터 Scene Play 전용이며 배포본에는 영향이 없다. |
| 11.3 | 회전·스프링·슬라이더 조인트와 마찰 | 예정. 기존 거리 조인트에 회전 축(각도 제한·모터), 스프링, 직선 이동 조인트를 더하고 강체에 마찰 계수를 둔다. PlatformerLab에 흔들다리와 도르래 발판을 두고 같은 입력에서 세 실행 경로의 위치 기록이 같은지 확인한다. |
| 11.4 | 카메라 흔들림과 화면 전체 효과 | 예정. 카메라 흔들림·줌 펀치와 화면 페이드·플래시·색 보정을 SDL3 렌더 타깃으로 그린다. 접근성 설정에서 흔들림과 깜빡임 강도를 줄일 수 있다. CameraLab에서 효과를 켜고 끈 화면 캡처와 접근성 설정 저장을 확인한다. |
| 11.5 | 서식 있는 텍스트와 글자 단위 표시 | 예정. UI 텍스트와 대화에서 색·굵기·크기·인라인 아이콘 태그를 쓰고, 글자를 한 자씩 표시하다가 입력하면 끝까지 넘긴다. 번역 테이블과 한글 줄바꿈, 미리 생성한 글리프 시트와 함께 동작한다. InventoryLab 약초상 대화에서 확인한다. |
| 11.6 | 게임패드 진동과 마우스 커서 | 예정. 기기별 진동 세기·시간과 접근성 설정의 진동 끄기, 프로젝트 PNG로 만든 커서와 핫스팟을 지원한다. 진동은 실제 패드가 필요하므로 이 Mac에서 연결한 패드로 확인하고, 게스트에서는 호출 기록만 비교한다. |
| 11.7 | 선·궤적 그리기와 경로 편집 | 예정. 점 목록으로 두께·색 그라데이션이 있는 선을 그리고, 움직이는 오브젝트 뒤에 궤적을 남긴다. Scene에서 곡선 경로를 편집하고 오브젝트가 일정 속도로 따라가게 한다. BarrageLab 레이저와 이동 발판 경로로 확인한다. |
| 11.8 | Aseprite 파일 임포트 | 예정. `.aseprite`의 프레임·태그·지속 시간을 스프라이트 시트와 애니메이션 클립으로 가져오고, 원본이 바뀌면 기존 자동 재임포트로 갱신한다. 임포터는 에디터·빌드 도구에만 두고 게임에는 만들어진 PNG와 클립만 포함한다. |
| 11.9 | 게임 스크립트 자동 테스트 | 예정. 프로젝트 안에 테스트 스크립트를 두고 `./monoon test project <경로>`가 씬을 헤드리스로 열어 입력을 넣고 상태를 단언한다. 기존 입력 재생을 재사용하고, 실패하면 프레임 번호와 첫 예외를 남긴다. KeyRoom 상자·문 규칙을 테스트로 옮겨 확인한다. |
| 11.10 | 프로젝트별 에디터 확장 | 예정. 프로젝트의 에디터 전용 스크립트 폴더에서 메뉴 명령, 컴포넌트 Inspector, 도킹 패널을 추가한다. 이 폴더는 게임 빌드에서 제외한다. 기존 프리팹 배치 확장 명령을 이 방식으로 옮기고, 배포본에 해당 어셈블리가 없는지 확인한다. |
| 11.11 | 오브젝트 복사·붙여넣기와 씬 간 이동 | 예정. Hierarchy·Scene에서 오브젝트를 복사해 같은 씬이나 다른 씬에 붙여 넣는다. 자식·컴포넌트·프리팹 연결·참조를 유지하고, 다른 씬을 가리키는 참조는 끊긴 참조로 표시한다. Undo 한 번으로 되돌린다. |
| 11.12 | 배포본 서명과 설치 파일 | 예정. macOS 앱 서명·공증 단계, Windows 설치 파일과 코드 서명, Linux 압축 배포본의 실행 권한·데스크톱 항목을 빌드 프로필 단계로 추가한다. 인증서와 비밀번호는 사용자가 지정한 위치에서 읽고 저장소에 넣지 않는다. |

노멀 맵 조명과 그림자는 SDL3 렌더러에서 사용자 셰이더를 쓸 수 있는지 먼저 확인해야 하므로 후보로만 남긴다.

## 12. 에디터 화면 개편

현재 에디터는 Hierarchy·Project(176px), Scene, Inspector(218px)가 고정 폭 3열이고 분할선과 탭이 없다. Console·Animator·Tileset·클립·입력 액션·대화·데이터 편집은 각각 별도 창으로 열린다. 이를 패널 단위로 나누어 탭으로 묶고 크기와 위치를 바꿀 수 있게 한 뒤, 전문 엔진 수준의 일관된 디자인을 적용한다.

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 12.1 | 도킹 기반과 Scene 뷰 재부착 검증 | 구현 완료. Dock.Avalonia는 테마가 별도 패키지에 있어 채택하지 않고, 에디터 안에 Grid·분할선·탭 묶음으로 도킹(`DockHost`)을 직접 만들었다. 추가한 패키지는 없다. 패널은 한 번 만들어 위치만 옮기므로 Mac Scene 뷰(SDL NSView)가 탭 이동·분할·플로팅 창 이동 뒤에도 렌더러와 Play 상태를 유지하는 것을 macOS arm64에서 확인했다. 게임 런타임(Engine.Core·Desktop·Player)은 바뀌지 않았다. [사용법](manual/editor.md#패널-배치와-레이아웃) · [검증 기록](../engine/tests/results/editor-docking/README.md) |
| 12.2 | 기본 패널 분리와 탭·분할 | 구현 완료. Hierarchy·Project·Scene·Inspector·Console을 독립 패널로 나눴다. 분할선으로 크기를 바꾸고, 탭을 끌어 다른 영역에 넣거나 상하좌우로 분할한다. 끄는 동안 놓일 위치는 팝업으로만 표시해 주변 컨트롤을 밀지 않는다. 패널 최소 크기 덕분에 Inspector 레이블·입력 열 간격(60px)이 1280·1600·1920px과 긴 이름에서 그대로임을 세 OS에서 확인했다. [사용법](manual/editor.md#패널-배치와-레이아웃) · [검증 기록](../engine/tests/results/editor-docking/README.md) |
| 12.3 | 레이아웃 저장과 Window 메뉴 | 구현 완료. 레이아웃은 사용자 폴더의 `editor-layout.json`에 저장하고 다음 실행에 복원한다. Window 메뉴에서 닫은 패널 다시 열기, 기본·편집·애니메이션 레이아웃 전환, 현재 레이아웃 저장, 기본값 초기화를 제공한다. 화면보다 큰 저장값은 기본 위치로 되돌린다. 저장한 배치·창 크기·플로팅 창 위치가 재실행 뒤 같게 복원되는 것을 세 OS에서 확인했다. [사용법](manual/editor.md#패널-배치와-레이아웃) · [검증 기록](../engine/tests/results/editor-docking/README.md) |
| 12.4 | 별도 창의 패널화와 플로팅 창 | 구현 완료. Console·Profiler·Animator·클립 타임라인·Tileset·입력 액션·대화·데이터 테이블을 도킹 패널로 바꿨다. 패널은 별도 창으로 띄우고 다시 붙일 수 있으며, 같은 패널을 다시 열면 기존 탭으로 이동한다. 에셋 종류마다 편집 패널은 하나이고, 다시 붙인 패널은 기본 위치로 돌아간다. [사용법](manual/editor.md#플로팅-창) · [검증 기록](../engine/tests/results/editor-docking/README.md) |
| 12.5 | Scene·Game 탭 분리와 Play 도구 모음 | 구현 완료. Scene 옆에 Game 탭을 두고 Play를 누르면 Game 탭이 앞으로 온다. Game 탭은 해상도·확대 배율·통계 표시를 고른다. 상단 가운데에 Play·일시정지·한 틱 진행, 왼쪽에 이동·회전·크기·Rect 도구와 스냅을 둔다. Game 탭은 두 번째 렌더러를 만들지 않고 같은 Scene 뷰를 옮겨 쓰므로 Play 중 Scene과 Game을 나란히 볼 수는 없다. [사용법](manual/editor.md#scene과-game-탭-도구-모음) · [검증 기록](../engine/tests/results/editor-docking/README.md) |
| 12.6 | 디자인 토큰과 아이콘 | 구현 완료. 색·글자 크기·간격(4px 단위)·모서리·선 두께를 `EditorStyles.axaml` 토큰으로 정리했고, 코드로 만드는 패널은 `EditorTokens`로 같은 값을 읽는다. 컴포넌트·에셋 종류·도구 모음에 저장소 안의 16px 벡터 아이콘 48개를 쓴다. 용량을 늘리지 않도록 Inter·Pretendard는 넣지 않고 OS 시스템 글꼴을 쓴다. 호버·선택·포커스·비활성·드래그 상태를 같은 규칙으로 표시한다. [검증 기록](../engine/tests/results/editor-theme/README.md) |
| 12.7 | 패널별 세부 디자인 | 구현 완료. Hierarchy에 종류 아이콘·검색 필터·표시/잠금 토글을 두었다. 표시·잠금 상태는 사용자 폴더의 `editor-state.json`에만 저장되어 씬 파일과 Release 빌드는 바이트 단위로 그대로다. Project에 폴더 트리, 썸네일 격자·목록 전환, 경로 표시줄을 두었다. Inspector는 컴포넌트 머리말에 아이콘·메뉴를 두고 접기 상태를 기억한다. 엔진 컴포넌트에 활성 속성이 없어 활성 토글은 넣지 않았다. Console은 종류별 개수·필터·검색·같은 메시지 묶기를 지원한다. [사용법](manual/editor.md#네-패널에서-하는-일) · [검증 기록](../engine/tests/results/editor-panels/README.md) |
| 12.8 | 3개 OS 패키지 회귀와 캡처 | 구현 완료. macOS arm64 에디터와 Windows x64·Linux x64 QEMU 게스트의 패키지 에디터에서 도킹 다섯 단계, 테마, 패널 세부, 프리팹 편집, 6.8 에디터 조작 회귀가 통과했고 측정값과 저장 파일이 세 OS에서 같았다. 1280·1600·1920px과 긴 이름 캡처를 남겼다. 100% 배율에서만 드러나는 1px 정렬 오류 세 건(분할 위치, 패널 머리줄, 경로 표시줄)을 고쳤다. 조작키 설정 Scene Play는 두 게스트에서 원래 시간으로 실패했다(10.2와 같은 에뮬레이션 지연, 대기를 늘린 진단 실행은 통과). 125%·150% 배율, 실제 마우스 끌기, 물리 GPU는 확인하지 않았다. [검증 기록](../engine/tests/results/editor-ui-x64/README.md) |

### 에디터 디자인 기준

- 어두운 바탕 3단계를 쓴다(창 바탕 · 패널 · 입력 필드). 강조색은 선택·포커스·Play 상태에만 쓰고, 경고·오류는 따로 정한 노랑·빨강만 쓴다.
- 기본 글자는 12px로 둔다. 패널 제목과 탭은 12px SemiBold, 보조 정보는 11px 회색으로 한다. 행 높이는 22px, 입력 필드는 24px, 패널 안쪽 여백은 8px로 맞춘다.
- Inspector의 레이블 열은 기존처럼 고정 폭을 쓰고 패널 너비에 따라 입력 열만 늘린다. 긴 이름은 말줄임과 툴팁으로 처리한다.
- 아이콘은 16px 한 벌로 통일하고 글자 없는 버튼에는 툴팁을 단다.
- 매 단계마다 KeyRoom을 연 같은 화면을 캡처해 이전 단계와 비교한다.
