# 게임플레이 런타임 구현 기록

범위: 오브젝트·Transform·스크립팅·시간·입력·충돌과 물리·길찾기·절차 생성·씬과 리소스·저장. 대괄호는 2026-11-25에 로드맵에서 뺀 예전 로드맵 번호다.
사용법은 매뉴얼에, 측정값과 화면은 결과 README에 있다. 이 문서는 어떻게 만들었는지와 기록에 남은 이유만 모은다. 기록에 이유가 없으면 적지 않는다.

- [공통 원칙](#공통-원칙)
- [오브젝트와 프리팹](#오브젝트와-프리팹)
- [Transform](#transform)
- [스크립팅](#스크립팅)
- [시간](#시간)
- [입력](#입력)
- [충돌과 물리](#충돌과-물리)
- [길찾기](#길찾기)
- [절차 생성·턴·리듬·대량 스프라이트](#절차-생성턴리듬대량-스프라이트)
- [씬과 리소스](#씬과-리소스)
- [데이터와 저장](#데이터와-저장)

## 공통 원칙

- 런타임 코드는 [Engine.Core](../../engine/src/Engine.Core)에 있다. 에디터 Scene Play, 헤드리스 검사, Release 플레이어, Native AOT 키트가 모두 같은 Engine.Core를 쓴다. 에디터 전용 기능(스크립트 다시 불러오기, Scene 기즈모, Inspector)은 Engine.Editor·Engine.Authoring에 두고 배포본에 넣지 않는다(AGENTS.md).
- 시뮬레이션은 게임 시간 1/60초 고정 틱이다. 같은 입력이면 Scene Play·Release·Native AOT의 기록이 바이트 단위로 같아야 한다는 것이 대부분 기능의 합격 기준이다. 그래서 스레드·타임스탬프 대신 틱 수로 세고, 동점은 ID·셀 번호로 정한다.
- 결정성 계산 규칙: `Affine2D.Transform`·`TransformNormal`·`Lerp`는 곱셈과 덧셈을 따로 반올림한다. .NET `Vector2.Transform`·`Lerp`는 CPU에 FMA가 있을 때만 합쳐 계산해 x64에서 JIT(Release)과 Native AOT 결과가 갈렸다(Linux 게스트의 Joints·Paths). 처음에는 `MathF.FusedMultiplyAdd`로 맞췄지만 FMA 없는 x64 AOT에서 소프트웨어 fma로 느려, 사용자 결정으로 분리 계산을 택했다(arm64에서 약 39% 빠름). `MathF.SinCos`도 macOS arm64 JIT과 AOT가 입력의 약 2.6%에서 달라 쓰지 않는다. `./monoon test`의 [DeterminismScanChecks](../../engine/tests/Engine.Checks/DeterminismScanChecks.cs)가 엔진과 샘플 스크립트에서 금지 API를 찾는다(로드맵 결정 15: 정적 스캔만, osx-x64 비교는 보류). [x64 결정론](../../engine/tests/results/x64-determinism/README.md) · [사용 규칙](../manual/transform-2d.md)
- 런타임은 리플렉션을 쓰지 않는다. 스크립트 생성·필드 값·이벤트 연결은 빌드 때 Roslyn으로 만든 코드가 처리한다([스크립팅](#스크립팅)). 월드 체크포인트와 스크립트 필드 값은 `JsonSerializer` 없이 직접 읽고 쓴다. 변환기 계층이 빠져 KeyRoom Native AOT 실행 파일이 8,321,968 → 7,575,072 B가 됐다(커밋 `a0b8ef7`).

## 오브젝트와 프리팹

**런타임 생성·제거 [1.1].** [RuntimeObjects.cs](../../engine/src/Engine.Core/RuntimeObjects.cs)의 `IGameObjects`가 `Instantiate`·`Destroy`·`SetActive`를 제공한다. 생성·제거는 그 틱의 고정 업데이트 끝에 한 번에 반영하고(렌더·Hierarchy 갱신), 새 오브젝트의 스크립트와 충돌은 다음 업데이트부터 돈다. 프리팹의 각 오브젝트는 새 GUID를 받고, 프리팹 내부 참조는 같은 인스턴스 안으로 다시 연결된다. 월드마다 프리팹 YAML을 처음 한 번만 읽고 템플릿을 복제한다(경로와 수정 시각이 같으면 다시 읽지 않음). 생성마다 프리팹 파일을 읽던 비용을 없애려고 넣었다. [perf-scene](../../engine/tests/results/perf-scene/README.md) · [매뉴얼](../manual/runtime-prefabs.md) · [실행 기록](../../engine/tests/results/runtime-prefabs/README.md)

**태그·그룹 [9.2].** [ObjectGroups.cs](../../engine/src/Engine.Core/ObjectGroups.cs)가 오브젝트 하나에 태그 하나와 그룹 여러 개를 두고 등록 순서의 멤버 목록을 유지한다. 돌려준 목록은 생성·제거를 따라가고, 순회 중 제거는 시작 시점의 ID로 계속한다. 추가 씬을 내리면 그 씬의 멤버만 빠진다. Play의 Debug 패널이 같은 조회를 쓴다. [매뉴얼](../manual/runtime-prefabs.md#태그그룹으로-오브젝트-찾기) · [세 OS 에디터 기록](../../engine/tests/results/object-groups/README.md)

**충돌체·스크립트가 있는 프리팹 풀 [10.8].** [PrefabPools.cs](../../engine/src/Engine.Core/PrefabPools.cs)의 `Rent`·`Return`. 반환할 때 프리팹의 로컬 값을 기준 자세로 부모·변환·활성·충돌 접촉·태그·그룹·이벤트·저장 추적을 되돌리고 `OnRent`·`OnReturn`을 부른다. 풀에 있는 인스턴스는 월드 체크포인트에 넣지 않는다. BarrageLab에서 프레임당 관리 할당이 약 841 KB → 58 KB가 됐고 명중 수는 생성·제거와 같았다. [매뉴얼](../manual/sprite-pools.md#충돌체와-스크립트가-있는-프리팹-풀) · [실측](../../engine/tests/results/prefab-pools/README.md)

## Transform

**Transform 2D [1.2].** [Transform2D.cs](../../engine/src/Engine.Core/Transform2D.cs)는 위치·회전(시계 방향 도)·배율·피벗과 기울기(shear)를 가진 2D 아핀 변환이다. 부모를 바꿀 때 보이는 모양을 유지하려면 비균일 배율·반전 부모 아래에서 기울기가 필요해 씬·프리팹에 저장한다. 화면 UI는 Transform이 아니라 Rect와 앵커로 배치한다. [매뉴얼](../manual/transform-2d.md) · [저장·재열기 기록](../../engine/tests/results/transform-2d/README.md)

**Play 중 변환과 부모 변경 [8.1].** [RuntimeTransforms.cs](../../engine/src/Engine.Core/RuntimeTransforms.cs)의 `TryGetLocalTransform`·`SetLocalTransform`·`SetParent(keepWorld)`. 같은 틱에 자손의 월드 행렬, 공간 격자, 충돌 도형을 갱신한다. 순환·다른 씬 부모·역변환 불가는 `false`로 거부하고 상태를 유지한다. 부모·로컬 변환이 바뀐 저장은 월드 데이터 버전 2로 쓰고 버전 1도 읽는다. 16.3 작업 때 호출마다 만들던 `SceneObject` 복사와 충돌체 없는 오브젝트의 형상 재계산을 없앴다. [매뉴얼](../manual/transform-2d.md#play-중-변환과-부모-변경) · [실행 기록](../../engine/tests/results/runtime-transforms/README.md)

## 스크립팅

**컴파일과 생명주기 [1.4].** 게임은 `Scripts/**/*.cs`(와 Project 패널에서 옮긴 스크립트)를 한 어셈블리로 컴파일한다. [ProjectScripts.cs](../../engine/src/Engine.Authoring/ProjectScripts.cs)가 Roslyn 의미 모델로 파일명과 같은 공개 `GameBehaviour` 클래스를 고르고, 필드 목록·이벤트·처리기를 읽어 `MoNoon.Generated.ProjectScriptFactory`(생성·필드 값 적용 코드)를 만들어 함께 컴파일한다. Release는 `MoNoon.GameScripts.dll` 하나, Native AOT는 실행 파일에 정적으로 들어간다. 생성 코드가 [ScriptValues](../../engine/src/Engine.Core/ScriptValues.cs)로 값을 읽으므로 런타임 리플렉션이 없다(AGENTS.md의 소스 생성기 규칙). 생명주기는 [ScriptLifecycle.cs](../../engine/src/Engine.Core/ScriptLifecycle.cs): 켤 때는 부모부터, 끌 때·제거할 때는 자식부터, 같은 상태를 두 번 지정해도 이벤트는 한 번. [매뉴얼](../manual/scripting.md) · [기록](../../engine/tests/results/scripting/README.md)

**컴포넌트 속성 바꾸기 [6.1].** `SetSprite`·`SetSpriteTint`·`SetColliderEnabled`는 실행 중 인스턴스만 바꾼다. 원본 씬은 그대로라 Stop하면 돌아온다(KeyRoom 씬 파일의 SHA-256이 실행 전후 같음). 내장 컴포넌트 `GetComponent`는 설정의 복사본을 돌려줘 스크립트가 실수로 렌더·충돌 설정을 바꾸지 못한다. [매뉴얼](../manual/scripting.md#플레이-중-스프라이트와-충돌체-바꾸기) · [기록](../../engine/tests/results/runtime-components/README.md)

**게임 정의 이벤트 [7.3].** 발신은 인자 없는 공개 `event Action`, 수신은 `[GameEventHandler]` 메서드다. 빌더가 이벤트와 처리기 목록을 생성 코드에 넣고, 씬에는 (이벤트, 대상 오브젝트, 스크립트, 메서드) 연결을 YAML로 둔다. [GameEvents.cs](../../engine/src/Engine.Core/GameEvents.cs)가 등록 순서대로 활성 대상만 부르고, 앞선 처리기가 대상을 지우면 건너뛴다. 프리팹 안의 연결은 인스턴스마다 다시 잇는다. [매뉴얼](../manual/scripting.md#게임-정의-이벤트) · [기록](../../engine/tests/results/game-events/README.md)

**필드 이름 바꾸기 [10.1].** `[FormerlySerializedAs]`를 빌더([ProjectScripts.FieldNames.cs](../../engine/src/Engine.Authoring/ProjectScripts.FieldNames.cs))가 읽어 씬·프리팹·인스턴스 오버라이드의 이전 이름 값을 새 필드로 잇는다. 파일을 일괄 변환하지 않고 저장할 때 새 이름으로 쓴다. 현재 이름이 있으면 그 값을, 없으면 선언 순서대로 처음 찾은 값을 쓴다. [매뉴얼](../manual/scripting.md#inspector-필드-이름-바꾸기) · [데스크톱 3종 기록](../../engine/tests/results/script-field-renames/README.md)

**대기와 순차 실행 [11.1].** [ScriptRoutines.cs](../../engine/src/Engine.Core/ScriptRoutines.cs)의 `StartRoutine`은 `IEnumerable<ScriptWait>` 반복자를 게임 루프가 시작 순서대로 이어 실행한다. 게임 시간 대기는 스크립트 `FixedUpdate`·접촉 이벤트 다음, 구조 변경 반영 전에, UI 시간 대기는 `UpdateUi` 다음에 진행한다. 스레드·`async`를 쓰지 않아 Native AOT에서도 리플렉션·동적 코드 없이 같은 순서가 된다. `Brutal.Concurrency`의 작업 시스템은 워커 스레드 병렬 실행용이라 게임 틱과 같은 스레드에서 순서대로 잇는 이 기능에 맞지 않았다. [매뉴얼](../manual/scripting.md#기다린-뒤-이어서-실행하기) · [기록](../../engine/tests/results/script-waits/README.md)

**Play 중 다시 불러오기 [11.2].** 에디터 전용. Scene Play는 스크립트를 수집 가능한 `AssemblyLoadContext`(`ScriptRuntime`)에 올리고, 0.75초 감시 타이머가 변경을 보면 백그라운드에서 다시 컴파일한다. 성공하면 [ScriptReload.cs](../../engine/src/Engine.Authoring/ScriptReload.cs)가 모든 동작의 새 인스턴스를 먼저 다 만든 뒤(하나라도 실패하면 아무것도 바꾸지 않음) 필드를 (선언 타입, 이름)으로 옮기고 이전 컨텍스트를 내린다. 루틴은 이전 코드의 반복자라 모두 멈춘다. BRUTAL에 핫 리로드가 없어 기존 Roslyn 컴파일과 로드 컨텍스트를 재사용했다. [매뉴얼](../manual/scripting.md#play-중-스크립트-다시-불러오기) · [기록](../../engine/tests/results/script-reload/README.md)

**트윈 API [16.3].** [ScriptTweens.cs](../../engine/src/Engine.Core/ScriptTweens.cs)의 `IGameObjects.Tween(id, TweenClock)`. 트랙과 단계를 구조체 배열 풀에 두고 세대 번호로 핸들을 확인해 재생 중 할당이 0이다. 게임 시간 트윈은 루틴과 같은 위치에서 진행한다. 이동·배율·회전은 `SetLocalTransform`과 같은 경로, 색·알파는 컴포넌트 키 클립(9.6)과 같은 값을 쓰므로 같은 틱에 클립이 Tint·Opacity를 재생하면 클립이 이긴다. `UiRect`는 UI 배치 결과에 더하는 오프셋(`SetBoxOffset`)이다. BRUTAL에는 트윈 기능이 없다. 16.2·16.8과 합쳐 KeyRoom Native AOT +66 KB. 기존 Position Tween 2D 컴포넌트는 그대로 둔다. [매뉴얼](../manual/scripting.md#트윈으로-값-움직이기) · [기록](../../engine/tests/results/engine-features-1/README.md)

**디버그 그리기 [16.14].** [ScriptDebug.cs](../../engine/src/Engine.Core/ScriptDebug.cs)의 `Line`·`Circle`·`Text`는 `[Conditional("MONOON_DEBUG_DRAW")]`이다. Scene Play와 개발용 게시만 이 기호로 스크립트를 컴파일하므로 배포용에서는 호출과 인수 계산이 사라진다. 엔진 쪽 그리기는 기능 스위치 `MoNoon.ScriptDebugDraw`로 배포용 ILLink·ILC가 잘라 낸다. 배포 프로필에서는 빈 함수로 둔다는 계획(E-14)을 두 단계로 지켰고, CameraLab 배포본의 파일 크기와 `__managedcode`가 같았다(증가 0 B). 도형 목록은 FixedUpdate·UpdateUi 각각 4,096개 상한이고 용량을 재사용한다. [매뉴얼](../manual/scripting.md#디버그-선원텍스트-그리기) · [기록](../../engine/tests/results/engine-features-2/README.md#1614-스크립트-디버그-그리기)

## 시간

**일시정지·한 틱·배속·타이머 [1.5].** [GameTime.cs](../../engine/src/Engine.Core/GameTime.cs)가 게임 시간(`FixedStep` 1/60, `Scale` 0 초과 8 이하)과 UI 시간을 따로 센다. [GameLoop.cs](../../engine/src/Engine.Core/GameLoop.cs)는 프레임당 최대 0.1초에 배속을 곱한 만큼만 따라가 긴 프레임 뒤에 틱이 몰리지 않게 한다. 배속은 실제 1초 동안의 고정 틱 수를 바꾸는 방식이라 틱 하나의 계산은 배속과 무관하다. `GameTimer`는 도메인(Game·Ui)과 반복을 고르고, 여러 주기가 지나도 `Consume()`은 한 번만 참이다. 창이 비활성이면 시간을 멈춘다. [매뉴얼](../manual/time.md) · [기록](../../engine/tests/results/time/README.md)

## 입력

**입력 액션 [1.3].** [InputActions.cs](../../engine/src/Engine.Core/InputActions.cs)가 프로젝트 `GameSettings`의 액션 → 컨트롤 목록을 읽어 틱마다 `GameInput`을 만든다. 게임 UI가 먼저 입력을 받고([GameInputRouter.cs](../../engine/src/Engine.Core/GameInputRouter.cs)), UI가 쓴 클릭·키는 같은 틱의 게임 액션을 누르지 않는다. 패드는 SDL이 인식한 첫 게임패드 하나, 스틱 20%·트리거 10% 데드존. 창 입력은 BRUTAL `Brutal.Sdl`로 받는다. 예전 Left·Right·Up·Down·Interact 설정은 읽어서 액션 목록으로 저장한다. [매뉴얼](../manual/input-actions.md) · [기록](../../engine/tests/results/input-actions/README.md)

**입력 모드 [6.5].** [ScreenUi.Modes.cs](../../engine/src/Engine.Core/ScreenUi.Modes.cs)가 **Input mode root** 패널의 스택을 관리한다. 맨 위 모드와 그 자식만 클릭·포커스·`tick.Input`을 받고, 나머지 스크립트는 빈 입력으로 계속 업데이트되어 애니메이션·물리는 멈추지 않는다. 닫으면 이전 포커스를 되돌린다. [매뉴얼](../manual/input-actions.md#입력-모드-전환과-복귀) · [기록](../../engine/tests/results/input-modes/README.md)

**월드 오브젝트 클릭·드래그 [8.4].** [WorldPointer.cs](../../engine/src/Engine.Core/WorldPointer.cs). 왼쪽 버튼을 누르면 게임 UI를 먼저 검사하고, 그 점을 그리는 최상단 카메라에서 그리기 순서상 가장 앞의 `IWorldPointerHandler`를 찾는다. 드래그 동안 대상·카메라·잡은 로컬 점을 고정해 카메라만 움직여도 잡은 점이 포인터 아래에 남는다. 4 논리 픽셀 미만으로 움직이고 놓으면 Click이다. [매뉴얼](../manual/input-actions.md#월드-오브젝트-클릭과-드래그) · [기록](../../engine/tests/results/world-pointer/README.md)

**플레이어 조작키 설정 [10.2].** [InputRebinding.cs](../../engine/src/Engine.Core/InputRebinding.cs)의 `BeginRebind`는 다음 새 입력을 게임 UI보다 먼저 가로챈다. 다른 액션이 쓰던 입력이면 두 액션이 교환한다. `SaveInputBindings`는 프로젝트 기본값과 다른 액션만 `player.settings`의 `inputBindings`에 써서 프로젝트가 새 액션을 더해도 저장이 깨지지 않게 한다. 저장하는 것은 컨트롤뿐이고 상호작용 규칙(16.12)은 프로젝트 값을 따른다. Windows·Linux x64 게스트의 Release·Native AOT 트레이스와 저장 파일이 macOS와 바이트 단위로 같았다. [매뉴얼](../manual/input-actions.md#플레이어-조작키-설정과-저장) · [기록](../../engine/tests/results/player-rebinding/README.md)

**진동과 커서 [11.6].** [ScreenEffects.cs](../../engine/src/Engine.Core/ScreenEffects.cs)의 `Rumble`·`SetCursor`는 요청만 기록하고, 플레이어가 `SDL_RumbleGamepad`·`SDL_CreateColorCursor`로 처리한다. Mac Scene Play는 Scene 패널(NSView)이 마우스를 직접 받으므로 같은 픽셀로 `NSCursor`를 만든다. 접근성 설정의 진동 끄기를 엔진에서 거른다. 실제 패드가 없어(사용자 결정) SDL 가상 패드와 `MONOON_FEEDBACK_LOG` 기록으로만 확인했다. [매뉴얼](../manual/input-actions.md#게임패드-진동과-마우스-커서) · [기록](../../engine/tests/results/rumble-cursor/README.md)

**상호작용·버퍼·벡터 [16.12].** 액션의 `interaction`(Hold·Tap·DoubleTap)·`ticks`·`buffer`를 `InputActions`의 게임 틱 수로 판정한다. 타임스탬프가 아니라 틱으로 세서 실행 경로·CPU와 무관하다. 규칙이 있는 액션만 틱마다 판정하고, 벡터는 입력이 바뀐 틱에만 계산한다(누르고 있는 1,000틱에 0 B). `vectors`가 없으면 예전 네 이름으로 `Move`를 만들어 기존 프로젝트가 그대로 돈다. 첫 구현의 LINQ를 반복문으로 바꿔 AOT 크기를 줄였다. Input Actions 패널의 Interaction·Ticks·Buffer 칸과 Vector 목록은 뒤에 따로 넣었고, 패널이 저장한 파일은 YAML로 직접 쓴 파일과 바이트 단위로 같다. BRUTAL에는 입력 액션 계층이 없다(SDL 이벤트까지). [매뉴얼](../manual/input-actions.md#홀드탭더블탭과-입력-버퍼) · [런타임 기록](../../engine/tests/results/engine-features-2/README.md#1612-입력-상호작용과-버퍼) · [패널 기록](../../engine/tests/results/editor-input-audio/README.md)

## 충돌과 물리

**충돌 도형과 질의 [2.3].** [Physics2D.cs](../../engine/src/Engine.Core/Physics2D.cs)의 `Shape2D`·`PhysicsFilter`·`IPhysics2D`(Overlap·Raycast·Sweep)와 [ScenePhysics.cs](../../engine/src/Engine.Core/ScenePhysics.cs). 레이어 0–31과 양방향 마스크, 질의는 대상 레이어만 본다. 처음에는 손으로 쓴 SAT·원형 캐스트였고 11.3에서 Box2D 기하 함수로 바꿨다. 결과 순서는 거리, 같으면 ID와 셀 Y·X라서 경로마다 같다. [매뉴얼](../manual/collision.md) · [기록](../../engine/tests/results/collision/README.md)

**진입·유지·이탈 이벤트 [6.4].** [SceneContacts.cs](../../engine/src/Engine.Core/SceneContacts.cs)가 고정 틱의 이동·물리 뒤에 접촉 쌍을 비교해 양쪽에 Enter·Stay·Exit를 보낸다. 한 틱에 얇은 트리거를 통과하면 같은 틱에 Enter와 Exit, 같은 타일맵의 여러 셀은 접촉 하나다. Box2D로 옮긴 뒤에도 이 쌍 관리를 유지했다. Box2D 접촉 이벤트는 동적 바디가 없는 쌍(스크립트로 옮기는 콜라이더끼리, 키네마틱끼리)을 보고하지 않기 때문이다. 성능 묶음 A에서 왼쪽 끝 정렬을 틱 사이에 유지하고 충돌체 64개 이상이면 세로 행으로 나눠 후보를 찾게 바꿨다(순서는 전체 비교와 같음). [매뉴얼](../manual/collision.md#진입유지이탈-이벤트) · [기록](../../engine/tests/results/contact-events/README.md) · [perf-physics](../../engine/tests/results/perf-physics/README.md)

**공간 격자 [8.8].** 콜라이더가 32개 이상인 월드는 [SpatialGrid.cs](../../engine/src/Engine.Core/SpatialGrid.cs)·[ColliderGrid.cs](../../engine/src/Engine.Core/ColliderGrid.cs)의 64×64 월드 단위 격자로 겹침·레이·스윕 후보를 고른다. 256셀을 넘는 충돌체와 무한 범위는 별도 목록으로 항상 검사해 메모리를 제한한다. `QueryDiagnostics`로 후보 수를 읽고 격자를 꺼서 전체 순회와 대조할 수 있다. 1만 개에서 후보가 16,772,778 → 22,812로 줄고 적중 순서는 같았다. Box2D 월드 질의(`b2World_OverlapShape` 등)는 네이티브 콜백이 필요하고 트리거·타일 셀 결과·후보 수 진단을 그대로 줄 수 없어 쓰지 않고 이 격자를 유지했다. [매뉴얼](../manual/collision.md#대형-씬의-공간-인덱스) · [세 OS 기록](../../engine/tests/results/spatial-queries/README.md)

**강체·거리 조인트·캐릭터 [4.1 1차].** Rigidbody 2D(Dynamic·Kinematic·Static), 거리 조인트, Character Motor 2D(경사·점프·이동 발판). 캐릭터는 직접 움직이는 키네마틱 컨트롤러로, 처음에는 자체 해석기였다. [매뉴얼](../manual/platformer-physics.md) · [기록](../../engine/tests/results/platformer/README.md)

**타일별 충돌 도형과 단방향 발판 [9.7].** 타일셋의 타일마다 볼록 다각형과 방향이 있는 선분을 둔다([TileCollision.cs](../../engine/src/Engine.Core/TileCollision.cs)). 11.3 이후 단방향 면은 4점 Box2D 체인(한쪽 면만 충돌)이다. 타일 도형은 (타일맵, 셀)마다 월드 좌표로 한 번 만들어 두고, 셀이나 타일맵 변환이 바뀌면 다시 만든다(성능 묶음 G). 로컬 도형을 평행 이동하면 반올림이 달라져 기존 `WorldShape` 결과를 그대로 저장한다. [매뉴얼](../manual/tilemaps.md#타일별-충돌-도형과-단방향-발판) · [기록](../../engine/tests/results/tile-collision/README.md)

**Box2D 전환, 조인트와 마찰 [11.3].** BRUTAL 우선 원칙에 따라 자체 해석기와 충돌 판정을 `Ahwoo.BRUTAL.Box2D`(네이티브 box2d 3.1.1)로 옮겼다. 결정과 이유([전환 결정 표](../../engine/tests/results/physics-box2d/README.md#전환-결정)):

- Engine.Core가 패키지를 참조해 Scene Play·헤드리스 검사·Release·Native AOT 네 경로를 한 곳에서 덮는다.
- 길이 단위 1 = 1px. 반발 임계 25px/s(기존 해석기와 같음), 수면 끔, 작업 스레드 0(단일 스레드로 결정적), 고정 틱마다 `Step(dt, 4)`. 씬에 Rigidbody 2D가 있을 때만 월드를 만든다([ScenePhysicsWorld.cs](../../engine/src/Engine.Core/ScenePhysicsWorld.cs)).
- 질의는 Box2D 기하 함수(GJK 거리, 매니폴드, 형상 캐스트, 레이캐스트)를 쓰고 후보는 MoNoon 격자에서 고른다. 접촉 이벤트는 MoNoon 쌍 관리를 유지했다(위 6.4).
- Character Motor는 Box2D가 플랫포머에 권하는 키네마틱 컨트롤러로 남겼다. 발바닥 2px 위까지만 키네마틱 형상을 둬 무게 외의 힘으로 발판을 누르지 않는다.
- Box2D 3.1에 도르래 조인트가 없어 PlatformerLab은 Slider Joint 두 개와 스크립트로 만들었다.
- Box2D 다각형 한도 때문에 다각형 꼭짓점을 3–32에서 3–8로 줄였다. 새 조인트·마찰이 있는 씬은 스키마 29이고, 템플릿에 Box2D 표식이 없으면 빌드를 거부해 옛 해석기로 조용히 돌지 않게 했다.
- x64: Linux 게스트에서 처음에는 Native AOT만 달랐고 원인은 `Vector2.Transform`·`Lerp`의 FMA였다([공통 원칙](#공통-원칙)). 고친 뒤 Linux의 Release와 Native AOT는 같고, Mac과의 차이는 아키텍처가 다른 Box2D 네이티브 빌드 사이의 차이로 기록만 한다(Joints 300행 중 236행, 최대 0.001). 타일 충돌·Paths·Slopes·Spatial은 Mac과 같다. [Linux 회귀](../../engine/tests/results/next1-x64/README.md)

**물리 틱 할당 제거.** 16.4 측정이 찾은 고정 틱당 약 4 KB 할당(후보 반복자, UI 클릭 콜백, 회전 도형, 법선 목록)을 지웠다. Slopes 정지 상태 8,261 B/틱 → 0 B이고 `physics-alloc` 검사가 지킨다. [perf-physics](../../engine/tests/results/perf-physics/README.md#틱당-할당-제거-164-후속)

**캡슐·체인 충돌체 [16.4].** Capsule Collider 2D와 Edge Collider 2D(점 2–1024, Loop). BRUTAL Box2D의 `CreateCapsuleShape`·`CreateChain`과 캡슐·세그먼트 레이를 쓰고, 네이티브에 없는 인라인 `b2MakeCapsule`만 C#으로 옮겼다. 질의·이동·접촉·공간 격자·Box2D 월드가 모두 새 도형을 처리한다. 캡슐은 오브젝트 사각형의 긴 변을 따르고 방향·오프셋은 따로 두지 않았다(자식 오브젝트로 돌린다). 질의는 Edge를 양면으로 보지만 Box2D 체인은 한쪽 면만 막는다(Box2D 체인의 성질). 0.01px보다 짧은 선분은 Box2D가 법선을 만들 수 없어 버린다. 두 컴포넌트는 물리 기능으로 표시되어 쓰는 게임에만 Box2D가 들어간다(15.11). 씬 스키마 34. [매뉴얼](../manual/collision.md) · [기록](../../engine/tests/results/colliders-capsule-chain/README.md)

## 길찾기

**보행 길찾기와 AI [4.1 3차].** [Navigation2D.cs](../../engine/src/Engine.Core/Navigation2D.cs)의 `FindWalkPath`는 호출 시점의 충돌 도형(로드된 타일, 키네마틱 발판 현재 위치 포함)에서 Navigation Region 2D 안을 Spacing 간격으로 샘플링해 발밑·머리 위 여유를 검사하고 걸을 수 있는 지점을 A*로 잇는다. 경사 이음새에서 상자의 오르막 쪽 모서리가 먼저 지지하므로 양 발과 중심을 함께 검사하고, 직선 맞춤 대신 도달 가능한 높이를 전파한다. 점프·사다리·미래 발판 예약은 넣지 않았다. 순찰·추적·복귀는 엔진이 아니라 샘플 스크립트(Lookout)다(장르 규칙은 프로젝트 스크립트로 둔다는 로드맵 원칙). [매뉴얼](../manual/navigation.md) · [기록](../../engine/tests/results/navigation/README.md)

**탑다운 그리드 길찾기 [9.8].** [GridNavigation.cs](../../engine/src/Engine.Core/GridNavigation.cs): 행 우선 정수 비용, 4·8방향 A*, 걸음 비용 10·14 × 목적 칸 가중치, 대각선은 양옆이 열려 있을 때만, 동점은 칸 번호. `Revision`이 바뀌면 현재 칸에서 다시 찾는다. `TileNavigation`은 타일셋의 지형 비용(10.6)을 읽고 `SetTile`을 따라간다. [매뉴얼](../manual/navigation.md#탑다운-그리드-길찾기) · [기록](../../engine/tests/results/grid-navigation/README.md)

**탑다운 자유 이동 [16.15].** 계획(E-15)의 후보는 폴리곤 내비메시 또는 흐름장이었고, 구현은 기존 Navigation Region 2D를 칸으로 굽는 방식이다. [NavArea2D.cs](../../engine/src/Engine.Core/NavArea2D.cs)가 로드할 때 한 번(`BakeArea`) 정적 콜라이더와 Solid 타일에서 반지름(Agent W/2) 원이 들어가는 칸을 굽고(칸당 1 B), 8방향 정수 비용 A*(이진 힙, 세대 번호)를 시야 검사(string pulling)로 모서리 점만 남긴다. [NavCrowd2D.cs](../../engine/src/Engine.Core/NavCrowd2D.cs)는 분리·옆 비키기 회피(RVO 없음). 모든 버퍼를 생성 때 할당해 요청·장애물 변경·`Step`이 0 B다. `MaxPlansPerStep`(16)은 측정 뒤 넣었다. 문을 열 때 200명이 한 틱에 다시 찾으면 약 18 ms가 되기 때문이다. BRUTAL에 길찾기가 없어 직접 구현했고, `Brutal.Collections`는 버퍼 크기가 생성 때 정해져 관리 배열로 충분하고 Engine.Core 참조를 늘리지 않으려고 쓰지 않았다. 경로 요청 91 µs·0 B(기존 그리드 169 µs·65 KB). 쓰지 않는 게임의 Native AOT는 0 B. [매뉴얼](../manual/navigation.md#탑다운-자유-이동-길찾기) · [기록](../../engine/tests/results/navigation-free/README.md)

## 절차 생성·턴·리듬·대량 스프라이트

로드맵 4.3 "장르별 실행"의 네 단계다.

- **시드와 미로 [4.3 1차].** [SeededRandom.cs](../../engine/src/Engine.Core/SeededRandom.cs)는 SplitMix64 방식의 정수 난수열(버전 1)이다. `Fork(channel)`은 현재 뽑은 횟수가 아니라 원래 시드에서 파생해 보상 추첨을 늘려도 지형이 바뀌지 않게 한다. 채널은 `GetHashCode()`가 아닌 고정 숫자를 쓴다. [MazeLayout.cs](../../engine/src/Engine.Core/MazeLayout.cs)는 재귀 대신 반복 백트래킹이라 호출 스택 한도가 없고, 후보 순서와 동점 처리가 레이아웃 버전 1의 일부라 컬렉션 순회나 부동소수를 쓰지 않는다. `ReplaceTiles`는 실행 중 타일과 충돌을 바꾸고 원본 씬은 그대로 둔다. [매뉴얼](../manual/generation.md) · [기록](../../engine/tests/results/generation/README.md)
- **턴과 리플레이 [4.3 2차].** [GridTurns.cs](../../engine/src/Engine.Core/GridTurns.cs)의 `GridTurnRun`은 규칙 ID·생성기 버전·시드·시작 칸과 턴마다 명령·도착 좌표를 저장하고(최대 32,768턴, `latest.replay` YAML), 재생 중 결과가 기록과 다르면 `Diverged`로 첫 불일치 턴을 알린다. [매뉴얼](../manual/turn-replay.md) · [기록](../../engine/tests/results/turn-replay/README.md)
- **리듬 타이밍 [4.3 3차].** [RhythmTiming.cs](../../engine/src/Engine.Core/RhythmTiming.cs)는 48 kHz 출력 프레임 단위의 박자 격자다. [매뉴얼](../manual/rhythm.md) · [기록](../../engine/tests/results/rhythm/README.md)
- **대량 스프라이트 배칭·풀링 [4.3 4차].** [SpritePool2D.cs](../../engine/src/Engine.Core/SpritePool2D.cs). [매뉴얼](../manual/sprite-pools.md) · [기록](../../engine/tests/results/sprite-pools/README.md)

## 씬과 리소스

**비동기 전환·추가 씬·공유 에셋 [1.7].** [SceneStreaming.cs](../../engine/src/Engine.Core/SceneStreaming.cs)·[AdditiveScenes.cs](../../engine/src/Engine.Core/AdditiveScenes.cs)의 `LoadSceneAsync`·`AddSceneAsync`·`UnloadScene`은 `SceneOperation`을 돌려주고 한 번에 한 작업만 받는다. 추가 씬은 같은 월드에 등록되어 충돌·카메라·UI 입력을 공유한다. [SceneResources.cs](../../engine/src/Engine.Core/SceneResources.cs)는 파일마다 네이티브 리소스 하나를 참조 수로 관리해 마지막 참조가 사라질 때 SDL 텍스처·오디오를 해제한다. 세션 플래그와 플레이어 저장소는 전환 동안 유지한다. [매뉴얼](../manual/scene-streaming.md) · [기록](../../engine/tests/results/scene-streaming/README.md)

**나눠 읽기 [10.7].** 워커 스레드가 YAML과 PNG·오디오를 디코딩하고, 게임 스레드는 업데이트당 `SceneLoadBudgetMilliseconds`(기본 4 ms) 동안 오브젝트 생성과 SDL 업로드를 한다. 오브젝트 하나, 텍스처 한 장, 색인 생성, 월드 연결은 나누지 않는다. 취소하면 만든 오브젝트·업로드·디코딩 버퍼를 해제하고 게임 스레드는 워커를 기다리지 않는다. CollisionLab 1만 개 방 + 4096² 배경 4장의 최장 프레임이 Release 81 → 30 ms, Native AOT 79 → 22 ms. 워커 작업은 `Task.Run`·`Parallel.ForEach`로 한다. `Brutal.Concurrency`의 `JobScheduler`는 한 번에 한 묶음만 실행하고 완료 알림·결과·예외 처리가 없어 쓰지 않았다([BRUTAL 유지 결정](../brutal.md)). 예산 환경 변수는 개발용 플레이어와 Scene Play만 읽는다. [매뉴얼](../manual/scene-streaming.md#큰-방을-여러-프레임에-나눠-읽기) · [기록](../../engine/tests/results/split-loading/README.md)

## 데이터와 저장

**데이터 에셋과 세이브 슬롯 [1.6].** [GameData.cs](../../engine/src/Engine.Core/GameData.cs)는 `version`과 `values` 매핑의 YAML이고 [PlayerStorage.cs](../../engine/src/Engine.Core/PlayerStorage.cs)가 슬롯 1–999와 `player.settings`를 게임별 폴더(에디터 `Play/<id>`, 배포 `Games/<id>`)에 쓴다. YAML은 VYaml로 읽고 쓴다(BRUTAL에 YAML 패키지가 없다). 이전 스키마는 `migrate` 콜백을 한 단계씩 부르고 명시적으로 저장할 때만 파일을 바꾼다. 현재보다 높은 버전 슬롯을 덮어쓰지 않는다. [매뉴얼](../manual/player-data.md) · [기록](../../engine/tests/results/player-data/README.md)

**월드와 런타임 프리팹 상태 [7.4].** [WorldCheckpoints.cs](../../engine/src/Engine.Core/WorldCheckpoints.cs). 스크립트가 `TrackForSave(id, 필드)`로 저장할 오브젝트와 속성(존재·Transform·활성·충돌체)을 등록하고, `SaveWorld`가 주 씬별로 기록한다. 복원은 교체할 씬의 첫 `OnCreate` 전에 끝나고 살아 있는 인스턴스는 같은 GUID를 되찾는다. 프리팹 멤버 하나를 등록하면 같은 원본의 하위 오브젝트까지 따라온다. 추가 씬은 대상이 아니다. macOS arm64·Windows x64·Linux x64의 Release·Native AOT에서 저장·복원을 확인했다. JSON 본문은 [WorldCheckpointJsonCodec.cs](../../engine/src/Engine.Core/WorldCheckpointJsonCodec.cs)가 `JsonSerializer`와 같은 바이트로 직접 쓴다([공통 원칙](#공통-원칙)). [매뉴얼](../manual/player-data.md#월드와-런타임-프리팹-저장) · [기록](../../engine/tests/results/world-checkpoints/README.md)

**슬롯 복구 [8.5].** [PlayerStorage.Recovery.cs](../../engine/src/Engine.Core/PlayerStorage.Recovery.cs). 저장할 때 `slot-NNN.save`의 SHA-256을 `.sha256`에 쓰고 직전 정상본을 `.bak`로 남긴다. 체크섬 갱신 뒤에 게시가 멈춰도 건드리지 않은 파일은 검증을 통과하는 순서로 쓴다. 최신본이 없거나 체크섬이 다르면 `LoadSlot`이 직전 사본을 검사해 원래 바이트로 복원한다. 둘 다 손상되면 파일을 보존하고 오류를 낸다. [매뉴얼](../manual/player-data.md#직전-저장으로-복구하기) · [기록](../../engine/tests/results/slot-recovery/README.md)
