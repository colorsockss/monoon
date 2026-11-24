# 이 엔진을 만드는 이유

- **MoNoon은 Unity를 대체하려고 만드는 엔진이다. 목표는 게임 용량 경량화와 성능 최적화다.** Unity의 큰 런타임과 배포 용량, 실행 오버헤드에서 벗어나려고 직접 만든다.
- 모든 작업에서 배포 게임의 용량, 시작 시간, 프레임당 비용, 메모리 할당을 먼저 따진다. 기능이 같으면 더 작고 빠른 쪽을 고른다.
- 런타임에 의존성을 추가하거나 배포본이 커지는 변경은 늘어난 용량과 성능 영향을 확인하고 남긴다. 편의를 위해 Unity 같은 무거운 구조를 다시 들여오지 않는다.

# 에디터 UI 정렬

- UI를 만들거나 수정할 때 같은 패널의 제목, 레이블, 입력 필드, 버튼이 기존 행과 같은 기준선에 맞도록 한다. 기존 그리드 열 너비, 여백, 행 간격을 우선 재사용한다.
- 창 크기를 바꾸거나 긴 이름이 표시될 때도 정렬이 유지되는지 확인한다. 선택 상태와 드래그 강조 표시가 바뀌어도 주변 컨트롤이 밀리지 않도록 한다.

# 에디터와 게임 런타임의 경계

- 에디터는 게임 런타임을 참조할 수 있다. 런타임과 게임 스크립트는 에디터나 빌드 도구를 직접 또는 간접 참조하지 않는다. 양쪽에서 필요한 코드는 에디터 의존성이 없는 공통 모듈에 둔다.
- Release와 Native AOT 배포본에는 게임 실행에 필요한 코드, 라이브러리, 콘텐츠만 포함한다. Inspector, Scene 편집 기즈모, 에셋 임포터 등 에디터 전용 기능은 배포 대상에서 제외한다. 해당 기능이 만든 에셋은 게임에서 사용하는 경우에만 포함한다.

# 배포 게임 렌더링

- 빌드된 게임(Release와 Native AOT)은 BRUTAL의 SDL3 렌더러를 사용한다. 게임 런타임에서 SkiaSharp를 사용하거나 배포본에 SkiaSharp 관리 어셈블리와 네이티브 라이브러리를 포함하지 않는다.
- SkiaSharp는 에디터와 빌드 도구에서 미리보기, 셰이더 시트 사전 생성에 사용할 수 있다. 게임에서는 미리 생성한 에셋을 SDL3로 그린다. 게임 텍스트는 글리프 시트를 굽지 않고 BRUTAL의 SDL_ttf(`Brutal.Sdl.Ttf`)로 배포한 TTF·OTF에서 실행 중에 그린다. 에디터의 Scene Play도 같은 경로를 쓴다.

# BRUTAL 우선 사용

- 엔진 기능을 구현하거나 라이브러리를 추가하기 전에 [BRUTAL 기능 정리](docs/brutal.md)를 확인한다. BRUTAL에 해당 기능이 있으면 다른 NuGet 패키지나 직접 구현보다 BRUTAL을 먼저 사용한다.
- BRUTAL로 부족해 다른 방법을 쓸 때는 그 이유를 커밋이나 문서에 남긴다. 런타임 경계와 배포 렌더링 규칙은 위 항목을 그대로 따른다.

# 리플렉션 대신 C# 소스 생성기

- 빌드 패키징과 성능을 위해 리플렉션보다 C# Source Generator를 적극 사용한다. 런타임 리플렉션은 Native AOT trim에서 깨지기 쉽고, 메타데이터를 남겨 배포 용량을 키우며, 시작 시간과 프레임 비용을 늘린다.
- 런타임과 게임 스크립트에서 타입 검색, 컴포넌트 등록, 필드 직렬화·바인딩, 이벤트·메시지 연결처럼 리플렉션으로 하던 일은 빌드 때 코드를 생성해 처리한다.
- .NET 기본 생성기를 먼저 쓴다: JSON은 `JsonSerializerContext`, 정규식은 `[GeneratedRegex]`, 네이티브 호출은 `[LibraryImport]`. 직접 만들 때는 `IIncrementalGenerator`를 사용한다.
- 생성기 프로젝트는 빌드 도구로 보고 배포본에 넣지 않는다. 런타임 프로젝트는 `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`로만 참조한다.
- 리플렉션은 에디터와 빌드 도구에서만 쓴다. 런타임에서 피할 수 없으면 `[DynamicallyAccessedMembers]` 같은 trim 주석을 달고, Native AOT 빌드에 trim·AOT 경고가 새로 생기지 않는지 확인한다.

