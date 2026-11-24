# 에이전트 작업 흐름

AI 코딩 에이전트(Claude Code, Codex 등)와 사람이 여러 워크트리에서 MoNoon을 함께 고칠 때 따르는 운영 규칙이다. 정책(용량·성능 우선, 런타임 경계, BRUTAL 우선, 배포 렌더링)은 [AGENTS.md](../../AGENTS.md)에 있고 이 문서보다 우선한다. 이 문서는 그 정책을 지키며 일하는 방법이다. 명령은 저장소 루트(워크트리 루트) 기준이다.

## 워크트리 준비

- 작업마다 자기 git 워크트리에서 일한다. 브리프가 기준 커밋을 주면 먼저 `git log -1`로 확인하고, 아직 아무것도 바꾸지 않았는데 `git merge-base --is-ancestor <기준> HEAD`가 실패하면 `git merge --ff-only <기준>`으로 맞춘다.
- 첫 빌드 전에 한 번 실행한다. 네트워크를 쓰지 않는다. [준비하는 것과 캐시 규칙](../development.md#워크트리-준비)

```sh
engine/scripts/bootstrap-worktree.sh
```

- 메인 체크아웃의 패치한 `libSDL3.dylib`와 Scene 브리지를 복사하고, `player-templates/`를 `<메인>/.monoon-cache/players/<hash>`에 연결한다. 플레이어 소스를 고친 워크트리는 `engine/scripts/build-players.sh`로 로컬 템플릿을 만든다.
- `build-scene-sdl-mac.sh`가 "SDL 소스 묶음이 없다"며 멈추면 내려받지 말고 bootstrap을 다시 실행한다. `MONOON_ALLOW_SDL_DOWNLOAD=1`은 사용자 승인이 있을 때만 쓴다.
- Claude Code의 워크트리(`claude --worktree`, `isolation: worktree` 서브에이전트)는 프로젝트 설정 `worktree.baseRef: head`에 따라 지금 세션의 `HEAD`에서 갈라진다.

## 빌드와 테스트 명령 (macOS arm64)

| 할 일 | 명령 |
| --- | --- |
| 엔진 회귀(항상) | `./monoon test` |
| 템플릿 없이 빠르게 | `./monoon test --tier fast` ([계층](#enginechecks-등록과-계층)) |
| 검사 묶음 하나만 | `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only <이름>` ([이름 목록](#enginechecks---only)) |
| 하네스 하나, 바뀐 것만, 계층 | `python3 engine/scripts/run-harness.py <하네스> \| --changed <기준> \| --tier fast\|full` ([실행기](#하네스-실행기)) |
| InventoryLab Release·Native AOT 빌드와 재생 | `./monoon ci` |
| 플레이어 템플릿(플레이어 소스를 고쳤을 때) | `engine/scripts/build-players.sh` |
| Mac 에디터 패키지 | `engine/scripts/build-editor-mac.sh` → `editor/MoNoon.app` |
| 에디터 프로브 | `dotnet build engine/tests/<하네스> -c Release -m:1 -p:UsedAvaloniaProducts=` |

- 한 체크아웃에서 `dotnet build`를 둘 이상 동시에 돌리지 않는다. 다른 워크트리의 빌드와 겹치는 것은 괜찮지만, 동시 빌드는 에이전트 전체에서 3–4개로 제한한다(14코어 Mac 기준 추정).
- 기능은 Scene Play, Release, Native AOT 세 경로에서 확인하고 배포본의 금지 파일 검사(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring 없음)를 함께 본다. 로드맵 행이 다르게 정하면 그것을 따른다.

### Engine.Checks `--only`

`--only <이름>`은 이름에 해당하는 검사 묶음에서 멈춘다. 이전 `--<이름>-only` 플래그는 한 단계 동안 같은 뜻의 별칭으로 남는다. 모르는 이름은 목록과 함께 실패한다. 이름은 아래 [등록](#enginechecks-등록과-계층)에서 나오며 `--list`가 순서, 이름, 템플릿 필요 여부를 보여 준다.

| 종류 | 이름 |
| --- | --- |
| 그 묶음만 실행(템플릿 불필요) | `feature-switches`, `physics`, `nav-free`, `prefab-pool`, `particles`, `lighting`, `particle-budget`, `terrain`, `autotile`, `tile-animation`, `colliders`, `physics-alloc`, `audio-buses`, `camera-framing`, `screen-effects`, `lines`, `world-text`, `ui-grid`, `contacts`, `animator-conditions`, `animator-directions`, `script-debug`, `input-interactions`, `rebinding`, `engine-features-1`, `inspector-edit`, `history` |
| 그 묶음만 실행(템플릿 필요) | `asset-report`(Release와, `--only`로만 도는 Native AOT 보고서), `build-size`(= `./monoon test size`) |
| 앞의 검사를 모두 실행한 뒤 멈춤 | `names`, `features`, `determinism-scan`, `player-yaml`, `packages`, `build-settings`, `profiler`, `rich-text`, `deck`, `quest`, `inventory`, `pool`, `rhythm`, `turn`, `generation`, `cutscene`, `rig`, `effects`, `nav`, `world`, `platformer`, `split-loading` |

세 번째 줄의 이름은 `features`부터 세 플랫폼 템플릿이 필요하다(순서 200의 `FeatureChecks`가 게임을 내보낸다). 새 워크트리에서는 bootstrap 뒤에 실행한다. 목록은 검사를 더할 때마다 늘어나므로 `--list`가 기준이다.

### Engine.Checks 등록과 계층

검사 묶음은 `Program.cs`의 목록이 아니라 각자의 파일에서 특성으로 등록한다. 검사를 하나 더할 때 `Program.cs`는 고치지 않는다.

```csharp
[Check(7000, "rebinding")] static void Registered(CheckRun c) => Run(c.Project, c.Scratch);
[Check(3600, "tile-animation", StopAfter = "world")] static void Registered(CheckRun c) => Run(c.Repository, c.Scratch);
[Check(5800, Templates = true)] static void Registered(CheckRun c) { … }
```

| 값 | 뜻 |
| --- | --- |
| 순서(첫 인자) | 전체 실행에서의 위치. 기존 묶음은 100 간격이다. 앞 묶음의 결과에 기대는 검사는 그 뒤의 빈 번호를 쓴다. 같은 번호는 이름 순서로 정렬된다. 배포 용량 검사(`build-size`, 1,000,000)는 항상 마지막이고, 아니면 시작할 때 실패한다. |
| 이름(나머지 인자) | `--only <이름>`이 그 이름을 단 묶음만 순서대로 실행한다(`physics`처럼 여러 묶음이 같은 이름을 달 수 있다). |
| `StopAfter` | `--only <이름>`이 처음부터 이 묶음까지 실행하고 멈춘다(`names`, `packages`, `world` 등). |
| `Templates` | `player-templates/`로 내보내는 묶음. `--tier fast`가 `SKIP <묶음> (--tier fast: needs player templates)`를 찍고 건너뛴다. |
| `Full = false` | `--only`로만 실행한다(몇 분 걸리는 Native AOT 자산 보고서). |

- `CheckRun`은 저장소·임시 폴더·KeyRoom 경로와 명령줄을 준다. `c.Scene`은 처음 쓸 때 KeyRoom을 임시 폴더에 복사하고 한 번 읽은 씬을 뒤 묶음과 함께 쓴다. 예전 `Program.cs`의 복사 순서(`ProjectCatalogChecks` 앞에서 복사, 뒤에서 읽기)와 같다.
- `./monoon test`(`--tier full`)는 예전과 같은 묶음을 같은 순서로 실행하고 같은 PASS 줄을 찍는다. `./monoon test --tier fast`는 템플릿이 필요한 7개 묶음(`FeatureChecks`, `DistributionPackageChecks`, `BuildSettingsChecks`, `AssetReportChecks`, `SpriteChecks`, `KeyRoomChecks.Exports`, `BuildSizeChecks`)을 빼고 실행해 템플릿 없는 새 워크트리에서도 통과한다(이 Mac에서 약 50초, 전체는 약 6분).
- 검사 이름 목록: `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --list`.

## GUI 잠금

에디터나 게임 창을 여는 명령은 모두 잠금으로 감싼다. 잠금은 여러 에이전트의 창 실행을 하나씩 줄 세우고, 실행 중에는 `caffeinate`로 화면을 깨워 둔다.

```sh
sh engine/scripts/gui-lock.sh python3 engine/tests/KeyRoom.Rebinding/editor.py editor/MoNoon.app/Contents/MacOS/MoNoon …
```

- 잠금 폴더는 `<메인 체크아웃>/.monoon-cache/gui.lock`이다. 모든 워크트리가 같은 잠금을 쓴다. `owner` 파일에 소유 셸 PID, 시작 시각, 명령이 있다.
- 오래된 잠금: 소유 셸이 이미 끝났거나, 60분이 지났고 `MoNoon`·`MoNoonPlayer` 프로세스가 없으면 다음 대기자가 지우고 들어간다. 직접 지우지 않는다.
- 창 없는 재생(`MONOON_REPLAY_OFFSCREEN`, `MONOON_REPLAY_SNAPSHOTS`, 플레이어 `--smoke-test`)과 `./monoon test`·`./monoon ci`는 잠금이 필요 없다. 대부분의 `verify.py` Release·AOT 단계가 여기에 해당한다.
- `Avalonia.Native was not able to start the RenderTimer … -6661`은 화면이 잠들었거나 잠긴 것이다. 코드로 우회하지 말고 보고한다.

## 하네스 패턴

검사 하나는 `engine/tests/<샘플>.<기능>/` 폴더 하나다. 기존 하네스(예: [KeyRoom.Rebinding](../../engine/tests/KeyRoom.Rebinding/))를 복사해 시작한다.

| 파일 | 역할 |
| --- | --- |
| `verify.py` | `prepare`(샘플 복사), Release·Native AOT의 `play`(창 없는 재생), 결과 대조 |
| `editor.py` | 패키지 에디터에 `DOTNET_STARTUP_HOOKS`로 프로브를 넣어 Scene Play를 실행한다. `MONOON_USER_DATA`를 결과 폴더로 격리하고, 제한 시간은 측정값 기반(240–900 s) × `MONOON_QA_SLOW`다. |
| `StartupHook.cs` + `<이름>Probe.csproj` | 에디터 안에서 도는 프로브. csproj가 공용 [Probe.cs](../../engine/tests/Shared/Probe.cs)를 `<Compile Include="../Shared/Probe.cs" Link="Probe.cs"/>`로 링크한다. |
| `README.md` | 무엇을 어떤 입력으로 검사하는지, 실행 명령, 결과 링크 |

프로브는 `Start(errorFile, async life => { … }, allow: […])`로 시작한다. `Start`가 메인 창을 기다리고, 예외를 `error.txt`에 쓰고, 끝나면 에디터를 닫는다. 대기는 `Until(조건, "무엇", 초)`와 `Delay(ms)`를 쓴다(기본 30 s, `MONOON_QA_SLOW` 배율). 프로브가 열지 않은 창(저장 확인, 선택 질문, 오류 상자)이 뜨면 약 0.25초 안에 창 제목과 문구를 `error.txt`에 적고 종료 코드 3으로 끝난다. 일부러 여는 창은 `allow`에 제목을 적는다(끝의 `*`는 앞부분 일치). 외부 드라이버가 에디터를 닫는 프로브는 `shutdown: false`를 쓴다.

## 하네스 실행기

[engine/tests/harnesses.json](../../engine/tests/harnesses.json)이 `engine/tests` 아래 하네스 폴더를 모두 적는다. [run-harness.py](../../engine/scripts/run-harness.py)가 이 목록대로 실행하므로 README를 읽고 명령을 옮겨 적지 않는다.

| 필드 | 뜻 |
| --- | --- |
| `id`, `samples` | 폴더 이름, 쓰는 샘플(`samples/<이름>/**`가 바뀌면 고른다) |
| `modes` | `scene-play`, `release`, `native-aot`, `headless` 중 지원하는 것 |
| `needs` | `templates`(player-templates), `editor`(패키지 에디터), `gui`(창, GUI 잠금), `keyboard`(합성 키 입력 권한), `accessibility`, `window-tool`(`engine/.artifacts/clip-qa/window`) |
| `probe`, `baselines` | 프로브 프로젝트 이름(`$probe_dll`), 대조하는 기준 파일(커밋한 trace·입력 기록·fixture, verify.py 안의 기준 해시) |
| `watch` | 이 하네스에 영향을 주는 엔진 소스 glob(예: `engine/src/Engine.Core/InputRebinding.cs`) |
| `steps` | `kind`(prepare/probe/build/play/compare/editor), `modes`, `gui`, `run`(bash 한 줄). `modes`가 있는 단계는 그 모드를 모두 고를 때만 실행한다(세 경로 비교 단계 등). 창을 여는 단계는 `$lock`으로 시작한다. |
| `manual`, `omitted` | 자동 선택에서 빼는 이유(기준 에디터가 필요한 성능 측정, 손 캡처), 빼 둔 x64 게스트 명령 |

단계는 저장소 루트에서 한 bash 프로세스로 돈다. 변수: `$qa`(하네스 폴더), `$work`(새 결과 폴더), `$app`(패키지 에디터), `$bin`(`engine/.artifacts/bin`), `$probe_dll`, `$lock`(`sh engine/scripts/gui-lock.sh`, `MONOON_GUI_LOCK`으로 바꿀 수 있다).

```sh
python3 engine/scripts/run-harness.py --list                          # 모드(*는 창), needs
python3 engine/scripts/run-harness.py KeyRoom.Rebinding --mode release,native-aot --out "$tmp/h"
python3 engine/scripts/run-harness.py --changed wip/next1 --plan      # 고른 하네스와 모드만 출력
python3 engine/scripts/run-harness.py --tier fast --changed wip/next1 # ./monoon test + 창 없는 모드
python3 engine/scripts/run-harness.py --tier full                     # ./monoon test + 모든 하네스, 모든 모드
python3 engine/scripts/run-harness.py --check                         # 매니페스트 자기 검사
```

그 밖의 옵션: `--dry-run`(단계 스크립트만 쓰고 출력), `--build-editor`(Scene Play 전에 `build-editor-mac.sh`를 한 번 실행), `--skip-checks`(`--tier`에서 `./monoon test` 생략), `--broad`(아래).

- 결과 폴더(`--out`, 없으면 새 임시 폴더)에 하네스마다 `run.sh`, `run.log`와 `summary.json`(하네스, 모드, 종료 코드, 초, 실패한 단계, 첫 오류 줄)이 남는다. 첫 오류 줄은 결과 폴더의 가장 최근 `error.txt` 첫 줄, 없으면 실패한 단계 출력의 마지막 `Error`·`Exception`·`FAIL` 줄이다. 종료 코드 2는 준비물(템플릿, 에디터, 창 도구)이 없다는 뜻이고 이유가 같은 줄에 나온다.
- `--changed <기준>`은 기준과의 merge-base부터 작업 트리까지(커밋하지 않은 파일과 새 파일 포함), `a..b`나 `<커밋>^!`은 그 범위의 바뀐 파일을 본다. 하네스 폴더, 샘플, `watch`에 맞으면 직접 선택이다. 에디터 소스·`engine/tests/Shared`·`editor.py`·`StartupHook.cs`·프로브 csproj만 바뀌었으면 `scene-play`만, `Engine.Player`만 바뀌었으면 Release·Native AOT만 고른다. 문서와 결과 폴더만 바뀐 diff는 하네스를 고르지 않는다(`./monoon test`가 맡는다).
- 매니페스트의 `broad` glob(`engine/src/**`, `engine/native/**`, 빌드 스크립트 등)은 공용 코드라 거의 모든 하네스에 닿는다. `--broad`나 `--tier full`일 때만 더한다.
- `--tier fast`는 `./monoon test`(템플릿이 없으면 `--tier fast`) 뒤에 직접 선택된 하네스의 창 없는 모드만 실행한다. `--tier full`은 직접·broad 선택을 모두, 모든 모드로 실행한다. `--changed` 없이 쓰면 모든 하네스다. `manual` 하네스는 id로 직접 부를 때만 실행한다.
- 하네스 폴더를 더하거나 지우면 매니페스트도 고친다. `./monoon test`의 `HarnessManifestChecks`가 폴더 목록과 어긋나면 실패하고, `--check`는 모드·GUI 잠금·glob·기준 파일·bash 문법까지 본다.

## 결과 README 형식

결과는 `engine/tests/results/<기능>/README.md`에 한국어로 쓴다. 기존 예: [Inspector 편집 기록(I-1)](../../engine/tests/results/inspector-i1/README.md), [워크트리 준비와 프로브 라이브러리](../../engine/tests/results/agent-tooling/README.md).

1. 제목과 첫 단락: 날짜, OS/아키텍처, 실행 대상(Scene 편집, Scene Play, Save & Run, Release, Native AOT)과 빌드 종류, 기준 커밋, 측정 중 `qemu-system` 실행 여부.
2. 배포본 영향: Release·Native AOT의 용량·시작 시간·프레임 비용이 바뀌었는지. BRUTAL에 같은 기능이 있는데 쓰지 않았다면 그 이유.
3. **바꾼 것**: 파일과 내용 표.
4. **검증**: 검사 × 빌드 종류별 통과·실패 표. 실패는 첫 예외, 재현 입력, 로그·캡처 경로를 적는다.
5. **재현**: 그대로 붙여 실행할 수 있는 명령.
6. **확인하지 않은 것**: 실행하지 못한 경로와 이유(예: x64는 coordinator 몫).

게스트 실행 결과를 같은 OS의 에디터·Native AOT·물리 GPU 검증으로 적지 않는다. 사용자 문서는 해당 `docs/manual/*.md`를 같은 문체로 고친다.

## 성능 비교 판정

- 시작과 끝에 `pgrep -f qemu-system`으로 QEMU 게스트가 도는지 보고 결과에 적는다.
- 기준과 후보를 번갈아 같은 횟수(보통 5회) 실행해 부하가 양쪽에 같이 걸리게 한다.
- 중앙값 차이가 두 범위(최대−최소)의 합보다 클 때만 개선·악화로 적는다. 아니면 "차이 없음"이다.
- 해시·개수·저장 바이트 같은 결정성 기준은 부하와 무관하게 정확히 같아야 한다.

## 내려받기와 설치

- 승인된 내려받기: `Ahwoo.BRUTAL.*` 패키지(기존 `Ahwoo.BRUTAL.Sdl`과 같은 버전 계열), crossgen2 팩(Release ReadyToRun), `Avalonia.Controls.ColorPicker`(에디터 전용). [사용자 결정](../plans/next-work/README.md#결정-2026-11-24-사용자-확정)
- 그 밖의 NuGet 패키지·버전 변경, 다른 런타임 팩(osx-x64 등), 글꼴, 도구, 설치 프로그램은 승인되지 않았다. 멈추고 보고한다. 검증 도구 설치는 AGENTS.md의 규칙(이름과 이유를 알리고 명시적 승인)을 따른다.

## x64와 Windows

- Linux x64는 게임 실행 대상과 CI(헤드리스 빌드·리플레이)만이다. Linux 에디터는 지원하지 않는다.
- Windows 검증은 사용자가 Windows 장비 준비를 알릴 때까지 보류한다. 이 Mac에서 win-x64 템플릿·배포본을 빌드하는 것은 괜찮다.
- 구현 에이전트는 VM을 시작하지 않는다. x64 회귀는 coordinator가 웨이브 끝에 한 번에 돌린다. `.test-env/`의 가상 디스크·기반 이미지·키는 바꾸거나 지우거나 저장소에 넣지 않는다.
- 시뮬레이션 코드는 `Vector2.Transform`·`Lerp` 대신 `Affine2D`를 쓴다(FMA 차이로 x64 결과가 달라진다). `./monoon test`의 `determinism-scan`이 런타임 소스에서 금지 API를 찾으면 실패한다. [2D 변환 규칙](transform-2d.md)
- 지난 Linux x64 회귀(13–16장)의 게스트 구성, 실패 처리, Windows 보류 목록은 [next1-x64 기록](../../engine/tests/results/next1-x64/README.md)에 있다. 게스트 빌드 시간을 줄이려고 하네스의 빌드를 샘플별로 묶는 방법은 [제안](../../engine/tests/results/next1-x64/build-groups.md)만 있고 `run-harness.py`에는 아직 없다(하네스마다 빌드한다).

## 병렬 작업, 병합과 소유

- 자기 워크트리 브랜치에만 커밋한다. main과 `wip/*` 통합 브랜치에 커밋하지 않고, push하지 않는다. 병합은 coordinator가 한다.
- 커밋은 단계마다 나눈다. 메시지는 `wip: <작업 묶음> <요약>` 형식이다.
- 여러 묶음이 함께 고치는 핫스팟은 브리프가 정한 한 묶음만 고친다: `Engine.Core/SceneGame.cs`, `RuntimeObjects.cs`, `Engine.Core.csproj`, `docs/manual/build.md`, `docs/brutal.md`. 다른 묶음은 필요한 변경을 결과 README에 적고 coordinator가 옮긴다. 새 검사 묶음은 `Program.cs`가 아니라 자기 파일의 `[Check]`로 등록하고, 새 하네스는 `harnesses.json`에 id 순서 자리에 한 항목을 더한다.
- `docs/roadmap.md`는 coordinator만 고친다.
- 창을 여는 실행은 [GUI 잠금](#gui-잠금)으로만 한다. 잠금 대기가 길면 창 없는 경로(`verify.py play`)부터 끝낸다.
- 마무리: 관련 매뉴얼 갱신 → 커밋 → 보고(바꾼 파일, BRUTAL 사용과 건너뛴 것, 검사 × 빌드 종류 통과표, 실패의 첫 예외, 확인하지 않은 것).

## Claude Code 설정

저장소의 `.claude/`는 위 규칙 일부를 Claude Code에서 강제한다. 개인 허용 규칙은 `.claude/settings.local.json`(커밋하지 않음)에 둔다. Bash sandbox는 켜지 않았다(`caffeinate`, `screencapture`, dotnet 빌드 서버와의 충돌을 먼저 확인해야 한다).

- [CLAUDE.md](../../CLAUDE.md)는 `@AGENTS.md`로 AGENTS.md를 가져온다. CLAUDE.md가 있으면 Claude Code는 AGENTS.md를 따로 읽지 않으므로 이 줄을 지우지 않는다.
- [settings.json](../../.claude/settings.json)의 `deny`: `.test-env/` 아래 편집, `rm *.test-env*`, `curl`, `wget`, `brew install`, `pip install`/`pip3 install`, `dotnet tool install`. `allow`: `./monoon test`, `dotnet build`, bootstrap, 가드 자체 검사.
- PreToolUse 가드 [guard.py](../../.claude/hooks/guard.py)가 Bash·Edit·Write·MultiEdit·NotebookEdit 호출을 보고 종료 코드 2와 이유로 막는다.

| 막는 것 | 예 |
| --- | --- |
| 승인 안 된 내려받기·설치 | `curl`, `wget`, `brew`/`pip`/`npm`/`gem`/`cargo` 설치, `npx`, 원격 `git clone`, `dotnet tool`/`workload install`, BRUTAL·ColorPicker 외 `dotnet add package`, osx-arm64·linux-x64·win-x64 외 `-r`/`RuntimeIdentifier`, `MONOON_ALLOW_SDL_DOWNLOAD=1`, `.csproj`·`.props`·`.targets`에 새 패키지를 넣거나 기존 패키지 버전을 바꾸는 편집 |
| `.test-env` 변경·삭제 | 그 아래를 대상으로 하는 `rm`·`mv`·`cp`(목적지)·`ln`·`touch`·`tee`·`dd of=`·`sed -i`·`find -delete`/`-exec`·`git add/rm/mv`·`qemu-img commit/rebase/resize/amend/snapshot`, 리다이렉트, 파일 도구 편집. `git clean -x`/`-X`(무시된 파일 전체 삭제) |
| 잠금 없는 창 실행 | `editor.py`, `verify.py … editor`, `MoNoon`·`MoNoonPlayer`·`*.app/Contents/MacOS/*` 직접 실행, `open <앱>`, `dotnet run --project …Engine.Editor/Engine.Player`. `gui-lock.sh`로 감싼 명령과 창 없는 재생은 통과 |

- 가드는 명령 글자만 읽는다. `sh -c`, `$( … )`, 여러 줄 명령, `cd`는 따라가지만 스크립트가 스스로 내려받거나 파일을 지우는 것은 막지 못한다. 규칙을 지키는 책임은 그대로 에이전트에 있다.
- 실패할 때 열어 둔다: 입력 JSON을 읽지 못하거나 가드에 예외가 나면 통과시킨다(종료 코드 0). 가드 결함 하나로 모든 세션의 모든 도구 호출이 멈추지 않게 하려는 것이다. 가드 파일이 없을 때도 통과한다.
- 승인을 받은 예외는 사용자가 터미널에서 직접 실행하거나, `.claude/settings.local.json`에 `"disableAllHooks": true`를 잠시 넣었다가 뺀다.
- 가드를 고치면 `python3 .claude/hooks/test_guard.py`로 허용·차단 사례 표를 다시 확인한다. [규칙 전문과 검증 기록](../../engine/tests/results/agent-rules/README.md)

### skill과 서브에이전트

모두 저장소 스크립트를 부르는 얇은 안내다. 스크립트가 바뀌면 skill이 아니라 스크립트를 고친다.

| 이름 | 종류 | 하는 일 |
| --- | --- | --- |
| `/bootstrap-worktree` | skill | `engine/scripts/bootstrap-worktree.sh` 실행, 기준 커밋 확인 |
| `/run-harness <하네스> [scene-play\|release\|native-aot\|headless]` | skill | [실행기](#하네스-실행기)로 매니페스트의 단계를 실행(창 단계는 GUI 잠금), 실패하면 실패 단계·첫 오류·로그 경로 요약 |
| `/regress check <이름>\|fast [기준]\|full [기준]` | skill | `check`: `--only <이름>`. `fast`: `run-harness.py --tier fast --changed <기준>`. `full`: `run-harness.py --tier full --changed <기준>` + `./monoon ci` |
| `/results-readme <기능>` | skill | 위 [결과 README 형식](#결과-readme-형식)의 틀을 `engine/tests/results/<기능>/README.md`에 만든다 |
| `implementer` | 서브에이전트 | 자기 워크트리(`isolation: worktree`)에서 이 문서를 따라 구현하고 커밋·보고한다. 위 skill을 미리 읽는다 |
| `harness-runner` | 서브에이전트 | 하네스를 실행하고 결과표만 돌려준다(Sonnet, 낮은 effort, 파일을 고치지 않음) |
