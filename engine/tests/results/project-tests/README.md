# 게임 스크립트 자동 테스트 (`./monoon test project`)

macOS 27.0.1 (26A434) arm64, 이 Apple Silicon Mac에서 저장소 루트 기준으로 실행했다. 실행 대상은 창 없는 헤드리스 세션(소스 프로젝트, JIT)이다. 테스트는 게임 빌드에 들어가지 않으므로 Release·Native AOT에서는 테스트가 빠졌는지만 확인했다.

## 구조

- 테스트 위치: 프로젝트 루트의 `Tests/`. `ProjectScripts.ForTests`가 `Scripts/`의 게임 스크립트와 `Tests/**/*.cs`를 한 어셈블리로 컴파일한다(테스트 API `GameTestRun`·`[GameTest]`는 빌드 도구인 Engine.Authoring에 있다). 일반 컴파일(Scene Play, Release, Native AOT)은 루트의 `Tests/` 폴더를 건너뛴다.
- 실행: 테스트마다 빈 임시 `MONOON_USER_DATA`로 `GameSession`을 열고 `GameLoop`를 1/60초 프레임으로 진행한다. 입력은 `.inputreplay` 재생과 같은 `ReplayInput.Dispatch` → `GameInputRouter` 경로로 넣고, `run.Replay(...)`는 기록 파일 자체를 재생한다. 테스트 본문은 11.1의 `ScriptWait`로 기다린다. 배경 스레드의 씬 읽기는 프레임을 진행하기 전에 끝날 때까지 기다리므로 씬 전환도 벽시계 시간과 무관하다.
- 실패: 실패한 프레임 번호(1부터)와 게임 틱, 첫 예외, 프로젝트 파일의 줄 위치, 마지막 게임 로그 5줄을 출력하고 종료 코드 1을 반환한다.
- BRUTAL에는 게임 테스트 실행기가 없어([BRUTAL 기능 목록](../../../../docs/brutal.md)) 기존 입력 재생과 Roslyn 스크립트 컴파일을 재사용했다.

## KeyRoom 상자·문 규칙

`Engine.Checks`에 있던 상자 규칙(코인 하나만 생성, 두 번째 E 무시, 걸어서 획득)과 문 규칙(닫힌 문이 x 440에서 막음, 열쇠 획득 시 문 카메라, 1초 뒤 플레이어 카메라 복귀, 두 번째 방 진입)을 [ChestTests.cs](../../../../samples/KeyRoom/Tests/ChestTests.cs)·[DoorTests.cs](../../../../samples/KeyRoom/Tests/DoorTests.cs)로 옮기고, 도달 거리·문 안내 대기(30틱, 정지·Step, 문 제거 시 취소)·기록 입력 재생 테스트를 더했다. `Engine.Checks`의 중복 단언은 지웠고, `./monoon test`가 같은 KeyRoom 테스트를 실행한다.

```sh
./monoon test project samples/KeyRoom
```

결과: 9개 통과, 종료 코드 0. [전체 출력](keyroom.txt)

## 일부러 실패시킨 테스트

KeyRoom 사본의 `Tests/DeliberateFailure.cs`에 안내가 0.4초 뒤에 보인다고 잘못 단언하는 테스트를 넣었다. 저장소의 KeyRoom에는 이 파일이 없다.

```text
FAIL DeliberateFailure.HintAfterFourTenths (Scenes/KeyRoom.scene) at frame 38, tick 38
  GameTestFailure: the hint is visible 0.4 s after the door opens
    at DeliberateFailure.HintAfterFourTenths(GameTestRun run)+MoveNext() in ./Tests/DeliberateFailure.cs:line 14
  log: Key acquired.
  log: Door received KeyAcquired.
9 passed, 1 failed.
```

종료 코드 1. 열쇠를 얻은 틱 14에서 24틱 뒤인 38틱에 단언이 실패했다. [전체 출력](failing-assertion.txt)

게임 스크립트의 예외도 같은 형식으로 보고된다. 다른 사본에서 Key의 **Frames** 목록을 비우고 열쇠를 줍게 하면 `DoorLock`이 던진 첫 예외와 줄 위치가 표시된다.

```text
FAIL DeliberateFailure.KeyWithoutOpenSprite (Scenes/KeyRoom.scene) at frame 14, tick 14
  InvalidOperationException: KeySequence.OnTriggerEnter: DoorLock.Unlock: DoorLock needs an open sprite, Sprite Renderer and collider.
    at DoorLock.Open(IGameObjects objects, Guid door, KeySequence key) in DoorLock.cs:line 40
    at DoorLock.Unlock(GameEventContext message) in DoorLock.cs:line 14
```

[전체 출력](failing-script.txt)

## 빌드 출력에서 테스트 제외

저장소의 `samples/KeyRoom`을 Distribution 프로필로 Release와 Native AOT 빌드한 뒤 앱 번들 전체를 검사했다.

| 검사 | Release (264개 파일) | Native AOT (47개 파일) |
| --- | --- | --- |
| `Tests/` 폴더, `.cs`, `.inputreplay` | 없음 | 없음 |
| `DoorTests`·`ChestTests`·`GameTestRun`·`GameTestAttribute` 문자열 | 없음 | 없음 |
| SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·Microsoft.CodeAnalysis 파일 | 없음 | 없음 |
| 게임 데이터 폴더 `Game/` | Assets, GameSettings, Scenes, `.monoon-compiled` | Assets, GameSettings, Scenes |

`samples/KeyRoom/KeyRoom.csproj`도 `Tests/**/*.cs`를 컴파일 항목에서 뺐다.

## 확인하지 않은 것

- 외부 IDE용 `.monoon/ide/` 스크립트 프로젝트에는 `Tests/`가 포함되지 않는다(테스트 파일 편집 시 자동 완성 없음).
- Windows·Linux x64에서 `./monoon test project`를 실행하지 않았다.

Linux x64 게스트(2026-11-23): 패키지 CLI의 `test project`로 같은 9개가 통과했다. [Linux 결과](../chapter11-linux-x64/README.md)
