# 스크립트 대기와 순차 실행 (KeyRoom 문 안내)

macOS 27.0.1 (26A434) arm64, 이 Apple Silicon Mac에서 실행했다. 대상은 에디터 안의 Play(Scene Play, 패키지 `editor/MoNoon.app`), Distribution 프로필의 Release 앱, Native AOT 앱이다. 세 실행 모두 첫 예외는 없었다.

## 동작

KeyRoom의 [DoorLock](../../../../samples/KeyRoom/Scripts/DoorLock.cs)은 열쇠 이벤트로 문을 연 뒤 `StartRoutine`으로 `ShowHint`를 시작한다. `ShowHint`는 `ScriptWait.Seconds(0.5)` 뒤에 `doorHint` 플래그를 켜고, `showIf: doorHint`인 **Door hint** UI가 화면 위쪽 가운데에 **The door is open. Head east.**를 표시한다. 대기는 SceneGame의 고정 틱(게임 시간)에서만 진행하므로 정지 중에는 멈추고 Step(에디터 도구 모음, 독립 게임은 StepGame 액션 `.`)마다 한 틱 진행한다.

방식은 반복자(`IEnumerable<ScriptWait>`) 루틴이다. 스레드 풀·`async`를 쓰지 않고 게임 루프가 시작 순서대로 재개하므로 Native AOT에서도 리플렉션·동적 코드 없이 같은 순서로 실행된다. BRUTAL에서 가장 가까운 기능은 `Brutal.Concurrency`의 작업 시스템이지만 워커 스레드에서 병렬 작업을 실행하는 용도라, 게임 틱과 같은 스레드에서 정해진 순서로 스크립트를 이어 실행하는 이 기능에는 쓰지 않았다([BRUTAL 기능 목록](../../../../docs/brutal.md)). 대기는 MoNoon의 게임 시간(`GameTime`) 위에 구현했다.

## 재생 입력과 결과

입력: 120프레임, ←(5–22) → P(28) → .(40, 46, 52) → P(70). [입력 기록](wait.inputreplay) · [실행 방법과 검증 스크립트](../../KeyRoom.ScriptWaits/README.md)

| 실행 | 열쇠 (프레임 / 틱) | 정지 구간 틱 | Step으로 진행한 틱 | 안내 (프레임 / 틱) | 열쇠→안내 틱 | 기록 |
| --- | --- | --- | ---: | --- | ---: | --- |
| Scene Play | 19 / 19 | 28 → 31 | 3 | 89 / 49 | 30 | [trace](scene-play.trace) |
| Release | 19 / 19 | 28 → 31 | 3 | 89 / 49 | 30 | [trace](release.trace) · [player log](release-player.log) |
| Native AOT | 19 / 19 | 28 → 31 | 3 | 89 / 49 | 30 | [trace](native-aot.trace) · [player log](native-aot-player.log) |

세 trace 파일은 바이트 단위로 같다. 정지하지 않았다면 안내는 프레임 49에 나타났을 것이다. 프레임 28–69의 정지 동안 Step 세 번만 게임 시간으로 계산되어 안내가 40프레임 늦은 89에 나타났고, 틱으로는 문이 열린 뒤 정확히 30틱이다.

## 자동 검사

`./monoon test`의 [ScriptRoutineChecks](../../Engine.Checks/ScriptRoutineChecks.cs)는 다음을 확인한다. 모두 통과했다.

- 시작하면 첫 대기까지 바로 실행, `Frames(3)`은 세 번째 틱, `Until`은 조건이 참이 된 다음 틱에 재개
- 정지 중 게임 대기는 멈추고 `UiSeconds`는 계속 흐름, Step마다 한 틱, `Seconds(0.5)`는 정지 프레임을 빼고 정확히 30틱
- 같은 틱에 재개하는 루틴은 시작 순서를 유지
- 소유 오브젝트 비활성화·제거, 씬 종료 시 취소(`finally` 실행), 다시 켜도 재개하지 않음, 꺼진 오브젝트로 시작하면 오류
- 자기 소유 오브젝트를 끄는 루틴은 그 단계 뒤에 멈춤, 예외는 `ScriptRoutineChecks.Throws: boom`처럼 메서드 이름과 함께 보고

KeyRoom 프로젝트 테스트 [DoorTests](../../../../samples/KeyRoom/Tests/DoorTests.cs)도 같은 규칙(30틱, 정지·Step, 문 제거 시 취소)을 확인한다. [프로젝트 테스트 결과](../project-tests/README.md)

## 배포본 검사

같은 KeyRoom으로 만든 Release·Native AOT 앱에 SkiaSharp, Avalonia, Engine.Editor, Engine.Authoring, Roslyn(`Microsoft.CodeAnalysis`) 파일이 없고, `Tests/`의 소스·`.inputreplay`·테스트 클래스 이름도 없다. 루틴 코드(`ShowHint`)는 Release의 `MoNoon.GameScripts.dll`과 Native AOT 실행 파일에 포함된다.

## 확인하지 않은 것

- 안내 UI의 실제 화면 캡처(창 렌더링)는 남기지 않았다. 표시 여부는 `showIf` 플래그로 확인했다.
- Windows·Linux x64 게스트 실행은 하지 않았다.

Linux x64 게스트(2026-11-23): Scene Play·Release·Native AOT trace가 Mac 기록과 바이트 단위로 같다. [Linux 결과](../chapter11-linux-x64/README.md)
