# 에이전트 운영 규칙·Claude Code 가드·`--only` (W2·W7·W8·결정 16)

2026-11-24 · macOS 27.0.1 arm64 · .NET SDK 10.0.101 · 기준 커밋 `6751695`(wip/next1, W1·W3 포함). [next-work 5. AI 작업 흐름](../../../../docs/plans/next-work/5-ai-workflow.md)의 W2·W7·W8과 [사용자 결정](../../../../docs/plans/next-work/README.md#결정-2026-11-24-사용자-확정) 12·13·16을 구현했다. 측정 중 `qemu-system` 프로세스는 없었다.

문서, 저장소 스크립트, `.claude/` 설정, 테스트 프로그램(Engine.Checks), 에디터 프로브 두 개만 바뀌었다. Engine.Core·Desktop·Player와 에디터는 건드리지 않았으므로 Release·Native AOT 배포본의 용량·시작 시간·프레임 비용은 그대로다. 도구·프로세스 영역이라 BRUTAL에 해당 기능이 없다.

## 바꾼 것

| 단계 | 파일 | 내용 |
| --- | --- | --- |
| W2 | [docs/manual/agent-workflow.md](../../../../docs/manual/agent-workflow.md) | 세션 scratchpad의 `phase-common.md`에만 있던 규칙을 옮겼다: 워크트리 준비, 빌드·테스트 명령, GUI 잠금, 하네스 패턴(공용 `Probe.cs`), 결과 README 형식, 성능 비교 판정, 내려받기 승인 목록, x64·Windows 규칙, 병렬 작업·병합·소유, Claude Code 설정 |
| W2 | [AGENTS.md](../../../../AGENTS.md) | "로컬 테스트"에 위 문서 링크 한 줄. 나머지는 그대로 |
| W2 | [CLAUDE.md](../../../../CLAUDE.md) | 첫 줄 `@AGENTS.md`, 그 아래 Claude 전용 두 줄(skill·서브에이전트 이름, 가드). AGENTS.md 내용은 반복하지 않는다 |
| W2 | [engine/scripts/gui-lock.sh](../../../scripts/gui-lock.sh) | `docs/plans/next-work/tools/agent/`에서 옮겼다. 잠금은 `<메인 체크아웃>/.monoon-cache/gui.lock`(모든 워크트리 공유, git 밖이면 `$TMPDIR`). `caffeinate` 유지. 소유 셸이 끝났거나 60분이 지났고 `MoNoon`·`MoNoonPlayer`가 없으면 다음 대기자가 지운다. 기다릴 때 소유자를 한 번 출력하고, 명령의 종료 코드를 그대로 돌려준다 |
| W2 | [docs/development.md](../../../../docs/development.md), `tools/agent/run-harnesses.sh` | 문서 링크, 새 잠금 경로 |
| W7 | [.claude/settings.json](../../../../.claude/settings.json) | `deny`·공용 `allow`·PreToolUse hook·`worktree.baseRef: "head"`. 아래 [규칙](#커밋한-규칙) |
| W7 | [.claude/hooks/guard.py](../../../../.claude/hooks/guard.py), [test_guard.py](../../../../.claude/hooks/test_guard.py) | 가드와 사례 검사(Python 3.9 표준 라이브러리만) |
| W7 | `.gitignore` | `/.claude/worktrees/`, `/.claude/settings.local.json` |
| W8 | `.claude/skills/{bootstrap-worktree,run-harness,regress,results-readme}/SKILL.md`, `.claude/agents/{implementer,harness-runner}.md` | 저장소 스크립트를 부르는 얇은 안내 |
| 16 | [Engine.Checks/Program.cs](../../Engine.Checks/Program.cs) | `--only <이름>` 하나로 통일. `--<이름>-only` 27개는 같은 이름으로 바뀌는 별칭으로 한 단계 유지. 모르는 이름은 이름 목록과 함께 실패 |
| 프로브 | [CollisionLab.InspectorEdits](../../CollisionLab.InspectorEdits/StartupHook.cs), [PlatformerLab.Effects](../../PlatformerLab.Effects/StartupHook.cs) | W3 뒤에 추가된 두 프로브를 공용 `Probe.cs`로 옮겼다. 아래 [프로브 이전](#프로브-이전) |

### 커밋한 규칙

`.claude/settings.json`:

```json
"allow": ["Bash(./monoon test *)", "Bash(dotnet build *)", "Bash(engine/scripts/bootstrap-worktree.sh)",
          "Bash(sh engine/scripts/bootstrap-worktree.sh)", "Bash(python3 .claude/hooks/test_guard.py)"],
"deny":  ["Edit(**/.test-env/**)", "Bash(rm *.test-env*)", "Bash(curl *)", "Bash(wget *)", "Bash(brew install *)",
          "Bash(pip install *)", "Bash(pip3 install *)", "Bash(dotnet tool install *)"],
"hooks": {"PreToolUse": [{"matcher": "Bash|Edit|Write|MultiEdit|NotebookEdit", "hooks": [{"type": "command",
          "command": "f=\"$CLAUDE_PROJECT_DIR/.claude/hooks/guard.py\"; [ -f \"$f\" ] || exit 0; exec python3 \"$f\"", "timeout": 10}]}]},
"worktree": {"baseRef": "head"}
```

- `allow`는 누구에게나 같은 명령만 넣었다. 사람마다 다른 허용은 `settings.local.json`(커밋 안 함)에 둔다. Bash sandbox는 켜지 않았다(결정 13).
- `Edit(...)` deny는 Edit·Write·NotebookEdit에 모두 적용된다(Claude Code 권한 문서). `**/`로 시작해 워크트리 세션에서도 그 아래 어느 깊이든 막는다. 메인 체크아웃의 `.test-env`를 절대 경로로 고치는 경우는 가드가 막는다.
- `worktree.baseRef: "head"`: 서브에이전트 워크트리가 main 대신 지금 세션의 `HEAD`(통합 브랜치)에서 갈라지게 한다. W1(d)에서 결정 대기로 미뤘던 항목이다.

가드(`guard.py`)가 막는 것: 종료 코드 2와 stderr 이유.

| 분류 | 규칙 |
| --- | --- |
| 내려받기·설치 | `curl`, `wget`, `npx`; `brew install/reinstall/upgrade/fetch/tap/bundle`; `pip`/`pip3` `install/download`, `python -m pip install`; `npm/pnpm/yarn` 설치; `gem/cargo/go/port/apt/mas/pipx/uv/nuget` 설치; `softwareupdate --install`, `xcode-select --install`; URL로 하는 `git clone`; `dotnet tool/workload install/update/restore/repair`, `dotnet new install`; `Ahwoo.BRUTAL.*`·`Avalonia.Controls.ColorPicker`·`Microsoft.NETCore.App.Crossgen2*` 외의 `dotnet add package`; osx-arm64·linux-x64·win-x64 외의 `-r`/`--runtime`/`RuntimeIdentifier`; `MONOON_ALLOW_SDL_DOWNLOAD=1`; `.csproj/.fsproj/.props/.targets` 편집에서 승인 목록 밖 패키지를 새로 넣거나 기존 패키지의 버전을 바꾸는 `PackageReference/PackageVersion/PackageDownload` |
| `.test-env` | 그 아래를 대상으로 하는 `rm rmdir unlink shred truncate mv ln chmod chown chflags touch tee install dd(of=)`, `cp/rsync/ditto/scp`의 목적지, `sed/perl -i`, `find -delete/-exec/-execdir/-ok/-fprint`, `git add/rm/mv`, `qemu-img commit/rebase/resize/amend/snapshot`, 그 아래로 가는 리다이렉트, Edit/Write/MultiEdit/NotebookEdit. 위치와 무관하게 `git clean -x/-X` |
| GUI 잠금 | `editor.py`, `engine/tests/…/*.py editor`, 실행 파일 `MoNoon`·`MoNoonPlayer`·`*.app/Contents/MacOS/*`, `open -a`·`open *.app`, `dotnet run …Engine.Editor/Engine.Player`. 단 `gui-lock.sh`로 감싼 명령(옛 scratchpad 경로 포함)과 `MONOON_REPLAY_OFFSCREEN`·`MONOON_REPLAY_SNAPSHOTS`·`--smoke-test`·`--migrate-project`는 통과 |

명령은 `;` `&&` `||` `|` `&` 줄바꿈과 `$( … )`·`( … )`로 나눠 각각 본다. 앞의 `VAR=값`, `env`, `sudo`·`nohup`·`time`·`timeout`·`xargs` 같은 래퍼를 벗기고, `sh/bash/zsh -c '…'`은 안의 문자열을 다시 검사하고, `cd`를 따라 상대 경로를 푼다.

실패 시 열어 두는 곳과 이유:
- 입력 JSON을 읽지 못하거나 가드 안에서 예외가 나면 종료 코드 0(통과). 가드 결함 하나가 모든 세션의 모든 Bash·편집을 멈추게 하지 않기 위해서다.
- 가드 파일이 없으면 통과(`[ -f … ] || exit 0`). 이 줄이 없으면 `python3`가 "파일 없음"으로 종료 코드 2를 내 모든 호출이 막힌다.
- 따옴표가 맞지 않는 명령은 공백으로만 나눠 계속 검사한다(통과시키지 않는다).
- 명령 글자만 보므로 스크립트가 스스로 내려받거나 지우는 것(예: Python `urllib`)은 막지 못한다. heredoc 본문의 줄도 명령으로 읽으므로 `curl`로 시작하는 줄을 파일에 쓰면 막힐 수 있다.

## 검증

### 가드 사례 (`python3 .claude/hooks/test_guard.py`, 61/61 일치)

각 사례는 Claude Code가 hook에 주는 형태의 JSON(`tool_name`, `tool_input`, `cwd`)을 `guard.py` 표준 입력으로 넣고 종료 코드를 본다. 설정 파일의 hook 명령 문자열을 `sh -c`로 그대로 실행해도 같았다(차단 2, 허용 0, 가드 파일이 없는 경로 0). 한 번 실행에 중앙값 20.3 ms(19.4–23.0, 20회).

| 기대 | 결과 | 경우 | 사유(앞 90자) |
| --- | --- | --- | --- |
| 차단 | 차단 | curl download | curl: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.ColorPicker |
| 차단 | 차단 | wget download | wget: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.ColorPicker |
| 차단 | 차단 | brew install | brew install: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.Col |
| 차단 | 차단 | pip3 install | pip3 install: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.Col |
| 차단 | 차단 | python3 -m pip install | pip install: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.Colo |
| 차단 | 차단 | npx | npx(패키지 내려받기): 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.Co |
| 차단 | 차단 | dotnet add package (other) | dotnet add package Newtonsoft.Json: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, |
| 차단 | 차단 | dotnet tool install | dotnet tool install: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Contr |
| 차단 | 차단 | osx-x64 runtime pack | osx-x64 런타임 팩(dotnet publish -r osx-x64): 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossg |
| 차단 | 차단 | RuntimeIdentifier property | linux-arm64 런타임 팩(dotnet build -r linux-arm64): 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지,  |
| 차단 | 차단 | SDL source download | MONOON_ALLOW_SDL_DOWNLOAD=1은 SDL 소스 묶음을 GitHub에서 내려받는다. engine/scripts/bootstrap-worktree. |
| 차단 | 차단 | git clone remote | 원격 저장소 git clone: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls |
| 차단 | 차단 | curl inside bash -c | curl: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.ColorPicker |
| 차단 | 차단 | curl on a later line | curl: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.ColorPicker |
| 차단 | 차단 | curl in $(...) | curl: 승인되지 않은 내려받기다. 승인된 것은 Ahwoo.BRUTAL.* 패키지, crossgen2 팩, Avalonia.Controls.ColorPicker |
| 차단 | 차단 | rm under .test-env | .test-env 아래를 바꾸거나 지우는 명령(rm .test-env/x)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 저장소에 추가하 |
| 차단 | 차단 | cd .test-env then rm | .test-env 아래를 바꾸거나 지우는 명령(rm disk.qcow2)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 저장소에 추가하지 |
| 차단 | 차단 | redirect into .test-env | .test-env 아래에 쓰는 리다이렉트(/Users/someone/repo/.test-env/notes.txt)는 막혀 있다. x64 게스트의 디스크·이미지·키 |
| 차단 | 차단 | mv out of .test-env | .test-env 아래를 바꾸거나 지우는 명령(mv .test-env/base.qcow2)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 |
| 차단 | 차단 | cp into .test-env | .test-env 아래를 바꾸거나 지우는 명령(cp .test-env/ssh/)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 저장소에  |
| 차단 | 차단 | sed -i in .test-env | .test-env 아래를 바꾸거나 지우는 명령(sed .test-env/config)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 저장 |
| 차단 | 차단 | find -delete in .test-env | .test-env 아래를 바꾸거나 지우는 명령(find .test-env)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는 삭제하거나 저장소에 추가하 |
| 차단 | 차단 | git add .test-env | .test-env 아래를 바꾸거나 지우는 명령(git .test-env/ssh/id_ed25519)은 막혀 있다. x64 게스트의 가상 디스크·기반 이미지·키는  |
| 차단 | 차단 | git clean -fdx | git clean -x/-X는 무시된 파일을 지운다. 메인 체크아웃에서는 .test-env, 템플릿 캐시와 빌드 결과까지 사라진다. 필요하면 사용자에게 승인을 받 |
| 차단 | 차단 | qemu-img commit | .test-env 아래를 바꾸거나 지우는 명령(qemu-img .test-env/linux/overlay.qcow2)은 막혀 있다. x64 게스트의 가상 디스크· |
| 차단 | 차단 | Edit under .test-env | Edit로 .test-env 아래 파일(/Users/someone/repo/.test-env/linux/run.sh)을 바꿀 수 없다. x64 게스트 환경은 수정 |
| 차단 | 차단 | Write under .test-env (relative) | Write로 .test-env 아래 파일(.test-env/x.txt)을 바꿀 수 없다. x64 게스트 환경은 수정하지 않는다(AGENTS.md). |
| 차단 | 차단 | csproj adds other package | engine/src/Engine.Authoring/Engine.Authoring.csproj에 패키지 Newtonsoft.Json 13.0.3를 추가하거나 버전을 |
| 차단 | 차단 | csproj bumps SkiaSharp | engine/src/Engine.Authoring/Engine.Authoring.csproj에 패키지 SkiaSharp 3.116.1를 추가하거나 버전을 바꾸면  |
| 차단 | 차단 | editor.py without lock | engine/tests/KeyRoom.Rebinding/editor.py (에디터 하네스): 창을 여는 명령이다. sh engine/scripts/gui-lock |
| 차단 | 차단 | verify.py editor without lock | engine/tests/PlatformerLab.Effects/verify.py (에디터 하네스): 창을 여는 명령이다. sh engine/scripts/gui- |
| 차단 | 차단 | editor binary without lock | editor/MoNoon.app/Contents/MacOS/MoNoon: 창을 여는 명령이다. sh engine/scripts/gui-lock.sh <명령...> |
| 차단 | 차단 | game player without lock | game-builds/KeyRoom/KeyRoom.app/Contents/MacOS/MoNoonPlayer: 창을 여는 명령이다. sh engine/scripts |
| 차단 | 차단 | open .app | open <앱>: 창을 여는 명령이다. sh engine/scripts/gui-lock.sh <명령...>으로 감싸 실행한다(GUI 실행을 에이전트 사이에서 하나 |
| 차단 | 차단 | dotnet run editor | dotnet run (에디터·플레이어): 창을 여는 명령이다. sh engine/scripts/gui-lock.sh <명령...>으로 감싸 실행한다(GUI 실행을 |
| 허용 | 허용 | ./monoon test |  |
| 허용 | 허용 | ./monoon test piped |  |
| 허용 | 허용 | Engine.Checks --only |  |
| 허용 | 허용 | probe build |  |
| 허용 | 허용 | editor.py under gui-lock |  |
| 허용 | 허용 | editor binary under gui-lock |  |
| 허용 | 허용 | old scratchpad gui-lock path |  |
| 허용 | 허용 | offscreen player replay |  |
| 허용 | 허용 | player --smoke-test |  |
| 허용 | 허용 | verify.py play (headless) |  |
| 허용 | 허용 | dotnet add BRUTAL package |  |
| 허용 | 허용 | dotnet add ColorPicker |  |
| 허용 | 허용 | ReadyToRun linux-x64 (crossgen2) |  |
| 허용 | 허용 | copy out of .test-env |  |
| 허용 | 허용 | list .test-env |  |
| 허용 | 허용 | guest runbook writes results |  |
| 허용 | 허용 | git status && diff |  |
| 허용 | 허용 | grep for curl |  |
| 허용 | 허용 | curl as an argument |  |
| 허용 | 허용 | git clean without -x |  |
| 허용 | 허용 | csproj adds BRUTAL package |  |
| 허용 | 허용 | csproj adds ColorPicker |  |
| 허용 | 허용 | csproj keeps existing version |  |
| 허용 | 허용 | ordinary C# edit |  |
| 허용 | 허용 | Read tool |  |
| 허용 | 허용 | unreadable input (fails open) |  |


### GUI 잠금 (`engine/scripts/gui-lock.sh`)

| 경우 | 결과 |
| --- | --- |
| 명령 `sh -c '…; exit 7'` | 종료 코드 7을 그대로 돌려줌, 끝난 뒤 잠금 폴더 없음 |
| 앞 실행(`sleep 6`)이 잡고 있을 때 두 번째 실행 | "waiting for <소유자>"를 한 번 출력하고 6 s 뒤 시작 |
| 소유 PID가 없는 잠금(`999999`) | 즉시 "removing stale lock" 후 실행 |
| 소유 셸은 살아 있으나 60분 넘은 잠금, `MoNoon`·`MoNoonPlayer` 없음 | 즉시 정리 후 실행 |
| 60분 넘은 잠금 + `MoNoon` 실행 중 | **확인하지 않음**. 다른 에이전트의 에디터를 건드릴 수 있어 `MoNoon` 이름의 가짜 프로세스를 만들고 끄는 시험을 하지 않았다 |

### `--only` (Engine.Checks Release, 이 워크트리)

| 인자 | 종료 코드 | PASS 줄 | 시간 |
| --- | --- | --- | --- |
| `--only names` / `--names-only` | 0 / 0 | 3 / 3 | 0 s |
| `--only rebinding` / `--rebinding-only` | 0 / 0 | 26 / 26 | 1 s |
| `--only deck` / `--deck-only` | 0 / 0 | 75 / 75 | 4–5 s |
| `--only bogus` / `--bogus-only` / `--only`(이름 없음) | 134 | 0 | `ArgumentException: Unknown check group '…'. Use --only <name> with one of: …` |
| 인자 없음(`./monoon test`) | 0 | 730 | 36 s, `All checks passed.` |

### 프로브 이전

| 하네스 | 바꾼 것 | 바꾸지 않은 것 |
| --- | --- | --- |
| CollisionLab.InspectorEdits | 자체 메인 창 대기·try/catch/Shutdown·창 가드(250 ms, 제목만 기록)를 `Start`로, 자체 `Until`을 공용 `Until`로 | 검사 단계와 `Require` 전부, 씬 대기 제한 120 s·폴링 20 ms(인자로 유지), 측정 코드, `editor.py` |
| PlatformerLab.Effects | 메인 창 대기·try/catch/Shutdown을 `Start`로, 제한 없던 "Torch flame" 대기를 `Until`(30 s)로. 공용 창 가드가 새로 붙었다(예상한 창 없음) | 재생·캡처 코드, `verify.py` |

같은 패키지 에디터(이 워크트리에서 빌드)로 옮기기 전 프로브와 옮긴 뒤 프로브를 차례로 실행했다. 모든 실행은 기존 scratchpad 잠금과 새 `engine/scripts/gui-lock.sh`를 겹쳐 감쌌다.

| 검사 | 옮기기 전 | 옮긴 뒤 |
| --- | --- | --- |
| PlatformerLab.Effects `verify.py editor`(Scene Play) | 종료 0, 4 s, 캡처 9장 | 종료 0, 4 s, 캡처 9장 |
| Effects `verify.py compare` 두 실행 | 9장 모두 바이트 동일. [P.0 기록](../particles-p0/captures.txt)의 해시(`f39a8da6…`–`4e370a71…`)와도 같다 | |
| CollisionLab.InspectorEdits `editor.py`(엄격 검사, 1회씩 번갈아) | 모든 `Require` 통과 | 모든 `Require` 통과 |
| 저장한 Field10000 SHA-256 | `99189634ed49…37f4` | 같음. [I-1 기록](../inspector-i1/README.md)의 해시와도 같다 |
| 결정적 값: 확정당 캡처, 기록 스냅샷 문자열·크기, 키 입력당 할당 | 1, 101개·1065.2 MB, 2.25 MB(숫자)·2.24 MB(텍스트) | 같음 |
| InspectorEdits 전체 실행 | 54 s(두 에디터 세션), `PASS inspector edit probe` | |

시간 값(키 입력 3–9 ms 등)은 1회씩이라 비교하지 않았다. `./monoon test`가 같은 때 일부 겹쳐 돌았다. 측정용 비교는 [I-1 기록](../inspector-i1/README.md)의 5회 교대 방식을 쓴다. Effects 프로브에 새로 붙은 창 가드는 이번 실행에서 걸리지 않았다(예상한 창이 없다).

## 재현

```sh
sh engine/scripts/bootstrap-worktree.sh && sh engine/scripts/build-editor-mac.sh
python3 .claude/hooks/test_guard.py
dotnet build engine/tests/Engine.Checks -c Release
dotnet engine/.artifacts/bin/Engine.Checks/release/Engine.Checks.dll "$PWD" --only deck
./monoon test
dotnet build engine/tests/PlatformerLab.Effects -c Release -m:1 -p:UsedAvaloniaProducts=
python3 engine/tests/PlatformerLab.Effects/verify.py prepare samples/PlatformerLab "$W/PlatformerLab"
sh engine/scripts/gui-lock.sh python3 engine/tests/PlatformerLab.Effects/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon \
  "$W/PlatformerLab" engine/.artifacts/bin/EffectsProbe/release/EffectsProbe.dll "$W/scene-play"
python3 engine/tests/PlatformerLab.Effects/verify.py compare <이전 scene-play> "$W/scene-play"
dotnet build engine/tests/CollisionLab.InspectorEdits -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 engine/tests/CollisionLab.InspectorEdits/editor.py samples/CollisionLab "$W/inspector" 1 \
  pre=editor/MoNoon.app/Contents/MacOS/MoNoon,<이전 프로브 dll> post=editor/MoNoon.app/Contents/MacOS/MoNoon,engine/.artifacts/bin/InspectorEditsProbe/release/InspectorEditsProbe.dll
```

## 확인하지 않은 것

- 실제 Claude Code 세션에서의 동작: 새 세션의 `/memory`에 CLAUDE.md와 AGENTS.md가 함께 보이는지, deny·hook·allow가 적용되는지, skill·서브에이전트가 목록에 뜨는지. 설정은 세션을 시작한 메인 체크아웃에서 읽으므로 이 브랜치가 메인 체크아웃에 합쳐진 뒤에 확인할 수 있다. `@AGENTS.md` 형식은 [Claude Code memory 문서](https://code.claude.com/docs/en/memory)("Share one file with other coding tools")와 맞췄다.
- 연구 문서 W7 완료 기준의 "`./monoon test`와 `dotnet build`가 승인 요청 없이 실행"(위 이유와 같음). 가드 판단은 위 사례 표로만 확인했다.
- GUI 잠금 전환기: 진행 중인 다른 에이전트는 아직 scratchpad의 `gui-lock.sh`를 쓴다. 두 잠금은 서로를 모르므로 모든 에이전트가 새 경로로 옮길 때까지 겹쳐 감싸야 한다.
- W4(하네스 목록)·W5(검사 등록·계층)가 없어 `/run-harness`는 README를 읽어 실행하고, `/regress full`의 "바뀐 경로의 하네스"는 README 대조로 고른다.
- x64·Windows: 해당 없음(Mac 도구와 문서). VM을 시작하지 않았다.
