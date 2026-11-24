# 에이전트 작업 도구: 워크트리 준비(W1)와 프로브 공용 라이브러리(W3)

2026-11-24. [next-work 5. AI 작업 흐름](../../../../docs/plans/next-work/5-ai-workflow.md)의 W1과 W3을 구현했다. W3는 [아래](#프로브-공용-라이브러리와-모달-가드w3)에 있다. 에디터·게임 런타임 코드는 바꾸지 않았다. 바꾼 것은 빌드 스크립트, `.gitignore`, `.worktreeinclude`, 문서다. 배포본의 용량·시작 시간·프레임 비용은 그대로다(스크립트만 바뀜). BRUTAL에 해당하는 기능이 없는 도구 영역이다.

## 환경

| 항목 | 값 |
| --- | --- |
| OS/아키텍처 | macOS 27.0 (Darwin 27.0.0), Apple Silicon arm64, 14코어 |
| 대상 | 저장소 스크립트, `./monoon test`(Engine.Checks), 에디터 프로브 빌드 |
| 부하 | QEMU 게스트는 돌지 않았다(`pgrep -f qemu-system` 0개). 다른 에이전트의 빌드가 함께 돌아 load average 3–10이었다. |

## 바꾼 것

- [bootstrap-worktree.sh](../../../scripts/bootstrap-worktree.sh)
  - Mac: 메인 체크아웃의 `libSDL3.dylib`, `libBrutalScenePanel.dylib`를 복사한다. 패치·브리지 소스가 메인과 다르면 복사하지 않는다. `cp -p`가 아니라 `cp`로 복사해 체크아웃한 입력 파일보다 새 mtime을 갖게 하므로 MSBuild가 SDL을 다시 빌드하지 않는다. SDL 소스 묶음(`downloads/`)과 CMake(`tools/`)는 링크한다.
  - 템플릿: `HEAD`의 플레이어 소스 트리 해시(Engine.Core·Desktop·Player, `engine/native`, `Directory.Build.*`, 두 빌드 스크립트)로 `<메인>/.monoon-cache/players/<hash>`를 찾고 `player-templates`를 그곳에 링크한다. 없으면 `MONOON_PLAYERS_DIR`로 한 번 빌드한다. 플레이어 소스에 커밋하지 않은 변경이 있으면 캐시를 쓰지 않는다.
- [build-scene-sdl-mac.sh](../../../scripts/build-scene-sdl-mac.sh): 소스 묶음이 없으면 `curl` 대신 안내와 함께 실패한다. 내려받기는 `MONOON_ALLOW_SDL_DOWNLOAD=1`일 때만 한다.
- [build-players.sh](../../../scripts/build-players.sh), [build-native-aot-kit.sh](../../../scripts/build-native-aot-kit.sh): `player-templates`가 캐시 링크이면 링크를 지우고 워크트리 안에 빌드한다. 고친 플레이어 소스로 공유 캐시를 덮어쓰지 않기 위해서다.
- `.gitignore`: `/player-templates/` → `/player-templates`(링크도 무시), `/.monoon-cache/` 추가.
- [.worktreeinclude](../../../../.worktreeinclude): Claude Code가 만드는 워크트리에 두 dylib을 복사한다. `.claude/settings.json`과 hooks는 사용자 결정이 필요해 추가하지 않았다(`worktree.baseRef` 포함).
- 문서: [개발자 가이드의 워크트리 준비](../../../../docs/development.md#워크트리-준비), [빌드 매뉴얼 로컬 회귀](../../../../docs/manual/build.md#로컬-회귀-검사).

## 검증

| 검사 | 결과 |
| --- | --- |
| 소스 묶음 없이 `sh engine/scripts/build-scene-sdl-mac.sh` | 종료 코드 1, 안내 3줄, `curl` 호출 없음, 빈 `downloads/`도 만들지 않음 |
| 네이티브 없이 프로브 빌드(`dotnet build engine/tests/KeyRoom.Rebinding`) | `MacScene.targets(9,5): error MSB3073`, 앞 줄에 같은 안내. 내려받기 없음 |
| bootstrap 후 같은 프로브 빌드(빈 `engine/.artifacts`에서) | 통과 4 s. 빌드 로그에 CMake·SDL 빌드 없음 |
| 캐시가 빈 상태의 bootstrap + `./monoon test` | 템플릿 3종+AOT 키트를 캐시에 한 번 빌드(12 s), `All checks passed.` |
| 캐시가 있는 bootstrap + `./monoon test` | 템플릿 빌드 0회, bootstrap 0 s, `All checks passed.` |

### 새 워크트리에서 `./monoon test`까지 걸린 시간

매번 `engine/.artifacts`와 `player-templates`를 지운 이 워크트리에서 쟀다. 이전 방식은 phase-common의 수동 절차다: `libSDL3.dylib` 복사 → `build-players.sh` → `./monoon test`. 두 방식을 번갈아 실행했다.

| 방식 | 준비 | `./monoon test` | 합계 |
| --- | --- | --- | --- |
| 이전(수동 복사 + 템플릿 빌드) | 11 / 14 / 12 s | 29 / 32 / 33 s | 40 / 46 / 45 s (중앙값 45) |
| bootstrap, 캐시 있음 | 0 / 0 s | 37 / 34 s | 37 / 34 s (중앙값 35.5) |

- 차이는 템플릿 빌드 시간 약 12 s다. 중앙값 차이 9.5 s가 두 범위의 합(6 + 3 s)보다 커서 개선으로 본다. `./monoon test` 자체 시간은 부하에 따라 29–37 s로 흔들렸다.
- 조사 문서가 추정한 "워크트리당 약 10분"은 SDL을 다시 빌드하거나 내려받는 사고의 비용이다. 이번 측정에는 그 사고가 없다. 지금은 그 경로가 내려받기 대신 즉시 실패로 바뀌었다.
- `./monoon test`(Engine.Checks)는 에디터를 참조하지 않아 SDL과 무관하다. SDL은 에디터와 에디터 프로브 빌드에서만 필요하다.

## 남은 점

- `build-scene-sdl-mac.sh`가 MSBuild 입력이라, 이 변경이 합쳐진 뒤 메인 체크아웃의 첫 에디터 빌드는 SDL을 한 번 다시 빌드한다. 메인에는 소스 묶음과 CMake가 있으므로 내려받기 없이 끝난다.
- 메인 체크아웃의 `.gitignore`가 이 변경을 받기 전에는 `.monoon-cache/`가 메인에서 추적되지 않은 폴더로 보인다.
- 같은 해시를 두 워크트리가 동시에 처음 빌드하면 둘 다 빌드하고 늦은 쪽 결과를 버린다.
- `libSDL3.dylib` 복사 조건은 패치 파일만 비교한다. `build-scene-sdl-mac.sh`의 CMake 옵션이 바뀌면 비교 대상에 넣어야 한다.
- `.worktreeinclude` 동작(Claude Code가 새 워크트리에 복사하는지)은 이 작업 안에서 새 Claude 워크트리를 만들어 확인하지 않았다.

# 프로브 공용 라이브러리와 모달 가드(W3)

에디터 소스는 바꾸지 않았다. 조사 문서의 W3(a) `MONOON_QA_MODAL`(에디터가 창 대신 정해진 답을 돌려주는 기능)은 에디터 변경이라 이번 범위에서 뺐다. 대신 프로브 쪽에서 창을 감지해 빨리 실패시킨다. 테스트 코드만 바뀌었으므로 배포본에는 영향이 없다.

## 바꾼 것

- [engine/tests/Shared/Probe.cs](../../Shared/Probe.cs): 에디터 프로브 28개의 `.csproj`가 `<Compile Include="../Shared/Probe.cs"/>`로 링크한다. `global using static Probe;`라서 프로브 안에서 바로 부른다.
  - `Start(errorFile, test, allow:, shutdown:)`: 메인 창을 기다린다(60 s × `MONOON_QA_SLOW`). UI 스레드에서 검사를 실행하고, 예외는 `errorFile`에 쓴다. 끝나면 `life.Shutdown()`을 부른다. 외부 드라이버가 에디터를 닫는 프로브(CameraLab, ObjectGroups, PlayInspector, UiLayout)는 `shutdown: false`를 쓴다.
  - 창 가드: 100 ms마다 메인 창이 아닌 보이는 창을 찾는다. `allow`에 없는 창이 있으면 제목과 문구를 `errorFile`에 덧붙인다. stderr에 `probe: …`를 쓰고 `Environment.Exit(3)`으로 끝낸다. 가드는 Shutdown 중에도 계속 돈다. `"Prefab · *"`처럼 끝의 `*`는 제목 앞부분 일치다.
  - `Until(done, what, seconds = 30, poll = 50)`, `Delay(ms)`, `Slow`: 모두 `MONOON_QA_SLOW` 배율을 따른다.
- 프로브 28개(StartupHook.cs, 에디터 프로브 전부. 플레이어용 AudioLab.Widgets는 제외)를 기계적으로 옮겼다. 검사 내용은 바꾸지 않았다.
  - 각자 복사해 두었던 메인 창 대기, try/catch/Shutdown, `Slow`·`Delay`·`Until`·`Wait`을 지웠다. EditorPerf·EditorBuild의 자체 창 가드도 공용 가드로 바꿨다.
  - 제한 없이 돌던 `while (… ) await Task.Delay(100)` 씬 대기는 `Until`로 바꿔 30 s 제한을 걸었다.
  - 폴링 간격이 프레임이나 표본 수와 묶인 세 곳은 간격을 유지했다: SplitLoading 10 ms, EditorPerf 20 ms, PlayInspector 70 ms(안정 표본 7개). 외부 입력 드라이버를 기다리는 PlayInspector(120 s)와 UiLayout(300 s)은 기존 제한을 유지했다. 나머지 대기의 기본 제한은 60–120 s에서 30 s로 줄었다.
  - 일부러 여는 창은 `allow`에 적었다: Build Game, Prefab Overrides, `Prefab · *`(프리팹 원본 창), UI Layout Preview, Select sheet cells, 떠 있는 패널 창(Console·Animator·Inspector·Scene·Game).
- `editor.py` 25개의 에디터 실행 제한을 600–3600 s에서 측정값 기반으로 낮추고 `MONOON_QA_SLOW` 배율을 붙였다. 에디터 밖의 `monoon build`·`dotnet build` 제한은 그대로 두었다.

| 하네스 | 이전 | 이후 | 근거(이번 측정, 하네스 전체) |
| --- | --- | --- | --- |
| 일반 21개 | 600–1800 s | 240 s | 2–54 s. 가장 긴 EditorDocking은 에디터를 5번 띄워 54 s |
| FrameProfiler, SplitLoading, EditorPerf | 900 s | 300 s | 13 s, 71–74 s(세션 3번), 100 s |
| EditorBuild | 3600 s | 900 s | 90 s(에디터 안 Release·AOT 빌드 포함). AOT 빌드가 196 s 걸렸던 기록이 있어 여유를 크게 뒀다 |

## 검증 (macOS arm64, 패키지 에디터 `editor/MoNoon.app`, Release 빌드 프로브)

| 검사 | 결과 |
| --- | --- |
| 프로브 빌드 28개 | 모두 통과, 새 경고 0(AudioLab 경고 2개는 사라짐, CopyPaste 기존 경고 2개 그대로) |
| 에디터 하네스 27개(editor.py 25개 + GameText 두 샘플 + ObjectGroups verify.py), 옮기기 전 | 27/27 통과, 합계 509 s |
| 같은 하네스, 옮긴 뒤 | 첫 실행 26/27. EditorTheme가 의도한 시트 선택 창(`Select sheet cells`)에서 가드에 걸려 7 s에 실패했다. `allow`에 추가한 뒤 통과. 합계 500 s(실패한 7 s 제외) |
| 결정적 출력 비교(trace·tsv·저장·설정·캡처 PNG 381개) | 357개 같음. 다른 24개는 모두 실제 창 캡처 PNG(Console 시간, 프로파일러 그래프, 창 스크린샷)다. trace·tsv·저장 파일은 모두 같다 |
| `./monoon test` | `All checks passed.` |
| 실행하지 못한 것 | CameraLab.ScreenEffects(verify.py: 게임 빌드·가상 패드 필요), KeyRoom.PlayInspector·UiLayout(손쉬운 사용 권한이 필요한 OS 입력, phase1에서도 환경 실패)는 빌드만 확인했다 |

실행 순서: 하네스별로 프로브를 빌드해 출력 폴더를 따로 복사한 뒤(Rebinding과 ScriptWaits가 같은 `EditorProbe.dll` 이름을 쓴다) GUI 잠금 안에서 차례로 실행했다. QEMU는 돌지 않았다.

### 모달 가드 확인(임시 프로브, 커밋하지 않음)

KeyRoom 복사본을 연 임시 프로브로 두 경우를 확인했다. `t0`는 창을 띄우는 동작 직전 시각이다.

| 경우 | 결과 | 동작 → error.txt | 동작 → 에디터 종료 |
| --- | --- | --- | --- |
| 에디터의 `Choice()` 질문 창 | 종료 코드 3, `unexpected window 'QA modal': Opened on purpose by the guard demo. \| OK` | 0.05 s | 0.10 s |
| 오브젝트 추가 후 메인 창 닫기 | 종료 코드 3, `unexpected window 'Unsaved scene': Save changes to KeyRoom? \| Cancel \| Discard \| Save` | 0.21 s | 0.26 s |

이전에는 두 경우 모두 `editor.py` 제한(900–1800 s)까지 기다렸다. 확인 뒤 임시 프로브는 저장소에서 지웠다.

- `life.Shutdown()`만으로는 저장 확인 창이 뜨지 않았다(첫 시도에서 dirty 상태로 Shutdown해도 종료 코드 0). 확인 창은 창 닫기(`Close()`) 경로에서 뜬다. 그래서 조사 문서의 완료 기준("dirty 상태로 Shutdown")은 창 닫기로 재현했다.
- `check=True`를 쓰는 `editor.py`는 가드 실패를 `returned non-zero exit status 3`으로만 보여 준다. 원인은 결과 폴더의 `error.txt`와 `editor.log`의 `probe:` 줄에 있다.
