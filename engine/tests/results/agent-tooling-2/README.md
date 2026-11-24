# 에이전트 작업 도구 2: 하네스 매니페스트·실행기(W4)와 Engine.Checks 등록·계층(W5)

2026-11-24. [next-work 5. AI 작업 흐름](../../../../docs/plans/next-work/5-ai-workflow.md)의 W4와 W5를 구현했다(결정 #16·D7: `--only`는 이미 통일, 옛 `--<이름>-only` 별칭은 한 페이즈 유지). 기준 커밋 wip/next1 `c648586`. 실행 대상은 이 Mac의 `./monoon test`(Engine.Checks), Release·Native AOT 헤드리스 재생, 패키지 에디터(`editor/MoNoon.app`, Release)의 Scene Play다. 측정 시작과 끝에 `qemu-system` 프로세스는 없었다. 다른 에이전트의 빌드가 함께 돌아 load average가 10–19였다(시간 비교는 하지 않았다).

## 환경

| 항목 | 값 |
| --- | --- |
| OS/아키텍처 | macOS 27.0.1 (26A434), Apple Silicon arm64 |
| .NET SDK | 10.0.101 |
| 대상 | Engine.Checks 전체·빠른 계층·`--only`, 하네스 실행기로 Release·Native AOT(`--profile distribution`)·Scene Play·헤드리스 |

## 배포본 영향

없다. 엔진 런타임·에디터 코드는 바꾸지 않았다. 바뀐 것은 테스트 프로젝트(Engine.Checks), 테스트 스크립트, `monoon` 셸 스크립트의 `test --tier` 분기, skill, 문서다. BRUTAL에 해당 기능이 없는 저장소 도구 영역이다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [CheckRegistry.cs](../../Engine.Checks/CheckRegistry.cs) | `[Check(순서, 이름…, StopAfter, Templates, Full)]` 특성, 리플렉션으로 찾고 정렬하는 `CheckStep`, 실행 문맥 `CheckRun`(경로, 명령줄, 처음 쓸 때 만드는 KeyRoom 임시 복사본과 공유 `Scene`) |
| Engine.Checks의 검사 파일 99개 | 각 클래스에 한 줄 `[Check(…)] static void Registered(CheckRun c) => Run(…);`. 순서 번호는 예전 체인 순서 × 100 |
| [KeyRoomChecks.cs](../../Engine.Checks/KeyRoomChecks.cs) | `Program.cs`에 있던 인라인 검사(프로젝트 테스트, 심볼릭 링크, KeyRoom 씬 편집 블록, 세 플랫폼 내보내기)와 `WalkGame`·`CaptureCanvas`를 옮김. 본문은 그대로다. |
| [Program.cs](../../Engine.Checks/Program.cs) | 471줄 → 97줄. 특수 모드(`--inventory-builds` 등)와 실행기만 남김. `--tier fast\|full`, `--list` 추가, build-size가 마지막이 아니면 시작 시 실패 |
| [HarnessManifestChecks.cs](../../Engine.Checks/HarnessManifestChecks.cs) | `harnesses.json`의 id와 `engine/tests` 하네스 폴더가 어긋나면 `./monoon test` 실패(새 PASS 한 줄). 등록으로 더했고 `Program.cs` diff는 0줄이다. |
| [monoon](../../../../monoon) | `./monoon test --tier fast\|full` |
| [harnesses.json](../../harnesses.json) | 하네스 69개: id, samples, modes, needs, probe, baselines, watch, steps(kind·modes·gui·run), manual/omitted. 각 README의 명령을 옮겨 적고, README에 명령이 없는 6개(CollisionLab.Spatial, Input.Gamepad, InventoryLab.Crashes, KeyRoom.PlayFields, PlatformerLab.Effects·Lighting)는 결과 README·docstring·phase1 스크립트에서 옮겼다. 저장소 안(`game-builds/`, `samples/dist`)이나 `/tmp`에 쓰던 출력은 `$work` 아래로 바꿨다(AccessLab만 verify.py가 `game-builds/AccessLab` 고정 경로를 읽어 그대로 둠). |
| [run-harness.py](../../../scripts/run-harness.py) | 실행기: id 실행, `--changed`, `--tier fast\|full`, `--mode`, `--broad`, `--plan`, `--dry-run`, `--build-editor`, `--list`, `--check`. 결과 폴더에 `run.sh`·`run.log`·`summary.json` |
| [run-harness skill](../../../../.claude/skills/run-harness/SKILL.md), [regress skill](../../../../.claude/skills/regress/SKILL.md) | README를 읽는 대신 실행기를 부른다 |
| [agent-workflow.md](../../../../docs/manual/agent-workflow.md) | [Engine.Checks 등록과 계층](../../../../docs/manual/agent-workflow.md#enginechecks-등록과-계층), [하네스 실행기](../../../../docs/manual/agent-workflow.md#하네스-실행기), 명령 표, 핫스팟 목록에서 `Program.cs` 제거, skill 표 |

### 결정

- **등록 위치**: 한 파일의 표가 아니라 각 검사 파일의 특성이다. 표 한 파일은 끝에 덧붙이는 병합마다 다시 충돌한다. 순서는 100 간격 번호로 정하고, 같은 번호는 이름 순서라 두 에이전트가 같은 번호를 골라도 텍스트 충돌 없이 결정적으로 돈다.
- **`--only` 뜻 유지**: 이름을 단 묶음만 실행하는 이름(예: `physics`, `particles`)과 처음부터 그 묶음까지 실행하는 이름(`StopAfter`, 예: `world`, `packages`)을 그대로 뒀다. 이름 50개가 예전 목록과 정확히 같다. `--only a,b` 같은 다중 필터는 넣지 않았다(브리프 범위 밖).
- **KeyRoom 스크래치 블록**: fixture를 따로 만들지 않고 `CheckRun.CopyKeyRoom()`·`c.Scene`으로 처음 쓰는 곳(예전과 같은 `ProjectCatalogChecks` 자리)에서 복사·로드한다. 앞 단계가 만든 임시 폴더 상태와 순서가 예전과 같다.
- **`--changed` 범위**: 기준 하나를 주면 merge-base부터 작업 트리까지(커밋 안 한 파일·새 파일 포함), `a..b`·`<커밋>^!`은 그 범위다. Core·Desktop·Authoring 등 공용 소스는 `watch`에 맞는 하네스만 직접 고르고, 나머지 모든 하네스는 `broad`(`--broad`·`--tier full`에서만)로 둔다. 연구 문서 W4의 "Core는 넓게"를 그대로 하면 Core를 건드리는 거의 모든 커밋이 69개 전부를 골라 빠른 계층이 의미가 없어서다.

## 검증

### W5 Engine.Checks

| 검사 | 기준 `c648586` | 이 브랜치 | 판정 |
| --- | --- | --- | --- |
| `./monoon test`(전체) | 종료 0, PASS 948줄, 359 s | 종료 0, PASS 948줄(매니페스트 검사 추가 전), 362 s | 통과 |
| PASS 줄 순서·문구(측정값 숫자 제외) | — | 948줄 모두 같은 순서·같은 문구. 숫자만 다른 9줄은 시간·할당 바이트·업데이트 횟수·실행마다 다른 파티클 재생 해시(같은 실행 안 두 번 비교) | 통과 |
| `--only` 이름 집합 | 50개 | 50개, `diff` 없음 | 통과 |
| `player-templates` 링크를 치운 뒤 `./monoon test --tier fast` | — | 종료 0, 53 s, PASS 875줄, `SKIP` 7줄, `All fast-tier checks passed (7 template groups skipped).` 875줄은 모두 전체 실행 PASS 줄의 부분집합(새 매니페스트 줄 제외) | 통과 |
| `--only physics` / `particles` / `rebinding` / `--rebinding-only` / `names` / `player-yaml` | — | 종료 0, PASS 33 / 76 / 26 / 26 / 3 / 6 | 통과 |
| `--only build-settings --tier fast` | — | PASS 5, SKIP 3 | 통과 |
| `--only nope`, `--tier bogus` | 모르는 이름은 목록과 함께 실패 | 종료 134, `Unknown check group 'nope'. Use --only <name> with one of: …`, `Unknown tier 'bogus'…` | 통과 |
| 매니페스트 검사를 더한 뒤 전체 실행(인자 실수로 생긴 전체 실행) | — | 종료 0, PASS 949줄 | 통과 |

마지막 커밋 전에 wip/next1 `fdd46fe`(aot-size-2: `ProjectYamlResolverChecks`·`GameJsonWriterChecks`, 둘 다 `PlayerYamlChecks.Run` 안에서 실행되어 새 등록이 필요 없음)를 병합했다. 병합 뒤 bootstrap(템플릿 캐시 `c24f384ba03f380e`)을 다시 하고 `./monoon test`를 돌렸다: 종료 0, PASS 952줄(948 + aot-size-2 3줄 + 매니페스트 1줄), 374 s. 템플릿 없는 `--tier fast`: 종료 0, PASS 878줄, SKIP 7줄.

`--only physics`와 `particles`의 묶음 순서는 전체 실행 순서를 따른다. 예전에는 `physics`가 TileContact→Collision→Contact→RigidBody…, `particles`가 Particle→Effects 순서였다. 줄 집합은 같고 묶음 사이 의존은 없다.

### W4 매니페스트와 실행기

`python3 engine/scripts/run-harness.py --check`: 하네스 69개, 문제 0(폴더 목록, 단계 모드 ⊆ 하네스 모드, `gui` 표시와 `$lock` 일치, `$app`을 쓰는 단계의 잠금, `watch` glob과 기준 파일 존재, 단계 bash 문법).

최근 커밋의 `--changed <커밋>^! --plan` 선택:

| 커밋 | 바뀐 곳 | 직접 선택 | broad(`--broad`) | 판단 |
| --- | --- | --- | --- | --- |
| `c648586` Audio Mixer 편집(16.10) | Editor 1, AudioLab.MixerEditor 4, results 13 | AudioLab.MixerEditor(scene-play). CollisionLab.InspectorEdits는 manual이라 건너뜀 | 67 | 에디터만 바뀌어 Release·AOT를 고르지 않는다 |
| `1e6cbe2` Input Actions(16.12) | Editor 2, Checks 1, KeyRoom.InputMapEditor 4 | InputMapEditor, InputInteractions, EditorPanelsDetail(모두 scene-play만) | 66 | 같은 기준 |
| `5f76029` 4/8방향 클립(16.5) | Core 1, Authoring 2, Editor 1, AnimatorLab 33 등 | AnimatorLab.Conditions·Directions(세 모드), InventoryLab.Incremental(headless), KeyRoom.EditorBuild·EditorPanelsDetail(scene-play) | 64 | 샘플을 쓰는 두 하네스 전부, 빌드 도구 변경은 증분 빌드 하네스 |
| `2b396ad` 자유 이동 길찾기(16.15) | Core 3, Editor 3, SeedLab 7, SeedLab.NavigationFree 7 | SeedLab의 4개(세 모드), CollisionLab.EditorPerf(scene-play) | 63 | |
| `ed578dc` 스크립트 필드 타입(13.8) | Core 4, Authoring 4, Editor 4, InventoryLab 1 | InventoryLab 하네스 8개, KeyRoom.FieldRename·EditorExtensions·EditorBuild·ScriptReload | 56 | |
| `d45fc0c`, `556b1ae`, `19fce62`, `7647b5e` | 문서, results만 | 없음("fast tier covers this change") | 0 | |
| `9489f30`(이 브랜치 W5) | Engine.Checks 102, monoon | 없음 | 69(`monoon`이 broad) | `./monoon test`가 맡는다 |

실행기로 돌린 하네스(결과 폴더 `scratchpad/w45/h5`, `fast-2b396ad`, `navfree`):

| 하네스 | 모드 | 종료 | 초 | 비고 |
| --- | --- | --- | --- | --- |
| KeyRoom.Rebinding | scene-play, release, native-aot | 0 | 52 | 프로브 빌드 → Scene Play는 GUI 잠금 안에서 5 s |
| KeyRoom.EditorTheme | scene-play | 0 | 19 | GUI 잠금 |
| CollisionLab.Spatial | release, native-aot | 0 | 46 | README 없는 하네스, phase1 스크립트에서 옮김 |
| BarrageLab.PrefabPools | release, native-aot | 0 | 48 | |
| SeedLab.Autotile | release, native-aot | 0 | 38 | `--tier fast --changed 2b396ad^! --skip-checks` |
| SeedLab.GridNavigation | release, native-aot | 0 | 39 | 같은 실행 |
| SeedLab.TileTerrain | release, native-aot | 0 | 38 | 같은 실행 |
| SeedLab.NavigationFree | release, native-aot | 0 | 36 | 처음에는 Scene 기즈모 단계만 쓰는 창 도구 없음으로 종료 2 → 준비물 검사를 선택된 단계 기준으로 고친 뒤 통과 |
| Input.Gamepad | headless | 134 | 3 | 아래 |

Input.Gamepad 실패(이 브랜치와 무관한 기존 의존): 첫 오류 `Unhandled exception. System.IO.InvalidDataException: Scripts/PlayerMovement.cs: Could not find a part of the path '…/samples/KeyRoom/.monoon-compiled/MoNoon.GameScripts.dll'`. `engine/tests/Input.Gamepad/Program.cs`가 저장소의 `samples/KeyRoom` 씬을 바로 읽어, 에디터로 KeyRoom을 한 번 열어 `.monoon-compiled`가 생긴 체크아웃에서만 통과한다. 새 워크트리에서는 실패한다. `./monoon test project`로는 이 폴더가 생기지 않는다. 고치려면 하네스가 복사본을 만들어 스크립트를 컴파일해야 하므로 이번 범위에서 손대지 않았다. 로그: `h5/Input.Gamepad/run.log`.

Native AOT 빌드가 하네스마다 20 s 정도로 끝난 것은 Native AOT 키트와 콘텐츠 캐시가 이미 있었기 때문이다.

## 재현

```sh
sh engine/scripts/bootstrap-worktree.sh
./monoon test                                   # 전체, PASS 949줄(매니페스트 검사 포함)
mv player-templates player-templates.off && ./monoon test --tier fast; mv player-templates.off player-templates
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --list
python3 engine/scripts/run-harness.py --check
python3 engine/scripts/run-harness.py --changed 'c648586^!' --plan
engine/scripts/build-editor-mac.sh
python3 engine/scripts/run-harness.py Input.Gamepad CollisionLab.Spatial BarrageLab.PrefabPools KeyRoom.Rebinding KeyRoom.EditorTheme --out "$(mktemp -d)"
python3 engine/scripts/run-harness.py --tier fast --changed '2b396ad^!' --skip-checks --out "$(mktemp -d)"
```

## 확인하지 않은 것

- 69개 중 실행기로 실제 돌린 것은 위 9개다. 나머지는 README 명령을 옮긴 것으로 `--check`(문법·경로·잠금)만 통과했다. 특히 창·키보드 권한이 필요한 하네스(Accessibility.Mac, AudioLab.Widgets, DisplayLab.*, KeyRoom.PlayInspector·UiLayout, CameraLab.ScreenEffects)와 `$work`로 출력 경로를 바꾼 하네스는 돌리지 않았다. 처음 돌리다 틀린 단계가 나오면 `harnesses.json`의 해당 `run`을 고친다.
- `--tier full` 전체(69개, 수 시간)와 `./monoon ci`는 돌리지 않았다. `--only` 출력은 이름 집합만 기준과 비교했고, 묶음별 PASS 줄을 기준 바이너리와 나란히 비교하지는 않았다.
- `CameraLab.ScreenEffects`·`SeedLab.NavigationFree`(기즈모)·`Accessibility.Mac`이 쓰는 창 도구(`engine/.artifacts/clip-qa/window`)는 메인 체크아웃에만 있다. 워크트리에서는 실행기가 종료 2로 알린다.
- x64·Windows는 이번 범위 밖이다(VM을 시작하지 않았다). 게스트 전용 명령(`KeyRoom.Desktop/retest.py`, `AudioLab.Vorbis/ci.py win-x64|linux-x64`, `InventoryLab.Replay/build_guest.py`)은 매니페스트의 `omitted`에 적고 넣지 않았다.
- W4 연구 문서의 `./monoon test --harness …` 노출, 기존 하네스의 W3 `harness.py` 이전, GUI 대기열은 하지 않았다.
