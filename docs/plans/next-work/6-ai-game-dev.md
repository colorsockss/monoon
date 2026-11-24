# 6. 게임 개발자를 위한 AI 에이전트 도구 (CLI·MCP)

조사일 2026-11-24, 기준 `.worktrees/next1` (wip/next1, `40d6020`). 경로는 저장소 루트 기준이다. 읽기 전용으로 조사했고, 측정은 macOS arm64에서 CLI 시작 시간만 쟀다. 창은 열지 않았고 패키지도 설치하지 않았다.

[5-ai-workflow.md](5-ai-workflow.md)는 **엔진을 개발하는** 에이전트(워크트리, 하네스, hook)를 다룬다. 이 문서는 **MoNoon 에디터로 게임을 만드는 개발자**가 Claude Code·Codex·Cursor 같은 에이전트에게 게임 작업을 맡길 때 필요한 도구를 다룬다. 겹치는 부분은 W6(에디터 자동화 API) 하나다.

## 1. 현재 상태

### 1.1 에이전트가 지금 할 수 있는 일

| 작업 | 명령 | 출력·종료 코드 | 막히는 점 |
| --- | --- | --- | --- |
| 게임 빌드 | `./monoon build <project> [release\|native-aot] [--profile …] [--target …] [--output …]` | 결과 경로는 stdout 마지막 줄, 진행·콘텐츠 판정은 stderr. 0 성공, 1 실패, 2 사용법 오류 | `./monoon`은 저장소의 `dotnet run`이라 호출마다 **3.26 s**가 든다(같은 DLL을 직접 실행하면 0.06 s). 에디터 패키지에는 `MoNoon.Cli`가 없다(`package-editor.py`, `MoNoon.app/Contents/MacOS`). |
| 빌드(에디터 패키지만 있을 때) | `MoNoon --build-release\|--build-native-aot <project> <dest>` | 결과 경로 1줄, 실패 시 exit 1 | `BuildSettings`·profile·target을 무시하고 현재 RID로만 빌드한다(`Shell/Program.cs:32`). CLI와 동작이 다르다. |
| 게임 스크립트 테스트 | `./monoon test project <project>` | `PASS 이름` / `FAIL 이름 (씬) at frame N, tick T` + 예외 + 프로젝트 줄 위치 + 마지막 로그 5줄. 0/1 | 사람용 텍스트뿐이다. 에디터 패키지에서는 실행할 수 없다. |
| 스크립트 컴파일 오류 | 전용 명령 없음 | test·build가 실패하면서 **오류 처음 12개**만 예외 메시지로 낸다(`ProjectScripts.cs:294`). 경고는 버린다 | 대안은 `MoNoon --generate-script-project <project>` 후 `dotnet build .monoon/ide/*.Scripts.slnx`다. .NET SDK가 필요하고 MoNoon 컴파일과 똑같다는 보장이 없다. |
| 빌드한 게임 헤드리스 실행 | `MoNoonPlayer --smoke-test <profile> [control …]`, `--replay-input <rec> <profile>` | `PASS player smoke…`, `Headless replay completed: N frames…`. 0/1/2 | 먼저 빌드해야 한다. 상태 덤프가 없어 결과는 로그와 종료 코드뿐이다. |
| 캡처·계측 | 환경 변수 `MONOON_REPLAY_SNAPSHOTS=<dir>;<frame,…>`(PNG), `MONOON_REPLAY_CAPTURE`(프레임 CSV), `MONOON_FRAME_PROFILE`(개발 빌드), `MONOON_AUDIO_CAPTURE`, `MONOON_FEEDBACK_LOG`, `MONOON_STARTUP_TRACE` | 파일 | 사용자 문서에 흩어져 있고 하네스(`engine/tests/*/verify.py`)만 조합해 쓴다. 창을 막는 `SDL_VIDEO_DRIVER=monoon-no-video`도 하네스 관례일 뿐이다. |
| 입력 녹화 작성 | `.inputreplay`는 YAML 텍스트다(`samples/KeyRoom/Tests/TakeKey.inputreplay`, 527 B) | — | 에이전트가 직접 써도 되지만 형식 문서가 없다. `project`·`startScene` GUID를 맞춰야 한다. |
| 프로파일·크래시·이전 | `monoon profile <csv>`, `monoon crash explain`, `monoon migrate` | 텍스트 | — |
| 참조 검사 | 없음(CLI) | — | `ProjectCatalog.Inspect`(Authoring, UI 없음)가 이미 깨진 참조 목록을 만들지만 에디터의 **Check Missing References…**에서만 부른다. |

