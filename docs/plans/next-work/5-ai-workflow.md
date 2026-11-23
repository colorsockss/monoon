# 5. AI 코딩 에이전트로 MoNoon을 더 효율적으로 개발하기

기준: `.worktrees/phase1` (wip/phase1, `4c9b090`). 읽기 전용으로 조사했다. 빌드, 창 열기, 설치는 하지 않았다.
시간 수치는 scratchpad 로그의 생성 시각부터 마지막 쓰기 시각까지 잰 값이다(`stat -f "%B %m"`). 로그를 덮어쓰거나 GUI 잠금을 기다린 시간이 섞일 수 있으므로 **추정치**다.
Claude Code 기능은 2026-11-23에 code.claude.com 문서에서 확인했다.

---

## 1. 현재 상태

### 1.1 에이전트 설정

| 항목 | 상태 | 근거 |
| --- | --- | --- |
| 지시 파일 | `AGENTS.md`(5.5 KB)만 있다. `CLAUDE.md`, `.claude/`(settings, agents, skills, rules, hooks)는 저장소에 없다. 메인 체크아웃의 `.claude/`에는 `worktrees/`만 있다. | `ls -a .claude` |
| 운영 규칙 | 워크트리 준비, GUI 잠금, 빌드 명령, 하네스 패턴, 결과 README 형식이 저장소 밖 `tools/agent/phase-common.md`(조사 당시 scratchpad에 있던 사본)에만 있다. 작업마다 브리프에 다시 붙인다. | phase-common.md 전체 |
| GUI 직렬화 | `tools/agent/gui-lock.sh`(사본)는 `mkdir` 잠금과 `caffeinate`를 쓴다. 오래된 잠금을 정리하는 기능은 없어 "60분 넘은 잠금은 수동 삭제" 규칙에 맡긴다. | gui-lock.sh 1–9행 |
| Claude Code가 AGENTS.md를 읽는 방식 | CLAUDE.md가 없으면 AGENTS.md를 읽는다. CLAUDE.md를 추가하면 **AGENTS.md를 더 이상 읽지 않는다**. 단, CLAUDE.md가 `@AGENTS.md`로 가져오면 함께 읽는다. | [memory#agents-md](https://code.claude.com/docs/en/memory) |

### 1.2 빌드와 회귀 경로

- `./monoon test`는 `Engine.Checks`를 실행한다(`monoon` 22–24행). `Program.cs`(393행)는 80개 넘는 검사를 차례로 부른다.
  - 맨 앞 검사인 `DistributionPackageChecks.Run`은 linux-x64와 win-x64 템플릿이 필요하다(`Program.cs:75`, `DistributionPackageChecks.cs:32`).
  - `--deck-only` 같은 `--X-only` 플래그는 해당 검사 **앞의 검사를 모두 실행한 뒤** 멈춘다(`Program.cs:83–112`). 그래서 새 워크트리에서는 어떤 검사를 고르든 템플릿을 먼저 빌드해야 한다.
- 템플릿 위치는 `MONOON_PLAYERS_DIR`로 바꿀 수 있다(`GameBuild.cs:29`). 템플릿은 워크트리마다 `player-templates/`(메인 체크아웃 276 MB)에 빌드한다(`build-players.sh:4–17`).
- Mac 에디터의 SDL은 MSBuild 타깃이 `build-scene-sdl-mac.sh`를 부른다(`MacScene.targets:6–10`).
  - 입력 파일(패치와 스크립트)이 출력 `libSDL3.dylib`보다 새것이면 다시 빌드한다. 이때 tarball이 없으면 GitHub에서 `curl`로 받는다(`build-scene-sdl-mac.sh:11–12`).
  - 새 워크트리는 checkout 시각이 곧 입력 파일의 mtime이 된다. 그래서 dylib을 `cp -p`로 옛 mtime을 유지해 복사해도 다시 빌드된다(추론).
  - cmake는 `engine/.artifacts/native/tools/cmake`가 있어야 찾는다(같은 파일 20–23행).
- 측정치(추정):

| 작업 | 시간 |
| --- | --- |
| 냉간 에디터 빌드 | 561–731 s (`a36-editor`, `perfDE-cand-editor`, `build-mac`) |
| 증분 에디터 빌드 | 6–30 s |
| `./monoon test` | 25–107 s (`test-wp*`, `perfDE-monoon-test`, `wpcd-monoon-test`) |
| 에디터 하네스 23개 합계 | **324 s**. 개별 2–78 s이고 가장 긴 것은 CollisionLab.SplitLoading 78 s (`harness-after/summary.txt`) |
| KeyRoom Release / AOT 빌드 | 196 s / 154 s (`build-release.log`, `build-aot.log`) |

  → 회귀 자체는 싸고, 시간은 냉간 빌드·기준선 재빌드·GUI 잠금 대기·A/B 반복이 차지한다는 coordinator 관찰과 맞는다.
- 디스크: `.claude/worktrees/`에 워크트리 21개, **30 GB**다. 워크트리 하나가 1.5–1.8 GB이고 대부분 `engine/.artifacts`다(`du`).

### 1.3 하네스 구조

- `engine/tests`에 하네스 폴더 43개가 있다.
  - StartupHook 프로브 28개 중 27개가 `BindingFlags.NonPublic` 리플렉션을 쓴다.
  - 가장 자주 찾는 private 멤버는 `Scene` 23회, `game` 17회, `KeepSimulationActive` 7회다(grep 집계). 예: `KeyRoom.Rebinding/StartupHook.cs:24,31`.
- Python 스크립트 약 50개 중 공용 모듈(`desktop_players.py`)을 쓰는 것은 3개뿐이다. 나머지는 prepare, build, run, timeout을 각자 구현한다.
- `editor.py`의 timeout은 900 s가 18개, 1800 s가 7개다. 실제 실행은 2–78 s다. 모달이 뜨면 15–30분을 기다린다.
  - 모달이 뜨는 곳은 두 군데다. 닫을 때의 "Unsaved scene/prefab" 창(`Shell/MainWindow.axaml.cs:163–172`)과 공용 `Choice()`(`Shell/MainWindow.Workspace.cs:88`, 복구·병합 충돌·씬 삭제 질문)다.
  - 프로브는 `life.Shutdown()`으로 끝낸다(Rebinding:41). 씬이 dirty면 이 창이 막는다.
- 하네스 목록과 인자 형태는 저장소에 없다. coordinator가 `tools/agent/run-harnesses.sh`(사본)에 `probe()`와 `sample()` case 표, 하네스별 인자 분기를 직접 둔다(9–36행).
- 성능 A/B 실행기도 작업마다 새로 만든다(`perfDE-measure.sh`, `wpcd-measure.sh`, `perf-afg-time.py`). 기준선은 `git archive` 복사본을 냉간 빌드한다(`perfDE-base`).
- 창 없는 재생 경로는 이미 있다. `MONOON_REPLAY_OFFSCREEN`과 `MONOON_REPLAY_SNAPSHOTS`는 SDL 소프트웨어 렌더러로 메모리 서피스에 그린다(`OffscreenResources.cs:6–15`, `Engine.Player/Program.cs:109–141`). 그래서 대부분의 `verify.py`(Release/AOT)는 창 없이 돈다.
  - 창이나 접근성이 필요한 것: `Accessibility.Mac`, `CameraLab.ScreenEffects`, `KeyRoom.Desktop`, `KeyRoom.PlayInspector`(mac-input.swift), `InventoryLab.Bundles`(mac_key.m), 에디터 프로브 4개(osascript, screencapture).

### 1.4 충돌 지점 (최근 10일, merge 19개와 커밋 60개)

| 파일 | 변경 횟수 | 원인 |
| --- | --- | --- |
| `engine/tests/Engine.Checks/Program.cs` | **20** | 검사를 추가할 때마다 중앙 목록을 고친다 |
| `engine/src/Engine.Core/SceneGame.cs` | 14 | |
| `RuntimeObjects.cs` | 12 | |
| `docs/manual/build.md` | 11 | |
| `docs/roadmap.md` | 10 | |
| `docs/brutal.md` | 8 | '유지 결정' 표 끝에 행을 덧붙인다(brutal.md:78–92) |
| `Engine.Core.csproj` | 8 | |

### 1.5 x64 비결정성

- `e5571eb`의 원인은 `Vector2.Transform/TransformNormal/Lerp`가 `MultiplyAddEstimate`를 쓰는 데 있었다. 이 함수는 FMA가 있는 곳(arm64, AVX2 JIT)에서만 fused로 계산되고 x64 Native AOT에서는 아니다. QEMU Linux 게스트에서 늦게 발견했다.
- 이 Mac에는 Rosetta가 있다(`arch -x86_64 /usr/bin/true` 성공). NuGet 캐시에는 linux-x64와 win-x64 런타임 팩만 있고 osx-x64 팩은 없다.

---

## 2. 부족한 점 / 문제 (예상 낭비 시간 순)

1. **새 워크트리 준비가 수동이고 쉽게 깨진다.**
   - dylib 복사를 빠뜨리거나 mtime이 어긋나면 승인 안 된 SDL 다운로드가 일어나고 냉간 빌드가 약 10분 걸린다.
   - 템플릿이 없으면 `./monoon test`가 첫 검사에서 실패한다.
   - 세 플랫폼 템플릿을 워크트리마다 다시 빌드한다.
   - 기본 base가 main이라 `reset --hard b524375` 같은 수동 단계가 필요하다.
2. **운영 규칙이 저장소 밖에 있다.** 브리프마다 phase-common.md를 붙이고, Codex 같은 다른 에이전트는 이 규칙을 모른다. CLAUDE.md는 "강제"가 아니다. 문서 표현으로는 차단하려면 PreToolUse hook을 쓰라고 한다([memory](https://code.claude.com/docs/en/memory)).
3. **하네스 목록·인자·프로브 이름이 기계가 읽을 수 있는 형태로 없다.** 변경 경로에 맞는 하네스를 고르지 못해 "전부 실행"과 "감으로 고르기" 중 하나가 된다. coordinator 스크립트가 진짜 목록 역할을 하고 있다.
4. **프로브가 모달에서 멈춘다.** 두 모달에 무인 실행용 동작이 없고, timeout이 실제 실행 시간의 10–30배다.
5. **`Engine.Checks/Program.cs`가 최대 충돌 지점이고 필터가 가짜다.** 빠른 일부 실행이 불가능하고 템플릿 의존이 맨 앞에 있다.
6. **프로브가 private 멤버에 묶여 있다.** 에디터 리팩터링(`perf D E editor` 같은)마다 프로브 27개가 깨질 수 있다.
7. **A/B 기준선과 실행기를 매번 새로 만든다.** 냉간 빌드 약 10분이 성능 작업마다 반복되고, 판정 규칙("중앙값 차이 > 범위 합")도 사람이 계산한다.
8. **x64 비결정성 검사가 QEMU에만 있어 늦게 발견된다.**
9. **위험한 동작을 막는 장치가 없다.** 다운로드 금지, `.test-env` 보호, GUI 명령의 잠금 사용이 모두 브리프 문장에만 있다. 권한 허용 목록도 없어 승인 요청이 반복된다.
10. **디스크와 정리 부담.** 워크트리 21개, 30 GB.

---

## 3. 제안 작업 목록 (절약 시간 ÷ 노력 순)

표기:
- 구분: **[저장소]**는 커밋으로 공유하는 변경, **[사용자]**는 `~/.claude` 또는 `settings.local.json`이나 git 설정, **[coordinator]**는 진행 방식 변경이다.
- 런타임 영향: 에디터·테스트·스크립트만 바꾸는 항목은 Release와 Native AOT 배포본의 용량·시작 시간·프레임 비용에 **0**이다. 금지 파일 검사로 확인한다.
- BRUTAL: 해당 없음(도구·프로세스 영역)은 "—"로 쓴다.

### W1. 워크트리 부트스트랩 — S, [저장소]+[사용자]
- **내용**
  - (a) `.worktreeinclude`에 `engine/.artifacts/native/osx-arm64/*.dylib`, `engine/.artifacts/native/downloads/SDL3-3.4.12.tar.gz`, `engine/.artifacts/native/tools/**`를 넣는다. Claude가 만드는 모든 워크트리(subagent의 `isolation: worktree` 포함)에 gitignore 파일이 복사된다([worktrees#copy-gitignored-files](https://code.claude.com/docs/en/worktrees)).
  - (b) `build-scene-sdl-mac.sh`를 fail-closed로 바꾼다. tarball이 없으면 curl 대신 "메인 체크아웃의 `engine/.artifacts/native`를 복사하라"는 메시지와 함께 종료한다. 다운로드는 `MONOON_ALLOW_SDL_DOWNLOAD=1`일 때만 한다.
  - (c) `engine/scripts/bootstrap-worktree.sh`를 만든다. natives가 없으면 복사하고 dylib mtime을 `touch`한다. 템플릿은 공유 캐시(`<메인>/.monoon-cache/players/<hash>`)를 `MONOON_PLAYERS_DIR`로 가리킨다. hash는 `git rev-parse HEAD:engine/src/Engine.Core`, `…Engine.Desktop`, `…Engine.Player`, `engine/native`를 합친 값이다. 캐시가 없을 때만 `build-players.sh`를 실행한다.
  - (d) 프로젝트 `.claude/settings.json`에 `"worktree": {"baseRef": "head"}`를 넣는다. 통합 브랜치에서 갈라지므로 `reset --hard` 단계가 없어진다([worktrees#choose-the-base-branch](https://code.claude.com/docs/en/worktrees)).
- **완료 기준**: 새 `claude --worktree x`에서 `bootstrap-worktree.sh && ./monoon test`가 네트워크 접근 없이 통과한다. 플레이어 소스가 같은 두 번째 워크트리는 템플릿 빌드를 0회 한다. `build-scene-sdl-mac.sh`가 tarball 없이 curl을 부르지 않는다.
- **예상 절감**: 실수가 있으면 워크트리당 약 10분(SDL 재빌드와 다운로드 사고). 템플릿 3종 재빌드도 피한다(시간은 측정하지 않음). 브리프의 준비 단락도 없어진다.
- **의존성**: 없음.
- **용량·성능**: 런타임 0. 캐시는 템플릿 세트당 약 276 MB다.

### W2. CLAUDE.md와 공용 운영 문서 — S, [저장소]
- **내용**
  - `CLAUDE.md`는 첫 줄을 `@AGENTS.md`로 하고, Claude 전용 내용 몇 줄(사용할 skill·agent 이름)만 둔다. import를 빠뜨리면 AGENTS.md를 읽지 않는다는 점을 주석으로 남긴다.
  - phase-common.md의 도구 중립 규칙은 `docs/manual/agent-workflow.md`로 옮기고 AGENTS.md에서 링크한다. Codex도 같은 규칙을 읽게 하기 위해서다. 옮길 규칙: 워크트리 준비(W1), GUI 잠금, 빌드 명령, 하네스 패턴, 결과 README 형식, 벤치마크 판정 규칙.
  - 경로별 규칙은 `.claude/rules/*.md`의 `paths:`로 나눈다. `engine/src/Engine.Editor/**`에는 UI 정렬과 토큰 규칙, `engine/tests/**`에는 하네스와 결과 README 형식을 둔다. 해당 파일을 다룰 때만 로드된다([memory#path-specific-rules](https://code.claude.com/docs/en/memory)).
  - GUI 잠금 스크립트는 `engine/scripts/gui-lock.sh`로 옮기고, 잠금 경로는 `${TMPDIR}/monoon-gui.lock`처럼 저장소 밖 고정 위치로 바꾼다. 오래된 잠금 정리 기능을 넣는다: 소유 PID가 죽었거나 60분이 지났고 MoNoon 프로세스가 없으면 정리한다.
- **완료 기준**: 새 세션의 `/memory`에 AGENTS.md와 CLAUDE.md가 모두 보인다. 구현 브리프가 "W2 문서 + 작업 범위"만으로 지금과 같은 결과를 낸다(브리프 길이를 비교한다).
- **주의**: 워크트리 subagent는 지시 파일을 **메인 대화 쪽**에서 가져온다([worktrees#isolate-subagents](https://code.claude.com/docs/en/worktrees)). 따라서 CLAUDE.md는 coordinator가 실행되는 체크아웃에 있어야 한다.
- **의존성**: W1(문서에 넣을 명령). **용량·성능**: 0.

### W3. 프로브 공용 라이브러리 + 모달 가드 + 짧은 timeout — S→M, [저장소]
- **내용**
  - (a) 에디터에 `MONOON_QA_MODAL=discard|cancel|fail`을 추가한다(`CanLeave`와 `Choice` 두 곳). 설정되어 있으면 창을 띄우지 않고 제목과 선택을 stderr에 남긴 뒤 정해진 답을 돌려준다. `fail`이면 예외를 던진다.
  - (b) `engine/tests/Probe.Common`(공유 소스 또는 프로젝트)을 만든다. 담을 기능: `WaitMainWindow`, `WaitScene(name)`, `PressPlay`/`StopPlay`, `RunGuarded(output, timeout)`(error.txt 기록 후 Shutdown), 단계별 watchdog(초과 시 열린 창 제목을 덤프한다).
  - (c) `engine/tests/harness.py` 공용 모듈을 만든다. 담을 기능: prepare(샘플 복사), probe 빌드, `MONOON_USER_DATA` 격리, gui-lock 자동 적용, 기본 timeout 180 s(하네스별로 올릴 수 있음).
- **완료 기준**: 일부러 dirty 상태로 Shutdown하는 테스트 프로브가 5 s 안에 "modal: Unsaved scene"으로 실패한다. 기존 23개 에디터 하네스가 W3로 옮긴 뒤에도 통과하고, 합계 시간 324 s ± 범위 안이다.
- **예상 절감**: 모달이 걸릴 때마다 15–30분을 기다리던 시간이 수 초로 준다. 하네스를 새로 만들 때 복사하는 보일러플레이트도 준다.
- **의존성**: 없음. W4, W6과 같이 하면 좋다. **용량·성능**: 에디터 전용 코드라 배포본에 0.

### W4. 하네스 매니페스트 + 단일 실행기 + 변경 기반 선택 — M, [저장소]
- **내용**
  - `engine/tests/harnesses.json`(또는 `.yaml`)을 만든다. 항목마다 다음 필드를 둔다:
    - `name`, `sample`
    - `modes: [unit, scene-play, release, aot]`
    - `probe`(csproj와 dll 이름), `editor_args`(prepare를 쓸지, 프로젝트를 복사할지, 반복 횟수)
    - `needs_gui`, `needs_accessibility`, `timeout`
    - `watch: [glob…]`(영향을 받는 소스 경로)
  - `engine/tests/run.py <name|--changed BASE|--tier fast|full> [--mode …]`를 만든다. 매니페스트대로 prepare → build → (필요하면 gui-lock) → run을 실행하고 `summary.json`(이름, 모드, exit, 초, 첫 예외 줄)을 남긴다.
  - `--changed BASE`는 `git diff --name-only BASE...HEAD`를 `watch`와 맞춰 하네스를 고른다. `engine/src/Engine.Core/**`는 넓게 매칭하고, Editor만 바뀌었으면 scene-play만 고른다. 매칭이 없으면 fast tier로 둔다.
  - `./monoon test --harness …`로 노출한다. 지금 `run-harnesses.sh`가 하는 case 분기는 모두 매니페스트로 옮긴다.
- **완료 기준**: `run.py --tier full`이 coordinator 스크립트와 같은 23개 결과를 낸다. Editor만 고친 diff에서 `--changed`가 Release/AOT 하네스를 고르지 않는다. 매니페스트와 폴더 목록이 어긋나면 `./monoon test`가 실패한다(자기 검사).
- **예상 절감**: 반복마다 "영향받는 것만" 실행해 GUI 잠금 점유가 줄고, coordinator 스크립트를 유지할 필요가 없어진다. 비율은 작업에 따라 다르며 측정하지 않았다.
- **의존성**: W3. **용량·성능**: 0.

### W5. Engine.Checks 등록 방식과 계층 — M, [저장소]
- **내용**
  - 검사 클래스마다 `[Check("rebinding", Tier.Fast, NeedsTemplates=false)]` 특성을 붙이고 리플렉션으로 찾는다. 테스트 프로젝트라 AOT 제약이 없다.
  - `Program.cs`는 실행기만 남기고 검사 목록을 없앤다. 순서 의존이 있는 KeyRoom 스크래치 복사 블록(`Program.cs:150–170`)은 fixture로 분리한다.
  - `--only a,b`를 진짜 필터로 만든다. `--tier fast`는 템플릿이 필요 없는 검사만 실행한다. `--tier full`은 지금과 같다.
- **완료 기준**: 새 워크트리에서 템플릿 없이 `./monoon test --tier fast`가 통과한다. `--tier full` 결과의 PASS 줄 수가 지금(`perfDE-monoon-test.log` 780줄 기준 PASS 목록)과 같다. 검사를 하나 추가할 때 `Program.cs` diff가 0줄이다.
- **예상 절감**: 최대 충돌 파일(10일 동안 20회)이 사라진다. 빠른 반복이 25–107 s에서 일부 검사 수 초로 줄 것으로 추정한다.
- **의존성**: 없음. **용량·성능**: 0.

### W6. 안정된 에디터 자동화 API (리플렉션 제거) — M, [저장소]
- **내용**
  - `Engine.Editor`에 `internal` 자동화 표면을 만든다. 예: `EditorAutomation.Current` → `Scene`, `Game`, `Play()`, `Stop()`, `Select(id)`, `KeepSimulationActive`, `LayoutPreset`. 리플렉션 집계 상위 항목이 그대로 대상이다.
  - 프로브 어셈블리에 `InternalsVisibleTo`를 준다. StartupHook 프로브는 이름이 고정된 어셈블리이므로 가능하다.
  - 프로브 27개를 W3 라이브러리와 이 API로 옮긴다.
- **완료 기준**: `grep -l BindingFlags.NonPublic engine/tests/*/*.cs`가 0개다. 에디터 private 필드 이름을 바꿔도(예: `game` → `session`) 프로브 빌드가 깨지지 않는다. 23개 하네스가 통과한다.
- **예상 절감**: 에디터 구조 작업 후 프로브를 고치는 시간(이번 페이즈의 25개 결합 문제)이 사라진다.
- **의존성**: W3. **용량·성능**: 에디터에만 몇 KB가 늘고 배포본에는 0이다(Engine.Editor는 배포에서 빠진다).

### W7. Claude Code 권한·hook 가드 — S, [저장소] `.claude/settings.json` + `.claude/hooks/`
- **내용**
  - 권한
    - `permissions.deny`: `Edit(.test-env/**)`, `Write(.test-env/**)`, `Bash(rm * .test-env*)`.
    - `permissions.allow`: `Bash(dotnet build *)`, `Bash(./monoon test *)`, `Bash(python3 engine/tests/*)`, `Bash(sh engine/scripts/*)`, `Bash(git status*)`, `Bash(git diff *)`, `Bash(git log *)`.
    - 이 목록은 `/fewer-permission-prompts` skill로 실제 기록에서 다시 뽑을 수 있다([settings](https://code.claude.com/docs/en/settings), [permissions](https://code.claude.com/docs/en/permissions)).
  - PreToolUse(Bash) hook `guard.sh`. 차단은 exit 2와 stderr 사유로 한다([hooks](https://code.claude.com/docs/en/hooks)).
    - ① `curl|wget|brew install|pip install|dotnet tool install|gem install`을 차단한다. `dotnet add package`는 `Ahwoo.BRUTAL.*` 외에는 차단한다.
    - ② `qemu-system`·`.test-env` 실행을 차단한다(사용자 지시가 있을 때만 해제한다).
    - ③ 명령에 `MoNoon.app/Contents/MacOS/MoNoon`, `editor.py`, `KeyRoom.app`, `MoNoonPlayer`가 있는데 `gui-lock.sh`가 없으면 "gui-lock으로 감싸라"는 메시지와 함께 거부한다. 단, `MONOON_REPLAY_OFFSCREEN`이 있는 창 없는 재생은 허용한다.
  - Windows VM 시작 명령은 별도 패턴으로 막아 "Windows 보류" 메모리를 강제한다.
  - hook 경로는 `${CLAUDE_PROJECT_DIR}`가 메인 체크아웃에 고정되므로 워크트리 경로는 입력 JSON의 `cwd`로 읽는다([worktrees 주의](https://code.claude.com/docs/en/worktrees)).
- **완료 기준**: 테스트 세션에서 `curl https://…`, `python3 engine/tests/KeyRoom.Rebinding/editor.py …`(잠금 없이), `rm -rf .test-env/x`가 모두 사유와 함께 거부된다. `./monoon test`와 `dotnet build`는 승인 요청 없이 실행된다.
- **안전 규칙 관계**: AGENTS.md 규칙(설치 승인, `.test-env` 불변, Windows 보류)을 **강제할 뿐 완화하지 않는다**. 승인이 필요한 경우 hook 메시지가 "사용자 승인 후 사용자가 직접 실행하거나 hook을 임시 해제"라고 안내한다.
- **예상 절감**: 승인 요청 왕복과 GUI 충돌 사고(병렬 창 → 캡처 실패 → 재실행)가 준다. **용량·성능**: 0.
- 선택: Bash sandbox(`sandbox.enabled`, `sandbox.network.allowedDomains: ["api.nuget.org", "*.nuget.org"]`)는 네트워크 차단을 OS 수준으로 한다([sandboxing](https://code.claude.com/docs/en/sandboxing)). 다만 NuGet 도메인은 BRUTAL 외 패키지도 통과시키므로 패키지 이름 검사는 hook이 맡는다. 결정 사항 D3.

### W8. 프로젝트 skill과 subagent 정의 — S, [저장소] `.claude/skills/`, `.claude/agents/`
- **skill** (`SKILL.md`, 부작용이 있으면 `disable-model-invocation: true`, [skills](https://code.claude.com/docs/en/skills))
  - `/bootstrap-worktree`: W1 스크립트를 실행하고 `` !`git log -1 --oneline` ``로 base를 확인한다.
  - `/run-harness <name> [scene-play|release|aot]`: W4 실행기를 호출하고 실패하면 첫 예외와 로그 경로를 요약한다.
  - `/regress fast|full`: `--changed $(git merge-base HEAD wip/phase1)`로 fast는 W5 fast tier와 영향받는 하네스, full은 전부 실행한다.
  - `/baseline-update <harness>`: 기준 파일을 다시 만들고 옛→새 표(해시, 크기, 카운트)를 결과 README에 붙인다. 사용자 확인 후에만 커밋한다.
  - `/bench-ab <base-sha> <harness> [n=5]`: W9 실행기를 호출한다.
  - `/results-readme`: 결과 README 틀(OS/arch, 대상, 빌드 종류, 첫 예외, 재현 입력, 로그)을 만든다.
- **subagent** (`.claude/agents/*.md`, [sub-agents](https://code.claude.com/docs/en/sub-agents))
  - `implementer`: `isolation: worktree`, `model: inherit`, `skills: [bootstrap-worktree, run-harness, results-readme]` 사전 로드.
  - `harness-runner`: `model: sonnet`, `effort: low`, `tools: Bash, Read`, `background: true`. 실행하고 요약만 한다.
  - `explorer`: `model: haiku`, 읽기 전용.
  - `reviewer`: 읽기 전용이고 런타임 경계 검사표를 프롬프트에 담는다.
- **완료 기준**: coordinator 브리프가 "agent=implementer, 범위, 감사 노트 경로" 3줄이 된다. `/regress fast` 한 번으로 구현 에이전트의 회귀 단계가 끝난다.
- **의존성**: W1, W4, W5, W9. **용량·성능**: 0.

### W9. 기준선 캐시와 A/B 실행기 — S, [저장소]+[coordinator]
- **내용**
  - `engine/scripts/baseline.sh <sha>`는 `<메인>/.monoon-cache/baselines/<sha>/`에 `git worktree add --detach`로 받아, 에디터·프로브·플레이어를 **한 번만** 빌드한다. 이미 있으면 바로 경로를 돌려준다. coordinator가 통합 커밋마다 미리 만든다.
  - `engine/tests/bench_ab.py`는 base와 cand를 n회 교대로 실행한다. qemu 실행 여부를 시작과 끝에 기록하고, 중앙값·범위와 "중앙값 차이 > 범위 합" 판정을 표로 출력한다. 지금의 perfDE와 wpcd 스크립트를 일반화한 것이다.
- **완료 기준**: 같은 base를 쓰는 성능 작업 두 개 중 두 번째의 기준선 빌드가 0 s다. `bench_ab.py`의 표가 perfDE 결과 README의 수치와 같은 형식이다.
- **예상 절감**: 성능 작업마다 약 10분(냉간 에디터 빌드)과 실행기 작성 시간이 준다. **용량·성능**: 0.

### W10. x64 결정성 조기 검사 — S(정적 검사) / M(Rosetta), [저장소]
- **내용**
  - (a) 지금 할 것, 다운로드 없음. `Engine.Checks`에 소스 스캔 검사를 넣는다. `engine/src/Engine.Core`, `Engine.Desktop`, 패키지 런타임 코드에서 `Vector2.Transform(`, `TransformNormal(`, `Vector2.Lerp(`, `MultiplyAddEstimate`, `FusedMultiplyAdd`, `Vector*.Normalize` 등 FMA·추정 경로 API를 `Affine2D` 밖에서 쓰면 실패한다. 허용 목록은 파일에 둔다. Roslyn BannedApiAnalyzers도 같은 일을 하지만 NuGet 다운로드가 필요하므로 grep 검사로 대신한다.
  - (b) 결정 필요(D4). osx-x64 런타임 팩과 ILCompiler 팩을 받으면, 이 Mac에서 osx-x64 Native AOT 플레이어를 Rosetta로 실행해 InventoryLab 재생 저장 바이트를 arm64 기준과 비교할 수 있다. x64 AOT의 non-FMA 경로를 QEMU 없이 재현하는 것이 목적이다.
    - 재현될 것이라는 점은 추정이고 검증하지 않았다.
    - Release(JIT)는 Rosetta가 알리는 CPU 기능에 따라 FMA 여부가 달라질 수 있다. `DOTNET_EnableFMA=0` 같은 런타임 knob으로 두 경로를 강제해 볼 수 있다.
    - QEMU 게스트 결과를 대체하지는 않는다(AGENTS.md: 게스트 결과 ≠ 물리 장비).
- **완료 기준**: (a) `e5571eb` 이전 커밋에서 검사가 실패하고 이후 커밋에서 통과한다. (b) 같은 입력의 osx-x64 AOT(Rosetta) 저장 바이트가 linux-x64 QEMU AOT 기준(`engine/tests/results/input-replay-x64`)과 같다.
- **예상 절감**: 비결정성을 통합 직전 QEMU(TCG, 느림) 단계가 아니라 구현 단계에서 잡는다.
- **용량·성능**: 0. 배포본 코드 변경 없음.

### W11. 충돌을 줄이는 소유권과 통합 스크립트 — S, [저장소]+[coordinator]+[사용자]
- **내용**
  - (a) `docs/brutal.md`의 '유지 결정' 표를 영역 이름 알파벳순으로 정렬하고, 새 행은 정렬 위치에 넣는 규칙을 W2 문서에 적는다. 끝에 덧붙이던 충돌이 사라진다. 아니면 에이전트는 결정을 결과 README에만 쓰고 coordinator가 한 번에 옮긴다.
  - (b) 작업 패키지 브리프에 "소유 파일/금지 파일" 목록을 넣는다. `Engine.Core.csproj`, `docs/manual/build.md`, `SceneGame.cs`처럼 공유되는 핫스팟은 한 패키지만 소유한다.
  - (c) `engine/scripts/integrate.sh <branch…>`는 순서대로 `git merge --no-ff`를 하고, 각 merge 후 W5 fast tier와 W4 `--changed`를 실행한다. 첫 실패에서 멈추고 표를 출력한다.
  - (d) [사용자] `git config rerere.enabled true`로 반복되는 충돌 해결을 재사용한다.
- **완료 기준**: 다음 병렬 웨이브에서 brutal.md와 Program.cs 충돌이 0건이다(W5 포함). `integrate.sh`가 지금의 수동 merge 기록(19회)과 같은 결과를 낸다.
- **용량·성능**: 0.

### W12. 모델·병렬도 운영 지침 — S, [coordinator]+[사용자]
- **모델**(가격은 Claude API 기준, 입력/출력 $/MTok. 구독 사용자는 한도 소비 비율로 보면 된다)

| 모델 | 가격 | 맡길 일 |
| --- | --- | --- |
| Opus 5.5 | 4/20 | 구현과 비결정성·충돌 같은 원인 추적 |
| Sonnet 5.5 | 2/10 | 하네스 실행·요약, 기준선 갱신, 문서 반영 |
| Haiku 5.5 | 0.10/0.50 | 코드 탐색·grep 요약 |

  subagent의 `model`과 `effort` 필드로 지정한다. 실행 전용 에이전트는 `effort: low`로 둔다.
- **병렬도**
  - GUI 작업은 잠금 하나로 직렬이다. 에디터 하네스 전체가 324 s이므로 GUI 단계가 병목이 되는 것은 대기열 때문이다.
  - 구현 에이전트는 CPU 코어를 기준으로 동시 `dotnet build` 3–4개로 제한한다(측정 안 함, 추정). GUI 실행은 각자 하지 말고 W4 실행기 대기열로 넘긴다.
  - 성능 A/B는 구현 웨이브와 겹치지 않게 뒤로 미룬다. 빌드 부하가 노이즈가 되기 때문이다(phase-common의 교대 규칙을 보완한다).
  - 하나의 에이전트가 나은 경우: 핫스팟 파일(SceneGame.cs, RuntimeObjects.cs)을 공유하는 변경, 또는 기준선을 다시 만드는 BRUTAL 전환. 여러 에이전트가 나은 경우: 서로 다른 샘플·하네스·문서 페이지를 맡을 때.
- **디스크**: [사용자] `cleanupPeriodDays`를 줄이고(worktree sweep), coordinator가 merge 후 `git worktree remove`를 한다. 지금 30 GB를 쓴다.
- **완료 기준**: 다음 웨이브의 에이전트별 모델·effort와 총 대기 시간을 기록해 이번 웨이브와 비교한다.

### W13. (보류 후보) 에디터 Scene Play 창 없이 실행 — L, [저장소]
- Avalonia.Headless는 NuGet 다운로드가 필요하고(승인 필요), Mac Scene은 SDL NSView 경로라 headless로 같은 렌더 경로를 검증하지 못한다.
- 지금은 W4의 `needs_gui`로 GUI 하네스를 최소화하고, Release/AOT 쪽은 이미 있는 `MONOON_REPLAY_OFFSCREEN` 경로로 옮기는 것을 우선한다. 대상: `CameraLab.ScreenEffects`처럼 창 캡처를 쓰는 verify.py를 `MONOON_REPLAY_SNAPSHOTS`로 바꿀 수 있는지 검토한다.

---

## 4. 결정이 필요한 것

| # | 질문 | 권장 |
| --- | --- | --- |
| D1 | 운영 규칙을 어디에 둘까: CLAUDE.md만 / AGENTS.md에 직접 / 공용 문서(`docs/manual/agent-workflow.md`) + AGENTS.md 링크 + CLAUDE.md는 `@AGENTS.md`? | **공용 문서 + 링크**. Codex와 Claude가 같은 규칙을 읽고, AGENTS.md는 정책만 짧게 유지된다. |
| D2 | `.claude/settings.json`(권한 allow/deny, hook, `worktree.baseRef: head`)을 저장소에 커밋할까, 사용자 `settings.local.json`에만 둘까? | **deny와 hook은 커밋**한다(안전 규칙 강제). allow 목록은 커밋하되 사람마다 다른 것은 local에 둔다. allow 규칙은 폴더 신뢰 후에만 적용된다. |
| D3 | Bash sandbox(OS 수준 네트워크·쓰기 제한)를 켤까? | **보류, hook 먼저**. sandbox는 gui-lock의 `caffeinate`, `screencapture`, dotnet 빌드 서버 등과 충돌할 수 있어 먼저 검증해야 한다. 켠다면 `allowedDomains`는 NuGet만 둔다. |
| D4 | osx-x64 런타임·ILCompiler 팩을 받아(BRUTAL 외 다운로드라 승인 필요, 수십 MB 추정) Rosetta로 x64 결정성을 미리 검사할까? | **예, 1회 시험 후 판단**. W10(a) 정적 검사는 승인 없이 바로 한다. (b)가 QEMU 결과와 같은 바이트를 내면 x64 회귀 앞에 넣는다. |
| D5 | C# LSP용 code intelligence plugin 설치(도구 설치라 승인 필요)? | **보류**. W6 이후 리플렉션이 사라지면 grep으로 충분하다. |
| D6 | 공유 캐시(`.monoon-cache/`: 템플릿·기준선)를 메인 체크아웃에 둘까, scratchpad에 둘까? | **메인 체크아웃 + `.gitignore`**. scratchpad는 재부팅 때 지워질 수 있고, 템플릿은 276 MB/세트로 재사용 가치가 크다. |
| D7 | Engine.Checks의 `--X-only` 플래그를 W5에서 없애고 `--only`로 통일할까(기존 README·스크립트가 쓰는 플래그는 호환 별칭으로 남길까)? | **별칭을 한 페이즈 유지한 뒤 제거**한다. |
| D8 | 밤마다 full tier를 Desktop 예약 작업이나 `claude -p --bare`로 돌릴까? | **보류**. 사용자가 GitHub Actions를 없애고 로컬 실행을 택했다(`4513142`). W4·W5 이후 필요하면 Desktop scheduled task로 이 Mac에서만 돌린다([headless](https://code.claude.com/docs/en/headless)). |

---

### 참고한 Claude Code 문서
- https://code.claude.com/docs/en/memory — AGENTS.md 로딩 규칙, `@path` import, `.claude/rules` `paths:`
- https://code.claude.com/docs/en/sub-agents — `model`, `effort`, `tools`, `skills`, `isolation: worktree`, `background`, `memory`
- https://code.claude.com/docs/en/skills — `.claude/skills/<name>/SKILL.md`, `disable-model-invocation`, `context: fork`, `` !`cmd` ``, `$ARGUMENTS`
- https://code.claude.com/docs/en/hooks — PreToolUse exit 2 / `permissionDecision: deny`, matcher, WorktreeCreate
- https://code.claude.com/docs/en/worktrees — `.worktreeinclude`, `worktree.baseRef`, subagent 지시 파일 출처, sweep, hook `cwd`
- https://code.claude.com/docs/en/settings · https://code.claude.com/docs/en/permissions — 파일 위치·우선순위, allow/deny, 신뢰
- https://code.claude.com/docs/en/sandboxing — `sandbox.network.allowedDomains`, `filesystem.denyWrite`
- https://code.claude.com/docs/en/headless — `claude -p`, `--bare`, `--output-format json`, `--permission-mode dontAsk`
