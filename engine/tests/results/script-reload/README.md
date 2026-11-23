# Play 중 스크립트 다시 불러오기 (로드맵 11.2)

macOS 27.0.1 (26A434) arm64, 이 Apple Silicon Mac에서 저장소 루트 기준으로 실행했다. 실행 대상은 패키지 에디터 `editor/MoNoon.app`의 Scene Play(소스 프로젝트, JIT)다. 다시 불러오기는 에디터 전용이라 Release·Native AOT에서는 해당 코드가 빠졌는지만 확인했다.

## 구조

- Scene Play는 이전과 같이 `ProjectScripts.ForScenePlay`로 컴파일하고, 수집 가능한(collectible) `AssemblyLoadContext` 하나(`ScriptRuntime`)에 올린다. 에디터의 기존 0.75초 스크립트 감시 타이머가 Play 중에는 게임 스크립트의 변경 표시(`ChangeStamp`)를 비교한다.
- 바뀌면 같은 디버그 계측(`SceneDebugServer.Instrument`)으로 새 `ProjectScripts`를 만들어 백그라운드에서 컴파일한다. 성공하면 UI 스레드에서 [ScriptReload](../../../src/Engine.Authoring/ScriptReload.cs)가 살아 있는 모든 오브젝트의 동작을 새 컨텍스트의 인스턴스로 먼저 전부 만든다(하나라도 실패하면 아무것도 바꾸지 않음). 그다음 필드를 옮기고, 오브젝트의 동작 슬롯·포인터 처리기·게임 이벤트 연결을 교체한 뒤 세션의 `ScriptRuntime`이 새 컨텍스트를 넘겨받고 이전 컨텍스트를 `Unload`한다. 이후 생성되는 프리팹·씬도 새 코드를 쓴다.
- 필드는 선언 타입 이름 + 필드 이름으로 대응시킨다. 같은 타입(엔진·.NET 타입)은 값을 그대로, 프로젝트 enum은 같은 이름의 멤버로, 교체된 동작 인스턴스 참조는 새 인스턴스로, `ComponentReference<프로젝트 타입>`은 같은 오브젝트 ID로 옮긴다. 새 필드는 기본값, 삭제한 필드는 버림, 타입이 바뀌었거나 옮길 수 없는 프로젝트 타입 값은 기본값으로 되돌리고 경고를 남긴다. 이벤트·람다(델리게이트) 필드는 이전 인스턴스를 가리키므로 옮기지 않는다.
- 대기 루틴(11.1 `ScriptWait`): 모든 루틴은 프로젝트 어셈블리의 반복기이므로 다시 불러올 때 전부 중지하고 Console에 개수를 알린다. `OnCreate`·`OnEnable`은 다시 호출하지 않는다.
- 컴파일 실패: 이전 코드로 계속 실행한다. 경고 한 줄과 진단마다 오류 한 줄을 남긴다. 절대 경로를 `./Scripts/…`로 줄여 표시하며, 기존 Console의 파일·줄 파싱이 적용되어 더블클릭하면 해당 줄을 연다.
- 엔진 런타임(Engine.Core)에는 다른 어셈블리가 호출하는 내부 연결점만 추가했다: `ScriptRuntime.Adopt`, `SceneGame.ReplaceBehaviours`·`LiveBodies`·`StopAllRoutines`, `GameSession.Scripts`·`LoadingScene`. 필드 이동과 교체 절차는 플레이어가 로드하지 않는 Engine.Authoring에 있다.
- BRUTAL에는 C# 스크립트 핫 리로드 기능이 없어([BRUTAL 기능 목록](../../../../docs/brutal.md)) 기존 Roslyn 컴파일과 수집 가능한 로드 컨텍스트를 재사용했다.

## Scene Play 검증 (패키지 에디터)

[KeyRoom.ScriptReload](../../KeyRoom.ScriptReload/README.md)가 KeyRoom 사본에 [ReloadProbe v1](../../KeyRoom.ScriptReload/ReloadProbe.v1.source)(틱마다 `Ticks += 1`, `float Speed = 2`, `OnCreate`에서 루틴 시작)을 붙이고, 시작 훅이 Play 후 게임 틱 30개를 진행한다. 이어서 [v2](../../KeyRoom.ScriptReload/ReloadProbe.v2.source)(`Ticks += 1000`, `int Speed = 3`, 새 필드 `Label`, `Version` 초기값 변경)를 저장하고 에디터의 감시 타이머가 다시 불러오기를 마칠 때까지 기다린다. 마지막으로 세미콜론 하나를 지운 파일을 저장한다.

