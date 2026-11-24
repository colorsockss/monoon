# MoNoon Engine 범용 2D 로드맵

하나의 장르에 맞춘 완성형 시스템보다 여러 장르가 함께 쓸 실행 기능을 먼저 만든다. 장르 규칙(예: 상자 열기, 코인 획득)은 엔진 내장이 아니라 프로젝트 스크립트로 둔다. 이 문서에는 **남은 작업만** 둔다.

- 구현을 마친 1–16장 항목은 로드맵에서 뺐다(2026-11-25). 사용법은 [사용자 매뉴얼](manual/README.md)에, 어떻게·왜 그렇게 만들었는지는 [구현 기록](history/README.md)에 있다. 기록의 `[n.m]`이 예전 로드맵 번호다.
- 공통 조건: 모든 기능은 macOS arm64에서 Scene Play·Release·Native AOT로 확인한다. Linux x64는 게임 실행 대상과 CI만이고 에디터는 없다(2026-11-23 결정). **Windows x64 검증은 장비가 준비되면 한 번에 진행한다** — 할 일은 [Linux x64 회귀의 Windows 목록](../engine/tests/results/next1-x64/README.md)에 모아 두었다.

## 남은 항목과 보류

| 순서 | 기능 | 상태와 남은 범위 |
| --- | --- | --- |
| 4.4 | 멀티플레이 | **보류.** 로컬 협동, 네트워크 복제·동기화. 지금은 네트워크 코드가 없다(에디터 디버그 서버만 소켓을 쓴다). |
| 5.2 | 모바일·웹 타깃 | **보류.** BRUTAL이 Android, iOS, 웹을 각각 공식 지원할 때 해당 타깃의 개발을 재개한다. 빌드 대상은 osx-arm64·win-x64·linux-x64뿐이다. |
| 11.12 | 배포본 서명과 설치 파일 (남은 부분) | 서명·공증·tar.gz 단계는 구현했다([기록](history/build.md), [사용법](manual/build.md)). 남은 것: Windows 설치 프로그램 단계(지금은 ZIP + signtool만, 만드는 도구를 내려받아야 해서 보류), 앱 아이콘(.icns·exe), 실제 Developer ID 서명·공증과 signtool 확인(인증서 없음). |

## 17. 게임 개발자용 AI 에이전트 도구

