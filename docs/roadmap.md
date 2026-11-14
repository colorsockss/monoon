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
| 5.1 | 빌드·회귀 | 1차 [빌드 CLI와 GitHub Actions](manual/build.md#터미널에서-빌드와-회귀-검사) 및 [InventoryLab 실행 검증](../engine/tests/results/build-cli/README.md), 2차 [개발용·배포용 프로필과 별도 디버그 심볼](manual/build.md#개발용배포용-프로필과-디버그-심볼) 및 [Mac 배포본 실행·심볼 검증](../engine/tests/results/build-profiles/README.md) 완료. 3차 [헤드리스 입력 기록·재생과 CI 회귀 검사](manual/build.md#헤드리스-입력-리플레이), [InventoryLab 실제 플레이와 Release·Native AOT 저장 비교](../engine/tests/results/input-replay/README.md) 완료. 4차 [기존 Windows·Linux x64 VM에서 InventoryLab Release·Native AOT 빌드와 Mac 기준 저장 비교](../engine/tests/results/input-replay-x64/README.md) 완료. 추가 샘플 회귀 범위는 확장 가능 |
| 5.2 | 배포·유지보수 | [데스크톱 3종 지속 검증](manual/build.md#github-actions), [로컬 콘텐츠 번들](manual/build.md#콘텐츠-번들), [데이터 모드](manual/build.md#데이터-모드), [콘텐츠 업데이트](manual/build.md#콘텐츠-업데이트), [크래시 진단](manual/build.md#크래시-진단) 구현. [InventoryLab 번들 검증](../engine/tests/results/content-bundles/README.md) · [모드 전환 검증](../engine/tests/results/data-mods/README.md) · [업데이트 적용과 실패 복구 검증](../engine/tests/results/content-updates/README.md) · [Release와 Native AOT의 오류 줄 대조](../engine/tests/results/crash-diagnostics/README.md). [InventoryLab API 문서](manual/api.md)와 [기존 프로젝트 업그레이드 가이드](manual/upgrade.md) 작성 완료. [콘텐츠 버전 31 → 35 macOS·Windows·Linux 빌드·저장 검증](verification/inventorylab-upgrade.md) |
| 5.2 | 모바일·웹 타깃 | **보류.** BRUTAL이 Android, iOS, 웹을 각각 공식 지원할 때 해당 타깃의 개발을 재개한다. |

각 단계는 에디터 조작과 실제 빌드에서 같은 결과를 내는 작은 샘플로 검증한다. KeyRoom 다음에는 플랫포머로 물리·카메라를, 보드 퍼즐로 입력·UI·저장을 검증한다.