정리하면 에이전트는 빌드·테스트·리플레이를 **할 수는** 있다. 하지만 (1) 저장소와 .NET SDK가 있어야 하고, (2) 결과가 사람용 텍스트이며, (3) 편집 직후 "이 파일이 맞는가"를 확인할 빠른 명령이 없고, (4) 실행 중인 에디터와는 전혀 대화할 수 없다.

### 1.2 에이전트가 손으로 고칠 파일과 함정

| 파일 | 형식 | 에이전트가 만날 문제 |
| --- | --- | --- |
| `Scenes/*.scene` | YAML, `schemaVersion` + `objects`. 컴포넌트는 `type:` 판별자로 45종(`SceneComponent.cs`) | 모르는 키는 **오류 없이 버려진다**(VYaml 생성 코드의 `SkipUnmappedMember`). `colour:` 같은 오타는 저장 때 사라진다. 오브젝트·에셋·스크립트 참조가 모두 GUID라 새 오브젝트에는 새 GUID가, 에셋 참조에는 `.meta`의 ID가 필요하다. `schemaVersion`은 6–42를 받고 저장할 때 쓴 기능으로 다시 계산된다(`SceneDocument.Validate`). 프리팹 인스턴스 맵이 어긋나면 `"<이름> has an invalid prefab object map."`로 씬 전체를 거부한다. |
| `*.prefab`, `*.clip`, `*.particles` | 같은 YAML | 위와 같다. 프리팹 구조(`PrefabStructure`)와 인스턴스 오버라이드는 손으로 맞추기 어렵다. |
| `GameSettings`, `BuildSettings` | 확장자 없는 YAML | `BuildSettings` 값은 CLI 인자와 같은 단어다(문서화됨). 입력 액션 이름은 스크립트·테스트와 문자열로 맞춰야 한다. |
| 새 에셋(PNG 등) | 파일 + `.meta` + `.monoon-assets` | `ProjectAssets.Scan`이 **읽을 때 `.meta`와 목록을 써 넣는다.** 그래서 지금 있는 로더로 "검사만" 하면 파일이 바뀐다. `.meta`째 복사한 파일은 ID가 겹쳐 조용히 새 ID를 받는다. |
| `Scripts/*.cs`, `Tests/*.cs`, `Editor/*.cs` | C#. Roslyn이 메모리에서 컴파일한다(`ProjectScripts`) | 스크립트 필드 값은 씬의 `values:`에 필드 이름 문자열로 들어간다. 이름이 틀려도 로드는 되고, 저장 때 `NormalizeFields`가 정리한다. |

- 스키마 파일(JSON Schema 등)이 없다. 사용자 문서는 저장소의 `docs/manual/`에만 있고 에디터 패키지와 샘플 ZIP(`package-editor.py keyroom`)에는 들어가지 않는다. 샘플과 새 프로젝트에 `AGENTS.md`·`CLAUDE.md`가 없다.

### 1.3 실행 중인 에디터의 표면