2026-11-25 정리. [다음 작업 조사 6](plans/next-work/6-ai-game-dev.md)의 G1–G9와 [사용자 결정](plans/next-work/6-ai-game-dev.md#결정-2026-11-25-사용자-확정)을 옮겼다. MoNoon 에디터로 게임을 만드는 개발자가 Claude Code·Codex 같은 에이전트에게 편집·검사·실행을 맡길 수 있게 한다. 엔진 개발용 에이전트 도구(W)는 [조사 5](plans/next-work/5-ai-workflow.md)에 있다. 모든 항목은 에디터·빌드 도구 쪽(`Engine.Authoring`·`Engine.Cli`·`Engine.Editor`)에만 들어가고 배포 게임(Release·Native AOT)에는 0 B다. `Engine.Player`·`Engine.Core`·`Engine.Desktop`은 새 참조를 받지 않는다. JSON은 `JsonSerializerContext`로 만들고, 리플렉션은 도구 쪽에서만 쓴다. 에디터 패키지 크기와 명령 시작 시간을 측정해 남긴다. macOS arm64만 만들고 검증한다(결정 8). Windows는 장비 준비 후 검증한다.

| 순서 | 기능 | 범위와 완료 기준 |
| --- | --- | --- |
| 17.1 | 에디터에 CLI 통합 (G1) | `Engine.Cli`의 명령 해석을 `Engine.Authoring`으로 옮기고 에디터 실행 파일이 `MoNoon cli …`로 넘긴다(Avalonia 초기화 전에 분기). 에디터 패키지에 얇은 `monoon` 래퍼를 넣는다(결정 1). 에디터의 `--build-*`·`--migrate-project`는 별칭으로 남기고 BuildSettings·profile·target을 CLI와 똑같이 따른다. 두 진입점이 같은 인자에 같은 stdout·종료 코드를 내고, 패키지의 `monoon test project KeyRoom`이 0을 반환한다. 에디터 증가 약 30 KB, 명령 시작 0.1 s 이내(지금 `./monoon` 3.26 s). |
| 17.2 | 기계가 읽는 출력 `--json` (G2) | `build`·`test project`·`check`·`run`·`profile`이 결과를 JSON 객체 하나로 stdout에, 진행은 stderr에 낸다. 종료 코드 0/1/2를 문서화한다. 사람용 출력은 그대로다. KeyRoom 통과 결과와 일부러 실패하게 만든 픽스처의 JSON 필드를 Engine.Checks가 비교한다. |
| 17.3 | 프로젝트 검사 `monoon check` (G3) | 설정·씬·프리팹·클립·`.particles`를 기존 로더로 읽고 `ProjectCatalog.Inspect`의 깨진 참조를 함께 보고한다. 읽은 문서를 다시 직렬화해 원본에서 빠진 키(오타 키)를 `file:line`으로 경고한다. 스크립트 오류·경고를 개수 제한 없이 `id·severity·file·line·column`으로 낸다. `.meta`·`.monoon-assets`를 쓰지 않는 읽기 전용 경로라 실행 전후 파일 해시가 같다. 오타 키·없는 GUID·깨진 프리팹 맵·컴파일 오류 픽스처가 각각 기대한 진단을 내고 KeyRoom은 문제 0개다. |
| 17.4 | 게임 프로젝트용 에이전트 문서와 스키마 (G4) | 새 프로젝트와 샘플에 `AGENTS.md`(폴더 구조, 편집 → check → test → run 순서, GUID는 17.6 명령으로, 열린 에디터와의 관계)와 `CLAUDE.md`(`@AGENTS.md`)를 넣는다(결정 6). `monoon schema [--json]`이 컴포넌트의 YAML 키·타입·기본값과 프로젝트 스크립트 필드를 낸다. `AGENTS.md`의 명령을 하네스가 KeyRoom 복사본에서 모두 실행해 0을 확인한다. |
| 17.5 | 헤드리스 실행 `monoon run` (G5) | `monoon run <project> [--scene] --frames N [--input <.inputreplay> \| --press Action@frame…] [--snapshot …] [--dump objects,session] [--json]`. 항상 Release로 빌드한 뒤 `MoNoonPlayer` 헤드리스 재생으로 실행한다(결정 7). 같은 입력이면 빌드를 다시 하지 않도록 빌드 결과를 캐시한다. 결과는 로그, 첫 예외와 프로젝트 줄 위치, 최종 오브젝트·세션 상태, PNG 경로다. KeyRoom `TakeKey.inputreplay` 실행 후 세션에 `key`가 있고, PNG가 같은 빌드의 `MONOON_REPLAY_SNAPSHOTS` 결과와 같다. 두 번째 실행의 빌드 캐시 적중 시간을 기록한다. |
| 17.6 | 씬·프리팹 편집 명령 (G6) | `monoon scene new-object`·`add-component`·`instantiate-prefab`·`set <object>/<component>.<field>=<값>`·`remove`·`list [--json]` 6개(결정 5). 모두 `SceneDocument` load/save를 거쳐 GUID·`schemaVersion`·프리팹 인스턴스 맵·스크립트 필드 정규화를 엔진이 맞춘다. 명령으로 만든 씬이 에디터에서 같은 편집으로 만든 씬과 같은 YAML이고 17.3을 통과한다. |
| 17.7 | 열린 에디터와의 충돌 방지 (G9) | 에디터가 `.monoon/editor.lock`(pid, 열린 씬, 미저장 여부)을 쓴다. 17.6·17.8의 쓰기는 대상 씬에 미저장 편집이 있으면 거부(exit 1, JSON 이유)하고, 17.10 브리지가 있으면 브리지로 보낸다. `--force`일 때만 쓴다. 17.3은 항상 읽기 전용이다. |
| 17.8 | stdio MCP 서버 `monoon mcp` (G7) | 별도 stdio 프로세스(결정 3)로 `check`·`test`·`run`·`build`·`scene_*`·`schema`를 도구로, 매뉴얼·스키마·마지막 실행 로그를 리소스로 낸다. 도구에 읽기·쓰기 힌트를 단다. 공식 C# SDK `.Core`를 명시 등록으로 먼저 시험하고(내려받기는 시작할 때 승인), 에디터가 1 MB 넘게 커지거나 trim 경고가 새로 생기면 JSON-RPC를 직접 구현한다(결정 2). 게임 프로젝트에 `.mcp.json`을 넣는다. Python stdio 클라이언트 하네스가 `initialize` → `tools/list` → `tools/call check`를 KeyRoom에 실행하고 stdout에 MCP 외 출력이 없다. |
| 17.9 | 에디터 자동화 API (W6) | 17.10의 바탕. [조사 5의 W6](plans/next-work/5-ai-workflow.md#w6-안정된-에디터-자동화-api-리플렉션-제거--m-저장소)의 리플렉션 없는 에디터 자동화 API를 만든다. 기존 리플렉션 프로브 몇 개를 이 API로 옮겨 같은 결과를 확인한다. |
| 17.10 | 에디터 라이브 브리지 (G8) | 에디터가 루프백에서 듣고 `.monoon/agent/<pid>.json`에 주소와 토큰을 쓰고, 17.8이 이 파일로 에디터를 찾는다. 선택 읽기·쓰기, Inspector 값 읽기, 필드 설정(`SetField`/`RecordEdit`, Undo 한 번), Play·Stop·Pause·Step, Console 읽기, Scene·Game 스크린샷, 씬 저장. 처음 연결할 때 에디터에서 승인받고 프로젝트에 기억한다. Undo로 기록되는 쓰기만 허용하고 저장·삭제·빌드는 destructive로 표시한다(결정 4). 호출은 UI 스레드에서 하나씩 실행한다. GUI 잠금 아래 KeyRoom에서 Player 선택 → 값 변경 → Undo → Play → Console 기록 → 스크린샷이 비지 않음을 확인한다. |
| 17.11 | MoNoon용 Claude Code 플러그인 | 17.4의 문서, 17.8의 MCP 설정, 게임 개발 skill(편집 → 검사 → 테스트 → 실행, 빌드·패키지)과 에이전트 정의를 하나의 플러그인으로 묶는다(결정 6). 에디터 패키지와 함께 배포하고 버전을 맞춘다. 빈 KeyRoom 복사본에 설치해 skill 명령이 17.1–17.8 명령으로 끝까지 실행되는지 확인한다. |