# 로컬 테스트

- 개발 중 회귀 검사는 이 Mac과 기존 Windows·Linux VM에서 실행한다. [로컬 테스트 명령](docs/manual/build.md#로컬-회귀-검사)을 따른다.
- 에이전트의 워크트리 준비, GUI 잠금, 하네스·결과 기록, 병렬 작업 규칙은 [에이전트 작업 흐름](docs/manual/agent-workflow.md)을 따른다.

# 검증용 도구 설치

- 이 Mac에 QEMU 같은 검증용 도구를 새로 설치할 때는 도구 이름과 필요한 이유를 먼저 알리고, 사용자가 명시적으로 승인한 뒤에 설치한다. 검증을 진행해 달라는 요청만으로는 설치에 동의한 것으로 보지 않는다.

# 플랫폼 오류를 재현할 환경

먼저 오류가 난 대상을 기록한다: 에디터의 Scene 편집, 에디터 안의 Play, 독립 SDL 창(Save & Run), 또는 내보낸 게임(Release / Native AOT). 같은 프로젝트라도 실행 경로가 다르다. 명령은 저장소 루트 기준이다.

| 대상 | 엔진·에디터를 살필 환경 | 빌드한 게임을 살필 환경 |
| --- | --- | --- |
| [macOS arm64](docs/troubleshooting/macos.md) | 이 Apple Silicon Mac의 `editor/MoNoon.app`. Scene은 패치한 SDL 3.4.12 + Cocoa NSView를 사용한다. | 같은 Mac에서 `game-builds/KeyRoom/KeyRoom.app`과 `NativeAOT/KeyRoom.app`을 구분한다. 둘 다 플레이 기록이 있다. |
| [Windows x64](docs/troubleshooting/windows.md) | 보존한 Windows VM에서 패키지의 `editor\MoNoon.exe`로 KeyRoom을 열어 Avalonia Scene 편집·Play를 재현한다. | 보존한 QEMU Windows 11 Enterprise LTSC x64에서 `KeyRoom-win-x64/MoNoonPlayer.exe` (Release). 시작 오류는 배포 폴더의 SDL3.dll부터 확인한다. |
| [Linux x64](docs/troubleshooting/linux.md) | 에디터는 Linux를 지원하지 않는다(사용자 결정, 2026-11-23). Linux는 게임 실행 대상과 CI(헤드리스 빌드·리플레이)만 | 보존한 QEMU Debian 12 amd64의 X11/Xvfb에서 `KeyRoom-linux-x64/MoNoonPlayer` (Release). 창 초기화와 HUD 텍스트 오류를 구분한다. |

- x64 게스트는 이 Mac의 `.test-env/`를 재사용한다. 가상 디스크·기반 이미지·키를 삭제하거나 저장소에 추가하지 않는다. [재실행 명령과 합격 기준](docs/troubleshooting/README.md)을 따른다.
- InventoryLab의 Windows·Linux x64 회귀는 각 게스트에서 Release와 Native AOT를 직접 빌드하고 같은 입력을 헤드리스로 재생한다. [게스트 실행기](engine/tests/InventoryLab.Replay/README.md) · [네 배포본의 기준 저장 비교](engine/tests/results/input-replay-x64/README.md)
- 보고서에는 OS/아키텍처, 실행 대상과 빌드 종류, 첫 예외, 재현 입력, 로그 또는 캡처를 남긴다. 게임의 게스트 실행 결과를 같은 OS의 에디터·Native AOT·물리 GPU 검증으로 간주하지 않는다.
