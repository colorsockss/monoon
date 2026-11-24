# MoNoon 개발자 가이드

엔진 소스를 따라 읽거나 배포본을 만들 때 쓰는 문서입니다. 명령은 저장소 루트에서 실행합니다. 처음 에디터를 열어보려면 [KeyRoom 실행하기](../README.md#keyroom-실행하기)부터 시작하세요.

## 소스에서 어디를 볼까요?

| 영역 | 맡은 일 |
| --- | --- |
| [Engine.Core](../engine/src/Engine.Core/) | 씬, 컴포넌트, 입력, 물리, 저장과 게임 루프. 창 없이 실행할 수 있는 런타임입니다. |
| [Engine.Desktop](../engine/src/Engine.Desktop/) | BRUTAL의 SDL3 바인딩으로 게임 창을 열고 입력을 읽습니다. 스프라이트와 텍스트를 그리고 오디오를 출력합니다. |
| [Engine.Editor](../engine/src/Engine.Editor/) | Avalonia 기반 편집기입니다. Hierarchy, Scene, Inspector, Project와 에디터 안의 Play를 담당합니다. |
| [Engine.Authoring](../engine/src/Engine.Authoring/) | 에셋 임포트, 프로젝트 변환, 스크립트 컴파일과 게임 내보내기를 담당합니다. |
| [Engine.Player](../engine/src/Engine.Player/) · [Engine.Cli](../engine/src/Engine.Cli/) | Player는 배포 게임의 시작 씬을 엽니다. CLI는 에디터 없이 게임을 빌드합니다. |
| [samples](../samples/) · [packages](../packages/) | 편집용 게임 프로젝트와 다른 프로젝트로 가져올 수 있는 인벤토리·퀘스트·대화·덱 패키지입니다. |

IDE에서는 [엔진 솔루션](../engine/MoNoonEngine.slnx)을 엽니다. 에디터가 게임 프로젝트용으로 생성하는 C# 프로젝트는 컴포넌트를 편집하고 Play에 디버거를 연결할 때 사용합니다. [스크립팅과 외부 IDE](manual/scripting.md)

## 런타임과 씬

키보드·마우스·게임패드 입력은 프로젝트의 액션에 연결됩니다. 게임 루프는 경과 시간을 누적해 1/60초 단위로 시뮬레이션합니다. 렌더러는 두 틱 사이의 위치를 보간합니다. 월드 좌표는 오른쪽이 +X, 아래쪽이 +Y입니다. 창을 조절해도 게임 해상도의 종횡비는 유지됩니다. [게임 루프](../engine/src/Engine.Core/GameLoop.cs) · [입력 액션](manual/input-actions.md) · [카메라](manual/cameras.md)

| 실행 대상 | 화면을 그리는 경로 |
| --- | --- |
| macOS arm64 에디터의 Scene | Cocoa 자식 뷰 안에 SDL의 Metal 렌더러가 직접 그립니다. 첫 소스 빌드는 SDL 3.4.12를 패치해 빌드합니다. 소스 묶음이 없으면 내려받지 않고 멈추므로, 처음 한 번은 `MONOON_ALLOW_SDL_DOWNLOAD=1`을 지정하세요. [패치와 빌드 스크립트](../engine/scripts/build-scene-sdl-mac.sh) |
| Windows x64 에디터의 Scene(Linux 에디터는 지원하지 않음) | Avalonia가 씬을 그립니다. [Scene 뷰 소스](../engine/src/Engine.Editor/Scene/SceneViewport.cs) |
| 독립 실행과 배포 게임 | 세 플랫폼 모두 별도 SDL 창을 엽니다. 에디터에서는 **Run → Save & Run**으로 현재 씬을 실행할 수 있습니다. [데스크톱 호스트](../engine/src/Engine.Desktop/DesktopHost.cs) |

에디터와 배포 게임은 VYaml로 같은 YAML 콘텐츠를 읽습니다. 씬은 `.scene`, 프리팹은 `.prefab`, 애니메이션은 `.clip`에 저장합니다. 프로젝트를 복사할 때는 `Scripts/`, `.monoon-assets`와 각 에셋의 `.meta`도 함께 보관하세요. 에셋을 옮길 때는 Project 패널을 사용하면 ID와 참조가 유지됩니다. [에셋 이동과 재임포트](manual/project-assets.md) · [구버전 JSON·YAML 프로젝트 이전](manual/upgrade.md)

YAML에서 빠진 키는 코드가 선언한 기본값으로 읽힙니다. VYaml의 생성 코드는 속성 초기값을 무시하고 `default(T)`를 넣으므로, 엔진 형식에 0·false·null이 아닌 기본값을 줄 때는 `[YamlConstructor]` 생성자의 매개변수 기본값으로도 적습니다. 상수로 쓸 수 없는 값(배열, 범위 같은 중첩 형식)은 `T? x = null` 매개변수로 받아 `x ?? 초기값`으로 채우고, 이 형식은 null이 될 수 있는 참조 형식이어야 합니다. `./monoon test`가 모든 `[YamlObject]` 형식을 빈 매핑에서 읽어 선언한 기본값과 비교합니다. [검사](../engine/tests/Engine.Checks/YamlDefaultChecks.cs) · [기록](../engine/tests/results/yaml-defaults-determinism/README.md)

## 편집할 때 찾는 문서

| 작업 | 사용법 |
| --- | --- |
| 오브젝트 계층과 프리팹 | Hierarchy에서 자식을 추가하거나 복제합니다. 오브젝트를 Project로 끌면 프리팹이 됩니다. [다중 선택·단축키·프리팹 편집](manual/editor.md) |
| C# 컴포넌트와 이벤트 | **Add Component**로 스크립트를 붙입니다. Inspector에서 참조와 이벤트 수신자를 지정하고 톱니바퀴의 **Edit Script**로 외부 IDE를 엽니다. [스크립팅](manual/scripting.md) · [KeyRoom 이벤트 검증](../engine/tests/results/game-events/README.md) |
| 스프라이트와 타일 | PNG를 Scene에 끌어 배치합니다. KeyRoom의 Door.clip에서는 시트 셀의 순서와 시간을 바꿀 수 있습니다. [애니메이션](manual/sprites.md) · [타일맵](manual/tilemaps.md) |
| 충돌과 UI | 충돌체의 **Is Trigger**로 접촉을 감지합니다. 게임 화면의 버튼은 Canvas 아래에 배치합니다. [충돌](manual/collision.md) · [게임 UI](manual/game-ui.md) |
| 게임별 예제 | 탄막 배칭, 턴 리플레이, 리듬 판정, 음악 스트리밍 등은 각 샘플에서 조작할 수 있습니다. [전체 샘플](../samples/) · [기능별 매뉴얼](manual/README.md) |

## 에디터와 게임 빌드

편집과 Play에는 에디터만 있으면 됩니다. 게임을 내보낼 때는 대상 플랫폼의 플레이어 템플릿을 먼저 만드세요. Native AOT는 대상 OS와 아키텍처에서 빌드합니다. SDK 외에 해당 플랫폼의 네이티브 빌드 도구가 필요합니다. [플랫폼별 준비물](../engine/tests/InventoryLab.Replay/README.md)

아래는 macOS arm64에서 KeyRoom 배포본 두 종류를 만드는 예입니다. 첫 줄은 Release 템플릿과 AOT 키트를 준비합니다.

```sh
./engine/scripts/build-players.sh osx-arm64
./monoon build samples/KeyRoom release
./monoon build samples/KeyRoom native-aot
```

Release는 .NET 런타임과 컴파일한 게임 스크립트를 함께 배포합니다. Native AOT는 게임 스크립트를 플레이어 실행 파일에 컴파일합니다. 두 방식 모두 기본 배포 프로필은 `distribution`입니다. 디버그 심볼은 빌드 ID별로 보관합니다. [프로필·심볼·출력 경로](manual/build.md)

| 생성 위치 | 내용 |
| --- | --- |
| `editor/` · `samples/dist/` | 플랫폼별 패키징 스크립트가 만든 에디터와 편집용 샘플 압축 파일. [에디터 패키징 명령](manual/editor.md#데스크톱-배포-에디터) |
| `player-templates/` | 대상별 Release 플레이어와 Native AOT 키트. 다른 위치에 두면 `MONOON_PLAYERS_DIR`로 경로를 지정합니다. |
| `game-builds/KeyRoom/` | 위 명령으로 만든 Mac 앱은 `KeyRoom.app`과 `NativeAOT/KeyRoom.app`입니다. Windows·Linux에서는 플랫폼별 출력 폴더 전체를 전달합니다. |
| `engine/.artifacts/` | 엔진의 중간 빌드 파일과 네이티브 라이브러리 캐시입니다. |

에디터에서 같은 작업을 하려면 **Build → Build Game…**에서 시작 씬, 포함할 씬과 플랫폼을 고릅니다. 외부 배포 시 BRUTAL의 고지, 시작 로고, 크레디트 조건과 함께 배포하는 패키지의 라이선스를 확인하세요. [빌드 매뉴얼](manual/build.md) · [BRUTAL 라이선스](https://github.com/ahwootech/BRUTAL/blob/main/LICENSE.txt)

## 워크트리 준비

`git worktree`로 만든 새 작업 폴더에는 gitignore된 네이티브 라이브러리와 플레이어 템플릿이 없습니다. 첫 빌드 전에 워크트리 루트에서 한 번 실행하세요. 네트워크를 쓰지 않습니다.

```sh
engine/scripts/bootstrap-worktree.sh
```

| 준비하는 것 | 방법 |
| --- | --- |
| Mac Scene용 `libSDL3.dylib`와 `libBrutalScenePanel.dylib` | 메인 체크아웃의 `engine/.artifacts/native/osx-arm64`에서 복사합니다. 패치나 브리지 소스가 메인과 다르면 복사하지 않고 다시 빌드하게 둡니다. SDL 소스 묶음과 CMake는 메인 체크아웃의 것을 링크합니다. |
| `player-templates/` | 플레이어 소스(Engine.Core·Desktop·Player, `engine/native`, 빌드 스크립트)의 커밋 해시로 `<메인 체크아웃>/.monoon-cache/players/<hash>`를 찾아 링크합니다. 없으면 한 번만 빌드합니다. 같은 소스의 두 번째 워크트리는 템플릿을 빌드하지 않습니다. |

- 플레이어 소스를 고친 워크트리는 캐시를 쓰지 않습니다. `engine/scripts/build-players.sh`로 로컬 템플릿을 만드세요. 이 스크립트는 캐시 링크를 지우고 워크트리 안에 빌드하므로 공유 캐시를 덮어쓰지 않습니다.
- `MONOON_PLAYERS_DIR`가 지정되어 있으면 템플릿은 건드리지 않습니다.
- Claude Code가 만드는 워크트리는 저장소의 [`.worktreeinclude`](../.worktreeinclude)에 따라 두 dylib을 미리 복사합니다. 템플릿 링크는 위 스크립트가 만듭니다.
- 캐시는 템플릿 세트 하나에 약 290 MB입니다. 오래된 항목은 `.monoon-cache/players/`에서 지워도 됩니다. 다음 실행 때 다시 빌드합니다.
- `build-scene-sdl-mac.sh`는 SDL 소스 묶음이 없으면 `curl`을 부르지 않고 이 스크립트를 안내한 뒤 실패합니다.
- 여러 에이전트가 함께 작업할 때의 GUI 잠금, 하네스·결과 기록, 병합 규칙은 [에이전트 작업 흐름](manual/agent-workflow.md)에 있습니다.

## 회귀 검사와 문제 해결

| 확인할 내용 | 재현 방법 |
| --- | --- |
| InventoryLab 빌드와 저장 비교 | macOS arm64·Linux x64 셸에서 `./monoon ci`를 실행합니다. 두 플레이어를 빌드하고 약초 의뢰 입력을 재생합니다. 기존 빌드는 `./monoon test replay game-builds/InventoryLab`으로 검사합니다. [세 OS의 실행 절차](../engine/tests/InventoryLab.Replay/README.md) |
| 엔진 전체 회귀 검사 | macOS·Linux 셸에서 `./engine/scripts/build-players.sh`로 세 플랫폼 템플릿을 준비합니다. 이어서 `./monoon test`를 실행합니다. [CLI와 검사 범위](manual/build.md#터미널에서-빌드와-회귀-검사) |
| KeyRoom 데스크톱 조작 | 방향키와 WASD로 각각 두 번째 방까지 이동합니다. 창 크기를 바꾸고 게임 영역의 16:9 비율을 확인합니다. [데스크톱 검사기](../engine/tests/KeyRoom.Desktop/README.md) · [에디터 조작 기록](../engine/tests/results/editor-packaging/README.md) |

에디터 하네스는 패키지 에디터에 `DOTNET_STARTUP_HOOKS`로 검사 어셈블리(프로브)를 넣어 실행합니다. 프로브는 공용 소스 [Probe.cs](../engine/tests/Shared/Probe.cs)를 링크해 씁니다. `Start`가 메인 창을 기다려 검사를 실행하고, 실패하면 `error.txt`를 남긴 뒤 에디터를 닫습니다. `Until`과 `Delay`는 `MONOON_QA_SLOW` 배율을 따르며 기본 제한은 30초입니다. 프로브가 일부러 열지 않은 창(저장 확인, 선택 질문, 오류 상자)이 뜨면 약 0.25초 안에 창 제목과 문구를 `error.txt`에 적고 종료 코드 3으로 끝납니다. 프로브가 여는 창은 `allow:`에 제목을 적습니다. 각 `editor.py`의 에디터 실행 제한은 측정한 실행 시간을 바탕으로 240–900초(`MONOON_QA_SLOW` 배율)입니다. [측정과 확인 기록](../engine/tests/results/agent-tooling/README.md)

오류를 재현할 때는 OS·아키텍처와 실행 대상을 기록하세요. Scene 편집, Scene Play, Save & Run, 배포 게임은 확인할 경로가 다릅니다. VM 재실행 방법, SDL 라이브러리 진단과 이전 검증 캡처는 아래 문서에서 찾을 수 있습니다. [macOS](troubleshooting/macos.md) · [Windows](troubleshooting/windows.md) · [Linux](troubleshooting/linux.md) · [검증 기록 모음](../engine/tests/results/)

초기 개발은 SDL 창에서 사각형을 움직이는 실험으로 시작했습니다. 이후 씬 편집과 저장, KeyRoom의 열쇠와 문, 세 운영체제의 배포 검증으로 확장했습니다. [초기 설계](../engine/design/2d-engine-roadmap.md) · [현재 로드맵](roadmap.md)