| 검사 | 결과 |
| --- | --- |
| v1이 30틱을 세고 루틴 1개 실행 | 통과 |
| 저장 후 동작이 새 어셈블리의 인스턴스로 교체됨 | 통과 |
| `Ticks` 유지(30 → 38; 저장 감지까지 화면 틱이 더 진행됨), `Version`은 실행 중 값 `v1` 유지 | 통과 |
| 다음 한 틱에 `Ticks`가 정확히 +1000 (새 코드) | 통과 |
| 새 필드 `Label` = `added in v2`, `Speed`(float → int)는 3으로 초기화 | 통과 |
| Console 경고 `ReloadProbe.Speed: type changed from Single to Int32; the field was reset to its default.` | 통과 |
| KeyRoom 자체 동작 23개(오브젝트 19개)는 경고 없이 모든 필드 유지 | 통과 |
| 루틴 중지와 Console 안내 `Stopped 1 running routine(s) from the previous code.` | 통과 |
| 컴파일 오류가 `./Scripts/ReloadProbe.cs(11,71): error CS1002` 행으로 표시, 파일·줄 11 연결 | 통과 |
| 오류 뒤에도 Play 유지, 같은 인스턴스가 v2 코드로 +1000 | 통과 |

캡처(1280 × 800 창, `screencapture -l`): [다시 불러온 직후 Console](reload-1280.png) · [컴파일 오류 후 계속 실행](compile-error-1280.png). 실행 출력 [editor-run.txt](editor-run.txt), 기록 [reload.json](reload.json).

첫 실행에서는 KeyRoom의 `OpenChest.Player`(`ComponentReference<PlayerMovement>`)가 "옮길 수 없음" 경고로 초기화됐다. `ComponentReference<T>`를 오브젝트 ID로 옮기도록 고친 뒤 위 결과를 얻었다.

## 단위 검사 (`./monoon test`)

[ScriptReloadChecks](../../Engine.Checks/ScriptReloadChecks.cs)는 창 없이 같은 교체를 검사한다: int·프로젝트 enum 유지, 동작 참조와 `ComponentReference<T>`의 대응, 새 필드 기본값과 삭제 필드, 타입 변경 경고, 루틴 중지, 다음 틱의 새 코드, 컴파일 오류 시 `Counter.cs(12,…) CS1002`와 이전 코드 계속 실행, 게임 스크립트에서 `using Engine.Editor`가 컴파일 오류(CS0234)인지. [출력](engine-checks.txt). 전체 회귀 `./monoon test`: All checks passed.

## Release·Native AOT

에디터 확장 검증과 같은 KeyRoom 사본을 기본(Distribution) 프로필로 빌드해 앱 번들 전체를 검사했다([상세](../editor-extensions/README.md#releasenative-aot)).

| 검사 | Release (280개 파일) | Native AOT (63개 파일) |
| --- | --- | --- |
| `ScriptReload`, `Scripts reloaded`, `Script reload failed` 문자열 | 없음 | 없음 |
| Engine.Authoring·Engine.Editor·Avalonia·SkiaSharp·Microsoft.CodeAnalysis 파일 | 없음 | 없음 |

Engine.Core에 위의 내부 연결점이 추가되었으므로 Release의 `Engine.Core.dll`은 이 변경 전과 바이트 단위로 같지 않다. 다시 불러오기 절차 자체는 배포본에 없다.

## 확인하지 않은 것

- 다시 불러온 코드에 외부 IDE 중단점을 거는 동작(같은 계측을 쓰지만 연결 상태를 검사하지 않았다).
- 이전 컨텍스트가 실제로 메모리에서 해제되는지(Unload만 요청한다. 루틴·이벤트 구독 등이 이전 타입을 잡고 있으면 남는다).
- 스크립트가 다른 객체의 C# 이벤트에 직접 구독해 둔 처리기는 이전 코드로 남는다(매뉴얼에 Stop 후 다시 Play 안내).
- Windows·Linux x64 에디터.

Linux x64 게스트(2026-11-23): 판정 항목이 모두 통과했다. [Linux 결과](../chapter11-linux-x64/README.md)