- **외부 자동화 API 없음.** 하네스는 StartupHook + 리플렉션으로 에디터 내부를 만진다(`BindingFlags.NonPublic`을 쓰는 테스트 파일 59개). W6이 `internal` 자동화 API를 계획했지만 아직 없다.
- **에디터 확장 API**(`IEditorExtension`, [workspace.md](../../manual/workspace.md#프로젝트-에디터-확장)): `EditorContext.Scene`·`Selected`·`IsPlaying`·`PlacePrefab`·`Log`·`RecordEdit`·`SetField`(Undo 한 번). 라이브 브리지가 쓸 편집 경로가 이미 공개되어 있다.
- **선례: `SceneDebugServer`.** 루프백 TCP DAP 서버가 32바이트 토큰과 `.monoon/ide/ScenePlay.<pid>.code-workspace` 발견 파일을 쓴다. 에이전트 브리지에도 같은 발견·인증 방식을 쓸 수 있다.
- **Console**은 최근 1,000개를 메모리에만 보관한다. 외부에서 읽을 방법이 없다.
- **외부 편집**: Play 중 스크립트 저장은 약 0.75 s 안에 다시 불러온다. 열린 씬 파일을 다른 프로세스가 고치면 **Save Scene**에서 처음 연 상태·현재 편집·디스크를 3자 병합한다. 열린 씬에 디스크 변경이 바로 반영되지는 않는 것으로 보인다(추정, 확인하지 않음).

## 2. 다른 엔진 사례

| 엔진 | 구조 | 참고할 점 |
| --- | --- | --- |
| Unity (공식) | AI 클라이언트가 띄우는 **stdio 중계 프로세스**(`~/.unity/relay/`)가 에디터 안 브리지와 IPC(Windows 명명 파이프, macOS·Linux Unix 소켓)로 통신한다. 외부 클라이언트는 Project Settings에서 승인해야 하고 승인은 기억된다. C#으로 사용자 도구를 등록한다. [개요](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.6/manual/integration/unity-mcp-overview.html) · [시작](https://unity.com/blog/unity-ai-mcp-how-to-get-started) | stdio와 에디터 IPC를 분리하고 연결을 승인받는다. |
| Unity (커뮤니티) | CoplayDev `unity-mcp`: `manage_scene`(로드·저장·계층), `read_console`, `manage_editor`(Play 모드·태그·레이어) 등. [도구 목록](https://coplaydev.github.io/unity-mcp/reference/tools) | 도구 수를 작게 묶어 동작 인자로 나눈다. |
| Unreal 5.8+ (공식, 실험) | 에디터 프로세스 안의 Streamable HTTP `127.0.0.1:8000/mcp`. Origin 허용 목록은 있지만 **인증은 없다.** 툴셋은 C++/Python으로 정의하고 Read/Write/Execute를 `readOnlyHint`로 알린다. 기본적으로 메타 도구 4개(`list_toolsets` 등)로 도구 검색을 한다. 호출은 게임 스레드에서 하나씩 실행되고, Claude Code는 약 120 s에서 끊긴다. [문서](https://dev.epicgames.com/documentation/unreal-engine/unreal-mcp-in-unreal-editor) | 읽기·쓰기 표시, 직렬 실행, 긴 작업 분할. 인증이 없는 HTTP는 따라 하지 않는다. |
| Godot (커뮤니티) | Coding-Solo `godot-mcp`: 에디터 실행, 디버그 실행 + stdout 수집. `godot-mcp-runtime`: 실행 시 브리지를 주입해 키 입력·화면 읽기. 스크린샷이 가장 덜 성숙했다. [godot-mcp](https://cdn.jsdelivr.net/gh/coding-solo/godot-mcp@main/README.md) · [runtime](https://raw.githubusercontent.com/Erodenn/godot-mcp-runtime/main/README.md) | 가장 많이 쓰는 기능은 "실행해서 로그 받기"다. |
| Bevy | 게임이 BRP(JSON-RPC)를 열고 `bevy_brp_mcp`(stdio)가 이를 중계한다. 앱이 에이전트에게 공개할 메서드를 고른다. [docs.rs](https://docs.rs/bevy_brp_mcp) | 배포 게임에는 넣지 않는 개발용 플러그인으로 둔다. |

**MCP 기본**([사양 2025-11-25 transports](https://modelcontextprotocol.io/specification/2025-11-25/basic/transports)): 서버는 tools·resources·prompts를 제공한다. stdio에서는 클라이언트가 서버를 자식 프로세스로 띄우고 줄 단위 JSON-RPC를 주고받는다. stdout에는 MCP 메시지만 쓰고 로그는 stderr로 보낸다. 로컬 HTTP는 Origin 검사, localhost 바인딩, 인증을 요구한다.

**C# SDK**: 공식 `ModelContextProtocol` v2.0(2026-07-28, [발표](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/))은 Core(클라이언트·저수준 서버), 메인(stdio·호스팅·속성 기반 탐색), AspNetCore(HTTP)로 나뉜다. `ModelContextProtocol.Core` 2.4.0(2026-11-07)은 nupkg 2.24 MB이고 `Microsoft.Extensions.AI.Abstractions`·`Logging.Abstractions`에 의존한다([NuGet](https://www.nuget.org/packages/ModelContextProtocol.Core)). Native AOT·trim 지원은 공식 문서에 없다. 속성 기반 도구 탐색(`WithToolsFromAssembly`)은 리플렉션이므로 쓴다면 도구를 명시 등록한다. BRUTAL에는 JSON-RPC·MCP 기능이 없다([brutal.md](../../brutal.md)).

**배포 경로**: Claude Code는 프로젝트의 `.mcp.json`(커밋 가능, 처음 쓸 때 승인)과 `claude mcp add <이름> -- <명령>`으로 stdio 서버를 등록한다. 플러그인은 MCP 서버와 skill을 함께 묶을 수 있다(`${CLAUDE_PLUGIN_ROOT}`). [MCP](https://code.claude.com/docs/en/mcp) · [플러그인](https://code.claude.com/docs/en/plugins). Codex·Cursor 등은 [AGENTS.md](https://agents.md)를 읽는다.

## 3. 제안 작업 목록

모든 항목은 에디터·빌드 도구 쪽(`Engine.Authoring`, `Engine.Cli`, `Engine.Editor`)에만 들어간다. 배포 게임(Release/Native AOT)에는 **0 B**이고, `Engine.Player`·`Engine.Core`·`Engine.Desktop`은 새 참조를 받지 않는다. JSON은 `JsonSerializerContext` 소스 생성기로 만든다. 리플렉션은 도구 쪽에서만 허용하고 필요한 곳(G4 스키마 생성)에만 쓴다.

| ID | 크기 | 게임 개발자가 얻는 것 | 코드 위치 | 의존성·용량 |
| --- | --- | --- | --- | --- |
| G1 | S | 에디터만 설치해도 같은 `monoon` 명령을 0.1 s 안에 실행 | `Engine.Cli` 본문을 `Engine.Authoring/Cli/`로 옮기고 에디터 `Program.Main`이 `MoNoon cli …`로 넘긴다. 패키지에 `monoon` 셸·cmd 래퍼 추가 | 새 의존성 없음. 에디터에 약 30 KB(`MoNoon.Cli.dll` 29 KB 기준). Avalonia 초기화 전에 분기하므로 GUI 시작 비용 없음 |
| G2 | S | `--json`: 결과를 객체 하나로 stdout에, 진행은 stderr. 종료 코드 0/1/2 문서화 | `Engine.Authoring/Cli/` + `CliJsonContext` | System.Text.Json(에디터에 이미 있음) |
| G3 | M | `monoon check <project>`: 편집 직후 파일 검증 + 전체 컴파일 진단 | `Engine.Authoring/ProjectCheck.cs` | 없음 |
| G4 | S–M | 프로젝트 템플릿 `AGENTS.md` + `CLAUDE.md`(`@AGENTS.md`), 컴포넌트·필드 참조(`monoon schema`) | 템플릿·`package-editor.py keyroom`, `Engine.Authoring/ProjectSchema.cs` | 문서 수 KB |
| G5 | M | `monoon run`: 소스 프로젝트를 빌드 없이 N프레임 실행 + 입력 + 로그·PNG·상태 | `Engine.Authoring/ProjectRun.cs`(ProjectTests와 공유) | 없음(SDL 소프트웨어 렌더러는 이미 `OffscreenResources`에 있음) |
| G6 | M | `monoon scene …`: GUID·스키마·프리팹 맵을 엔진이 맞추는 편집 명령 | `Engine.Authoring/SceneEditCommands.cs` | 없음 |
| G7 | M | `monoon mcp`: stdio MCP 서버(G2–G6을 도구로, 문서·스키마를 리소스로) | `Engine.Authoring/Mcp/` 또는 에디터 진입점 | 결정 2: SDK(nupkg 2.24 MB + 의존성) 또는 직접 구현(JSON-RPC 수백 줄) |
| G8 | L | 에디터 라이브 브리지: 선택·Inspector 값·Play/Stop·Console·스크린샷 | `Engine.Editor/Automation/`(W6 API 위) | 없음. 에디터만 커진다 |
| G9 | S | 열린 에디터와 CLI 편집의 충돌 방지 | `.monoon/editor.lock`, G6·G7 쓰기 도구가 확인 | 없음 |

### G1. CLI를 에디터 패키지에 넣고 진입점 하나로
- 지금은 CLI가 두 벌이다. `Engine.Cli`는 저장소 전용이고, 에디터의 `--build-release`는 BuildSettings를 무시한다. 명령 해석을 한 곳에 두고 두 진입점이 같은 코드를 부르게 한다. 에디터의 기존 `--build-*`·`--migrate-project`는 별칭으로 남긴다.
- **검증**: Engine.Checks에서 두 진입점이 같은 인자에 같은 stdout·종료 코드를 내는지 비교한다. 패키지 하네스에서는 `MoNoon.app/Contents/MacOS/MoNoon cli test project KeyRoom`이 0을 반환해야 한다.

### G2. 기계가 읽는 출력 `--json`
- 대상은 `build`(제품 경로, 콘텐츠 보고서, 경고, 기호 경로), `test project`(테스트별 이름·씬·결과·프레임·틱·예외·`file:line`·로그), `check`, `run`, `profile`이다. 사람용 출력은 그대로 둔다.
- **검증**: KeyRoom 통과 결과와 일부러 실패하게 만든 픽스처(`engine/tests/fixtures/`)의 JSON을 Engine.Checks가 파싱해 필드를 비교한다.

### G3. `monoon check <project> [--json]` (검증·린트, 스크립트 진단 포함)
- GameSettings·BuildSettings와 모든 `.scene`·`.prefab`·`.clip`·`.particles`를 기존 로더로 읽는다(`Validate`). `ProjectCatalog.Inspect`의 `Problems`(깨진 GUID, 형식 불일치)를 함께 보고한다.
- **모르는 키 검출**: 읽은 문서를 기존 writer로 다시 직렬화한 뒤 원본 키 집합과 비교해 빠진 키를 `file:line`과 함께 경고한다. 새 스키마를 따로 유지하지 않는 방법이다.
- **스크립트**: 오류와 경고를 개수 제한 없이 `id·severity·file·line·column`으로 낸다. 옮긴 파일은 `Locations()`의 실제 경로를 쓴다.
- **읽기 전용**: `Scan`이 `.meta`·`.monoon-assets`를 쓰지 않도록 dry-run 경로를 추가하고, 만들어질 메타는 "생성 필요"로만 보고한다.
- **검증**: 오타 키, 없는 GUID, 깨진 프리팹 맵, 컴파일 오류를 하나씩 넣은 픽스처가 각각 기대한 진단을 내고, KeyRoom은 문제 0개여야 한다. 실행 전후 프로젝트 파일 해시가 같아야 한다.

### G4. 게임 프로젝트용 에이전트 문서와 스키마
- 새 프로젝트와 샘플 ZIP에 짧은 `AGENTS.md`를 넣는다. 내용은 폴더 구조, "편집 → `monoon check` → `monoon test project` → `monoon run`" 순서, GUID를 손으로 만들지 말고 G6을 쓰라는 규칙, 열린 에디터와의 관계(G9)다. `CLAUDE.md`는 `@AGENTS.md` 한 줄로 둔다.
- `monoon schema [--json]`은 45종 컴포넌트의 YAML 키·타입·기본값과 프로젝트 스크립트 필드를 낸다. 도구 쪽이므로 리플렉션으로 만들어도 된다. `docs/manual/`의 핵심 문서는 MCP 리소스(G7)로도 제공한다.
- **검증**: 템플릿 `AGENTS.md`의 코드 블록 명령을 하네스가 KeyRoom 복사본에서 모두 실행해 종료 코드 0을 확인한다(문서와 명령이 어긋나지 않게).

### G5. `monoon run` — 헤드리스 N프레임 실행
- 형태: `monoon run <project> [--scene <path>] --frames N [--input <file.inputreplay> | --press Action@frame…] [--snapshot 1,60,120] [--dump objects,session] [--json]`.
- 구현은 `ProjectTests`의 경로(메모리 Roslyn 컴파일, `GameSession`, `GameTestRun`의 입력 라우터)를 공유하고, PNG는 `OffscreenResources`(SDL 소프트웨어 렌더러)로 저장한다. 결과는 로그, 첫 예외와 프로젝트 줄 위치, 최종 상태(오브젝트 이름·위치·활성, 세션 플래그·변수), PNG 경로다.
- 이 경로는 에디터 Play와 같은 JIT·Roslyn 경로다. 출력에 `path: source-jit`을 표시해 Release/AOT 검증으로 오인하지 않게 한다. 배포본 확인은 `--build release|native-aot`를 주면 빌드한 뒤 `MoNoonPlayer --replay-input`과 환경 변수를 조합해 실행한다.
- **검증**: KeyRoom `TakeKey.inputreplay`를 돌리면 세션에 `key`가 생겨야 한다. 같은 입력의 `--snapshot` PNG를 Release 플레이어의 `MONOON_REPLAY_SNAPSHOTS` PNG와 비교한다(픽셀 동일 여부는 확인 전 가설).

### G6. 씬·프리팹 편집 명령
- 최소 묶음은 `scene new-object`, `add-component <type>`(기본값 포함), `instantiate-prefab`, `set <object>/<component>.<field>=<값>`, `remove`, `list [--json]`이다. 모두 `SceneDocument` load/save를 거치므로 GUID 생성, `schemaVersion`, 프리팹 인스턴스 맵, 스크립트 필드 정규화를 엔진이 처리한다.
- 단순 값 변경은 YAML 손 편집 + G3로 충분하다. 명령이 필요한 곳은 GUID와 구조가 생기는 작업이다.
- **검증**: 명령으로 만든 씬과 에디터 프로브로 만든 씬이 같은 YAML인지 비교하고, 결과 씬이 G3를 통과하는지 본다.

### G7. stdio MCP 서버 `monoon mcp`
- 도구: `check`, `test`, `run`, `build`, `scene_*`, `schema`. 각 도구에 읽기·쓰기 힌트(`readOnlyHint`)를 단다. 리소스: 매뉴얼 페이지, 컴포넌트 스키마, 마지막 실행 로그. 120 s를 넘을 수 있는 빌드는 진행 알림을 보내거나 작업 ID를 반환한다.
- 에디터가 없어도 동작하는 별도 프로세스다(Unity 방식). Linux CI에서도 같은 도구를 쓴다. 실행 중인 에디터가 있으면 G8 브리지로 중계한다.
- CLI만으로도 에이전트는 Bash로 같은 일을 할 수 있다. 그래서 MCP의 가치는 이미지 결과(스크린샷), 도구 발견, G8 중계에 있다. CLI(G1–G6)를 먼저 한다.
- **검증**: Python으로 만든 작은 stdio MCP 클라이언트 하네스가 `initialize`→`tools/list`→`tools/call check`를 KeyRoom에 실행하고, stdout에 MCP 외 출력이 없는지 확인한다.

### G8. 에디터 라이브 브리지 (W6 위)
- 에디터가 루프백(Unix 소켓 또는 `SceneDebugServer`처럼 TCP + 토큰)에서 듣고 `.monoon/agent/<pid>.json`에 주소와 토큰을 쓴다. G7이 이 파일로 에디터를 찾는다.
- 도구: `selection get/set`, `object get`(Inspector 값), `field set`(`SetField`/`RecordEdit`, Undo 한 번), `play/stop/pause/step`, `console read --since`, `screenshot scene|game`, `scene save`. 호출은 UI 스레드에서 하나씩 실행한다. Play 중 쓰기는 Play 값 조정 규칙([play-diagnostics.md](../../manual/play-diagnostics.md))을 그대로 따른다.
- 처음 연결할 때 에디터에서 승인 대화상자를 띄운다(Unity 방식). 승인은 프로젝트 `.monoon/`에 기억한다.
- **검증**: GUI 잠금 아래 KeyRoom에서 `Player` 선택 → `MoveSpeed=320` → Undo → Play → Console에 Play 시작 기록 → 스크린샷 PNG가 비어 있지 않음. W6 이후 리플렉션 없는 프로브로 만든다. macOS만 한다(Windows는 장비 준비 후, Linux는 에디터 없음).

### G9. 안전: 열린 에디터와의 충돌
- 에디터가 `.monoon/editor.lock`(pid, 열린 씬, 미저장 여부)을 쓴다. G6·G7 쓰기 도구는 대상 씬이 열려 있고 미저장 편집이 있으면 거부하거나, 브리지가 있으면 브리지로 보낸다. `--force`일 때만 쓰고, 그 경우 Save의 3자 병합이 마지막 안전망이 된다.
- 쓰기 전 백업은 따로 만들지 않고 버전 관리에 맡긴다. `AGENTS.md`(G4)에 "커밋 후 에이전트 편집"을 적는다. G3는 항상 읽기 전용이다.
- **검증**: 에디터 프로브가 씬을 미저장 상태로 두었을 때 `monoon scene set`이 거부(exit 1, JSON 이유)되는지 확인한다.

검토 후 뺀 것: 스크립트 진단 전용 명령(G3에 포함), 에디터 프로세스 안의 HTTP MCP(인증 문제와 에디터 없이 못 쓰는 점, 결정 3), 스크립트 파일 감시 `--watch`(Play의 다시 불러오기가 이미 있음), Claude Code 플러그인 배포(결정 6에서 보류).

## 4. 추천 순서와 결정이 필요한 것

**순서**: G1 → G2 → G3 (여기까지 S·M 세 개로, 에디터만 있어도 "편집 → 검사 → 테스트" 고리가 기계 판독 가능해진다) → G4 → G5 → G6 + G9 → G7 → (W6 뒤) G8.

| # | 질문 | 권장 |
| --- | --- | --- |
| 1 | CLI 배포 형태: 에디터 실행 파일의 하위 명령(`MoNoon cli …`) / 패키지 안 별도 실행 파일 / 지금처럼 저장소 전용 | **에디터 하위 명령 + 얇은 `monoon` 래퍼.** 새 파일이 거의 없고 DLL을 공유한다. 별도 apphost는 Mac 서명·deps.json이 한 벌 더 생긴다. |
| 2 | MCP 구현: 공식 SDK(`ModelContextProtocol.Core`, NuGet 내려받기 승인 필요) / 직접 구현(stdio JSON-RPC, System.Text.Json 소스 생성) | **SDK Core를 명시 등록으로 시험하고 에디터 패키지 증가량을 측정한다.** 사양이 자주 바뀌므로(2025-11 → 2026-07) 따라가는 비용을 줄인다. 1 MB를 넘거나 trim 경고가 생기면 직접 구현으로 바꾼다. |
| 3 | MCP 프로세스: 별도 stdio 프로세스 + 에디터 IPC 브리지(Unity 방식) / 에디터 안 HTTP(Unreal 방식) | **stdio + 브리지.** 에디터 없이도 동작하고 Linux CI에서 쓸 수 있다. HTTP 포트·Origin·인증 문제가 없다. |
| 4 | 라이브 브리지의 쓰기 범위: 읽기와 Play 제어만 / Undo가 기록되는 쓰기까지 | **처음 연결 승인 + Undo 기록 경로의 쓰기까지.** 저장·삭제·빌드는 도구 힌트를 destructive로 표시해 클라이언트가 확인하게 한다. |
| 5 | 씬 편집 CLI(G6) 범위: 최소 6개 / Inspector 수준 전체 | **최소 6개.** 나머지는 YAML 손 편집 + G3로 충분하다. |
| 6 | 게임 프로젝트에 넣을 파일: `AGENTS.md`만 / + `CLAUDE.md` / + `.mcp.json` / Claude Code 플러그인 | **`AGENTS.md` + `CLAUDE.md`(`@AGENTS.md`)**를 지금 넣는다. `.mcp.json`은 G7 뒤에 넣는다(Claude Code가 처음 쓸 때 승인을 받는다). 플러그인은 보류한다. |
| 7 | `monoon run`(G5) 기본 경로: 소스·JIT(빠름, 에디터 Play와 같음) / 항상 빌드 후 플레이어 | **소스·JIT 기본 + `--build`로 배포 경로.** 출력에 경로를 표시한다. |
| 8 | 플랫폼: 헤드리스 명령(G1–G7)을 Linux에서도 지원할까 | **예(CI용)**. 라이브 브리지(G8)는 macOS만, Windows는 장비 준비 후 한 번에 검증한다. |

### 결정 (2026-11-25 사용자 확정)

| # | 결정 | 권장과 다른 점 |
| --- | --- | --- |
| 1 | 에디터 하위 명령 + 얇은 `monoon` 래퍼 | — |
| 2 | 공식 SDK `.Core`를 명시 등록으로 시험한 뒤 판단(+1 MB 초과나 새 trim 경고면 직접 구현). 내려받기는 시험을 시작할 때 따로 승인받는다 | — |
| 3 | 별도 stdio 프로세스 + 에디터 브리지 | — |
| 4 | 첫 연결 승인 + Undo로 기록되는 쓰기만. 저장·삭제·빌드는 destructive 표시 | — |
| 5 | G6은 최소 6개 명령 | — |
| 6 | `AGENTS.md`·`CLAUDE.md`, G7 뒤 `.mcp.json`, **MoNoon용 Claude Code 플러그인(skill·에이전트 묶음)도 만든다** | 플러그인 보류 → 함께 만든다 |
| 7 | **`monoon run`은 항상 Release 빌드 후 플레이어로 실행한다** | 소스·JIT 기본 → 배포 경로만. 호출마다 Release 빌드 시간이 들므로 같은 입력의 빌드 결과를 다시 쓰는 캐시가 필요하다 |
| 8 | **macOS만** 만들고 검증한다 | Linux CI 지원 → 하지 않음. Windows는 장비 준비 후 |

## 확인하지 못한 것

- 모르는 YAML 키를 버린다는 판단은 VYaml 1.6.1 생성기의 `SkipUnmappedMember` 문자열로만 확인했다. 실제 씬으로 재현하지 않았다.
- 공식 C# SDK의 Native AOT·trim 동작과 실제 에디터 패키지 증가량(내려받지 않음).
- G5 PNG와 플레이어 리플레이 PNG의 픽셀 일치, 열린 씬이 디스크 변경을 반영하는지.
- Windows 콘솔 출력. 에디터는 `WinExe`(GUI 서브시스템)라서 파이프로 띄우는 에이전트는 stdout을 받지만, cmd에서 사람이 직접 실행하면 출력이 보이지 않을 수 있다. 그렇다면 결정 1의 래퍼를 `monoon.cmd`가 아니라 작은 콘솔 apphost로 바꾼다.
