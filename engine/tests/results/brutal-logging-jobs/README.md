# BRUTAL 로그 전환과 작업 스케줄러 검토

2026-11-23 · macOS 27.0 · Apple Silicon arm64 · .NET SDK 10.0. 기준 커밋 `2edfef9`(BRUTAL Sdl 2026.10.3)과 이 변경을 같은 Mac에서 차례로 빌드해 비교했다. 측정 내내 QEMU Debian x64 게스트가 실행 중이었다. 시간 비교는 기준과 후보를 번갈아 실행했다. 내려받은 패키지는 `Ahwoo.BRUTAL.Concurrency` 2026.10.2와 유지 결정 확인용 `Ahwoo.BRUTAL.*` 2026.10.x(Localization, Monitor.Common, Monitor.Host, ZLib, NativeFileDialog, Sdl.Image)뿐이다. 엔진 참조에는 아무것도 추가하지 않았다.

## 결정

| 항목 | 결정 | 근거 |
| --- | --- | --- |
| 플레이어 로그 | `Brutal.Logging` 채택 | [GameLog](../../../src/Engine.Desktop/GameLog.cs)가 스크립트 로그(`SessionState.Logged`), 씬 로드 실패(`LoadFailed`), 콘텐츠 업데이트 경고를 `LogCategory "MoNoon"`으로 보낸다. 플레이어가 세션을 만든 직후 연결한다. |
| CLI 출력 | 콘솔 유지 | `PASS player smoke`, 사용법, `Headless replay completed`, `Replay resources at …`, `Recorded …`, 모드 목록 출력은 명령의 결과물이라 그대로 둔다. |
| 크래시·최종 예외 출력 | 콘솔 유지 | `Crash report: …`와 최상위 `catch`의 예외 메시지는 로그 시스템 초기화가 실패해도 남아야 한다. InventoryLab.Crashes 검사기가 이 줄을 읽는다. |
| 오디오 오류(`DesktopHost`의 `TakeError`)·글꼴 추적(`MONOON_FONT_TRACE`) | 이번에는 유지 | 오디오·텍스트 전환 작업이 같은 줄을 바꾸고 있어 병합 충돌을 피했다. 전환이 끝난 뒤 `GameLog.Error`로 옮기면 된다. |
| `FrameProfileWriter` 경고, `SessionState.Log` 대체 출력 | 유지 | Engine.Core는 Box2D 외에 BRUTAL 로그 의존성이 없다. 구독자가 없는 세션(에디터 밖 도구, 테스트)에서는 기존 콘솔 대체 출력이 그대로 쓰인다. |
| 에디터 `--run-scene`(Save & Run) | 유지 | 텍스트 전환 작업이 바로 옆 줄(`GameTextRasterizer` 주입)을 지운다. 지금은 구독자가 없어 `SessionState.Log` 대체 출력으로 같은 줄이 나온다. |
| 백그라운드 로딩 | `Brutal.Concurrency` 미채택 | 아래 [작업 스케줄러 검토](#작업-스케줄러-검토) 참고. `SceneResources.Prefetch`와 `SceneStreaming`은 바꾸지 않았다. |
| 유지 결정 문서 | 작성 | [BRUTAL 기능 정리의 유지 결정](../../../../docs/brutal.md#유지-결정) |

### 바이트 동일성과 비용

- `LogSystem`에 MoNoon 전용 공급자를 등록한다. Microsoft.Extensions.Logging의 콘솔 공급자는 수준·카테고리 접두사를 붙이고 백그라운드 스레드에서 쓰므로 쓰지 않았다. 공급자는 메시지만 `Console.Out`(Info·Warning) 또는 `Console.Error`(Error)에 `WriteLine`한다. 이전 대체 출력과 같은 스트림, 같은 줄, 같은 순서다.
- `Brutal.Sdl`의 모든 P/Invoke 래퍼는 `Sdl` 카테고리로 Trace 로그를 남긴다. 로그 시스템을 켜면 SDL 호출마다 이 검사가 실행된다. 공급자에 필터만 두면 100만 회 호출에 약 21 ms(꺼진 경우 약 6 ms)가 들었다. GameLog는 `Sdl`·`SdlMixer`·`SdlTtf` 카테고리를 끄므로 비용이 로그 시스템을 켜기 전과 같다(100만 회 6.7 ms 대 6.5 ms, 할당 0). 다른 BRUTAL 카테고리는 공급자가 `NullLogger`로 버려 이전처럼 출력되지 않는다.
- 메시지 문자열은 스크립트가 이미 만든 것을 그대로 넘긴다. 카테고리가 꺼져 있으면 `LogString`은 버퍼를 만들지 않는다(1,000회 호출 할당 0바이트, JIT·Native AOT 모두).
- 초기화 비용: 첫 메시지까지 JIT에서 약 19–20 ms, Native AOT에서 0.6–0.9 ms였다. 게임 중 첫 로그에서 프레임이 끊기지 않도록 `GameLog.Attach`가 스레드 풀에서 초기화를 시작한다.
- [Engine.Checks](../../Engine.Checks/DiagnosticsChecks.cs)가 다음을 확인한다: Info·Warning은 표준 출력, Error와 `GameLog.Error`는 표준 오류, `{중괄호}`·한글 외 문자·연속 공백이 바뀌지 않음, `Sdl` 카테고리 꺼짐.

## 크기

`./monoon build … --profile distribution`, osx-arm64. 기준은 `2edfef9`, 후보는 이 변경이다. 바이트 단위.

| 산출물 | 기준 | 후보 | 차이 |
| --- | ---: | ---: | ---: |
| Release 템플릿 `player-templates/osx-arm64` | 98,837,895 | 98,842,143 | +4,248 |
| KeyRoom Release `.app` | 97,458,448 | 97,462,032 | +3,584 |
| KeyRoom Native AOT `.app` | 17,911,766 | 18,443,110 | +531,344 (+3.0%) |
| KeyRoom Native AOT 실행 파일 | 11,978,384 | 12,509,728 | +531,344 (+4.4%) |
| CollisionLab Release `.app` | 101,290,398 | 101,293,982 | +3,584 |
| CollisionLab Native AOT `.app` | 21,509,156 | 22,040,500 | +531,344 (+2.5%) |
| CollisionLab Native AOT 실행 파일 | 11,729,488 | 12,260,832 | +531,344 (+4.5%) |

Release는 `Brutal.Logging`과 Microsoft.Extensions.Logging·DependencyInjection·Options·Configuration·ObjectPool DLL을 이미 `Ahwoo.BRUTAL.Sdl`의 전이 의존성으로 배포하고 있었다. 늘어난 것은 GameLog 코드뿐이다. Native AOT는 지금까지 이 코드를 잘라 냈지만 `LogSystem.Initialize`가 `LoggerFactory`와 DI 컨테이너를 만들기 때문에 약 519 KiB가 더해진다. `LogSystem`에는 다른 초기화 경로가 없어 줄일 수 없었다. 이 크기가 부담되면 GameLog만 콘솔 직접 출력으로 되돌리면 된다(호출부는 그대로).

## 실행 시간

KeyRoom `collect` 입력(`Coin collected.`를 출력)을 헤드리스로 재생한 프로세스 전체 시간, 9회씩 기준·후보 교대.

| 빌드 | 기준 중앙값(범위) | 후보 중앙값(범위) |
| --- | --- | --- |
| Release | 257.1 ms (254.5–334.6) | 259.5 ms (258.2–265.7) |
| Native AOT | 23.5 ms (22.2–56.5) | 23.6 ms (22.7–24.6) |

차이가 범위 안이므로 차이가 있다고 판단하지 않는다.

## 작업 스케줄러 검토

`Ahwoo.BRUTAL.Concurrency` 2026.10.2(공개 버전은 2026.10.1, 2026.10.2)를 내려받아 공개 API를 확인하고 JIT·Native AOT 실행 파일로 시험했다. 공개 형식은 `IJob { void Run(); }`, `JobRunner`, `JobScheduler`, `JobRunnerMetrics`, `JobState`, `ExecutionResult`, 통계용 `MovingAverage<T>`·`Sampler<T>`다.

| 현재 코드가 필요한 것 | 현재 구현 | JobScheduler |
| --- | --- | --- |
| 시작 안 한 디코딩 건너뛰기 | `Parallel.ForEach` + `CancellationToken` | 가능. `AbortJobs(false)`는 시작 전 작업만 건너뛴다(시험에서 4개 중 2개만 실행) |
| 취소 뒤 진행 중 디코딩이 끝나면 버퍼 해제, 게임 스레드는 기다리지 않음 | `work.ContinueWith(…)` | 스케줄러 단위 완료 알림이 없다. `IsExecuting`을 확인하거나 바쁜 대기 `Wait()`만 있다. `JobRunner.ExecuteJobs(onComplete)`에만 러너별 콜백이 있다 |
| 씬 읽기 결과와 예외를 게임 스레드로 전달 | `Task<T>.Result`, `Task.Exception` | `Run()`은 반환값이 없다. 작업이 던진 예외는 잡히지 않고 스레드 풀에서 프로세스를 끝낸다(JIT·AOT 모두 `Unhandled exception`, 종료 코드 134) |
| 이전 로딩이 끝나기 전 새 로딩 시작(취소 후 재시도) | 로딩마다 새 `Task` | 실행 중 `Queue`는 `InvalidOperationException`. 로딩마다 스케줄러와 러너를 새로 만들어야 한다 |
| 분배 | `Parallel.ForEach` 동적 분할 | 큐에 넣을 때 비용 추정값으로 정적 분배 |
| Native AOT | 동작 | 동작(경고 없음) |

완료 알림, 결과·예외 전달, 겹치는 로딩을 모두 MoNoon 쪽 코드로 채워야 해서 바꾸면 코드와 할당이 늘고 이득이 없다. 지시한 조건(필요한 방식의 취소와 완료 지원)을 만족하지 못해 기존 코드를 유지했다. 배포본에도 넣지 않았다(Release·Native AOT에 `Brutal.Concurrency.dll` 없음). 나눠 읽기 결과는 아래처럼 그대로 통과했다.

## 회귀 검사

| 검사 | 빌드 | 결과 |
| --- | --- | --- |
| `./monoon test` | Engine.Checks (Release 템플릿 3종 포함) | 통과(`All checks passed.`, PASS 718줄, 새 GameLog 검사 2개 포함) |
| KeyRoom 플레이어 로그 바이트 비교(`spawn`, `collect` 입력, stdout·stderr 따로) | Release | 기준과 동일 |
| 같은 비교 | Native AOT | 기준과 동일 |
| [CollisionLab.Spatial](../../CollisionLab.Spatial/verify.py) (스크립트 로그 `SPATIAL …` 파싱 + `PASS player smoke`) | Release | 통과. 시간 필드를 지운 로그가 기준과 동일 |
| 같은 검사 | Native AOT | 통과. 시간 필드를 지운 로그가 기준과 동일 |
| [나눠 읽기](../../CollisionLab.SplitLoading/README.md) 헤드리스 3회 | 개발용 Release | 통과 |
| 같은 검사 | 개발용 Native AOT | 통과 |
| 나눠 읽기 Scene Play 3회 | 패키징한 에디터 | 통과. 취소 후 상주 3·디코딩 0·텍스처 3, 최장 프레임 중앙값 예산 0 107.4 ms, 4 ms 64.2 ms |
| [KeyRoom 월드 저장](../../KeyRoom.WorldCheckpoints/README.md) (금지 파일 검사 포함) | Release·Native AOT | 통과. 획득 슬롯 612바이트 `0becae59…`, Mac Scene Play 왕복 `da0e3772…`가 기준과 같음 |
| 금지 파일(SkiaSharp, Avalonia, Engine.Authoring, Engine.Editor, Roslyn) | KeyRoom·CollisionLab 배포 Release·Native AOT | 없음. `MoNoonPlayer.deps.json`에도 없음 |

나눠 읽기 중앙값(3회, 작업 시간 ms). QEMU 게스트가 같이 돌았다.

| 빌드 | 작업 | 예산 | 구간 프레임 | 최장 | p95 | 전체 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Release | 추가 | 0 | 15 | 83.807 | 81.897 | 540.206 |
| Release | 추가 | 4 ms | 20 | 23.629 | 17.494 | 341.729 |
| Release | 교체 | 0 | 15 | 85.758 | 83.114 | 537.771 |
| Release | 교체 | 4 ms | 20 | 18.125 | 17.850 | 336.493 |
| Release | 추가 후 취소 | 0 | 12 | 82.265 | 79.380 | 350.939 |
| Release | 추가 후 취소 | 4 ms | 16 | 19.225 | 19.116 | 271.673 |
| Native AOT | 추가 | 0 | 13 | 84.066 | 81.365 | 485.506 |
| Native AOT | 추가 | 4 ms | 16 | 13.356 | 11.715 | 266.667 |
| Native AOT | 교체 | 0 | 13 | 82.983 | 81.622 | 486.759 |
| Native AOT | 교체 | 4 ms | 16 | 12.686 | 5.705 | 266.667 |
| Native AOT | 추가 후 취소 | 0 | 10 | 83.505 | 83.505 | 320.070 |
| Native AOT | 추가 후 취소 | 4 ms | 14 | 12.558 | 5.739 | 233.333 |

취소한 여섯 번의 재생 모두 끝 자원(상주 파일 3, 디코딩 버퍼 0, SDL 텍스처 3)이 시작과 같았고 방은 연결되지 않았다. 로딩 코드는 바꾸지 않았으므로 이 숫자는 비교 기준이 아니라 통과 확인용이다.

## 재현

```sh
# 크기·로그 비교 (기준은 2edfef9 체크아웃에서 같은 명령)
engine/scripts/build-players.sh osx-arm64
./monoon build samples/KeyRoom release --profile distribution --output "$out/KeyRoom"
./monoon build samples/KeyRoom native-aot --profile distribution --output "$out/KeyRoom"
fx=engine/tests/KeyRoom.WorldCheckpoints/fixtures
MONOON_USER_DATA="$profile" SDL_VIDEO_DRIVER=monoon-no-video SDL_AUDIO_DRIVER=monoon-no-audio \
  "$out/KeyRoom/KeyRoom.app/Contents/MacOS/MoNoonPlayer" --replay-input "$fx/collect.inputreplay" "$profile"
python3 engine/tests/CollisionLab.Spatial/verify.py --player <CollisionLab MoNoonPlayer>
```

`collect` 재생 전에는 프로필에 `fixtures/pending.save`를 `slot-001.save`로 복사한다.

## 확인하지 않은 것

- Windows x64·Linux x64 게스트 실행(Windows는 보류, x64는 조정자가 나중에 실행). win-x64·linux-x64 Release 템플릿은 이 Mac에서 빌드만 했다.
- 독립 플레이어 창 실행(`DesktopHost`)에서 `LoadFailed`가 표준 오류로 나가는 경로는 Engine.Checks의 단위 검사로만 확인했다. 실제 창 실행으로 씬 로드 실패를 만들어 보지 않았다.
- Native AOT에서 Error 수준 스크립트 로그의 표준 오류 출력은 같은 코드 경로지만 따로 실행하지 않았다.
