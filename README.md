# MoNoon Engine · BRUTAL 기반 2D 게임 엔진

**MoNoon**은 Mac에서 직접 열어 2D 게임을 만드는 데스크톱 에디터다. 프로젝트 폴더를 열고, 장면에 오브젝트를 배치하고, Inspector에서 속성을 바꾼 뒤 Play로 테스트할 수 있다. `KeyRoom`은 이 엔진으로 여는 예제 게임 프로젝트다. Windows x64와 Linux x64도 이식 대상에 포함한다.

[사용자 매뉴얼](docs/manual/README.md) · [타일맵과 타일셋 사용법](docs/manual/tilemaps.md)

## 폴더 안내

| 하려는 일 | 위치 |
| --- | --- |
| 엔진 개발 | [engine/](engine/) — [엔진 솔루션](engine/MoNoonEngine.slnx), 소스, 네이티브 라이브러리, 빌드 스크립트, 테스트 |
| 에디터 실행 | [editor/MoNoon.app](editor/MoNoon.app) — Apple Silicon Mac에서 더블클릭 |
| 게임 빌드용 플레이어 템플릿 | [player-templates/](player-templates/) — `osx-arm64`, `win-x64`, `linux-x64` 중 내보낼 플랫폼의 폴더를 사용 |
| 샘플 편집 | [samples/KeyRoom/](samples/KeyRoom/) — 에디터에서 열 장면, 에셋, C# 스크립트 |
| 선택형 인벤토리 | [InventoryLab](samples/InventoryLab/)에서 아이템을 담고 사용하거나, [패키지](packages/Inventory.monopackage)를 다른 프로젝트로 가져온다. [사용법](docs/manual/inventory.md) |
| 선택형 퀘스트 | InventoryLab에서 약초 수집 의뢰를 받고 보상을 수령한다. [퀘스트 패키지](packages/Quests.monopackage) · [사용법](docs/manual/quests.md) |
| 선택형 대화 | InventoryLab의 약초상 대사와 선택지를 편집하고 대화로 의뢰와 보상을 받는다. [대화 패키지](packages/Dialogue.monopackage) · [사용법](docs/manual/dialogue.md) |
| 선택형 덱 | [CardLab](samples/CardLab/)에서 여정 카드를 뽑고 사용한다. [덱 패키지](packages/Decks.monopackage) · [사용법](docs/manual/decks.md) |
| 대량 탄막과 드로우콜 비교 | [BarrageLab](samples/BarrageLab/)에서 탄환 회수·재사용과 묶음 제출의 차이를 확인한다. [사용법](docs/manual/sprite-pools.md) |
| 음악 박자와 입력 비교 | [RhythmLab](samples/RhythmLab/)에서 Space를 눌러 입력의 빠름·정확·늦음과 밀리초 오차를 확인한다. [사용법](docs/manual/rhythm.md) |
| 절차 생성 샘플 실행 | [SeedLab](samples/SeedLab/) — 시드로 미로를 만들고 한 턴씩 이동한 경로를 재생한다. [미로 생성](docs/manual/generation.md) · [턴과 리플레이](docs/manual/turn-replay.md) |
| 플랫포머 물리 샘플 실행 | [PlatformerLab](samples/PlatformerLab/) — 경사 걷기, [적 순찰과 추적](docs/manual/navigation.md), 이동 발판·거리 조인트, [오토타일과 청크 스트리밍](docs/manual/tilemaps.md#오토타일-브러시). [조작법](docs/manual/platformer-physics.md) |
| UI·접근성 샘플 실행 | [samples/AccessLab/](samples/AccessLab/) — 한글 이름 입력, 다국어 설정, 스크롤 및 패드 탐색. [조작법](docs/manual/game-ui.md) |
| 오디오 샘플 실행 | [samples/AudioLab/](samples/AudioLab/) — 루프 음악과 비콘으로 버스·페이드·공간 음향을 확인한다. [조작법](docs/manual/audio.md) |
| 카메라 샘플 실행 | [samples/CameraLab/](samples/CameraLab/) — 분할 화면과 미니맵에서 두 플레이어를 추적한다. [설정과 조작](docs/manual/cameras.md) |
| 충돌 샘플 실행 | [samples/CollisionLab/](samples/CollisionLab/) — → / D로 센서·게이트를 지나 벽에 멈춘다. [콜라이더·질의 사용법](docs/manual/collision.md) |
| 애니메이션 샘플 편집 | [samples/AnimatorLab/](samples/AnimatorLab/) — 파라미터로 상태와 클립을 전환하고 HUD에서 프레임 이벤트 확인. [조작법](docs/manual/sprites.md#파라미터로-애니메이션-전환하기) |
| 스프라이트 샘플 편집 | [samples/SpriteLab/](samples/SpriteLab/) — 원본과 마스크·반전·색상을 적용한 정적/애니메이션 스프라이트 비교. [아틀라스 사용법](docs/manual/sprites.md) |
| 타일맵 샘플 편집 | [samples/KeyRoomTiles/](samples/KeyRoomTiles/) — 바닥과 충돌 벽 레이어를 칠하는 KeyRoom 프로젝트. [타일맵 사용법](docs/design/tilemap-editor.md) · [배포 게임](game-builds/KeyRoomTiles/) |
| 빌드한 게임 실행·배포 | [game-builds/KeyRoom/](game-builds/KeyRoom/) — Mac은 `KeyRoom.app`, Windows와 Linux는 플랫폼별 폴더 전체 |
| 오류 해결·다음 기능 설계 | [플랫폼별 실행 및 오류 해결](docs/troubleshooting/README.md) · [스프라이트 애니메이션 에디터](docs/design/sprite-animation-editor.md) · [타일맵 에디터](docs/design/tilemap-editor.md) · [인게임 UI](docs/design/in-game-ui.md) |

엔진을 다시 빌드하면 중간 파일은 `engine/.artifacts/`에 생성된다. 이전 플랫폼 검증 기록은 [engine/tests/results/](engine/tests/results/)에서 볼 수 있다.

## 에디터 실행

Apple Silicon Mac에서는 `editor/MoNoon.app`을 더블클릭한다. 빌드에는 .NET 런타임이 들어 있다. 소스에서 실행하거나 Mac 앱을 다시 빌드하려면 프로젝트 루트에서 다음 명령을 사용한다.

```sh
dotnet run --project engine/src/Engine.Editor
./engine/scripts/build-editor-mac.sh
```

Apple Silicon에서 소스 빌드에는 Xcode Command Line Tools와 CMake 3.24 이상이 필요하다. 첫 빌드는 공식 SDL 3.4.12 소스의 SHA-256을 확인하고 Cocoa 패치를 적용한다. 생성한 arm64 라이브러리는 에디터 빌드에 포함된다. [SDL 3.4.12](https://github.com/libsdl-org/SDL/releases/tag/release-3.4.12) · [Cocoa 패치 PR #16658](https://github.com/libsdl-org/SDL/pull/16658)

1. 저장소 안에서 실행하면 `samples/KeyRoom` 프로젝트가 열린다. 새 게임을 시작하려면 **File → New Project…**로 빈 폴더를 선택한다. 다음에 **File → Open Project…**로 같은 폴더를 열 수 있다.
2. **Hierarchy**의 **+**로 기본 **Square** 스프라이트가 지정된 오브젝트를 추가한다. **Scene**에서 드래그하거나 **Inspector**에서 위치, 크기, 이름과 색을 편집한다. 색상은 **Sprite Renderer → Color**에서 편집한다. KeyRoom에서는 **Inspector → Add Component → Project scripts → PlayerMovement**로 방향키 이동을 붙인다. 이미 붙어 있는 종류는 메뉴에 **(Added)**로 표시되며, Inspector에서 해당 컴포넌트 제목 오른쪽의 **⚙ → Remove**로 뗄 수 있다.
3. **Play**로 현재 장면을 실행하고 방향키로 조작한다. **Stop**이나 Esc를 누르면 편집 좌표로 돌아간다.
4. **File → Save Scene** 또는 ⌘S / Ctrl+S로 프로젝트의 `Scenes/*.scene`을 저장한다. **Project → + → New Scene**으로 장면을 추가한다. 탐색기에서 다른 `.scene` 파일을 더블클릭하면 장면이 전환된다. **Run → Save & Run**은 현재 장면을 저장하고 별도의 BRUTAL SDL 게임 창에서 실행한다.

**Project**는 현재 프로젝트의 실제 폴더를 한 단계씩 보여준다. 폴더를 더블클릭해 들어가고 **↑**로 상위 폴더로 돌아간다. **Add Component**의 **Built-in components**에는 엔진 내장 컴포넌트가, **Project scripts**에는 프로젝트에서 만든 C# 컴포넌트가 표시된다. KeyRoom의 `Scripts` 폴더에는 방향키 이동용 `PlayerMovement.cs`와 열쇠 수집과 시점 전환용 `KeySequence.cs`가 있다. **Project → + → New C# Script…**로 새 컴포넌트를 만든다. 오브젝트에 바로 붙이려면 **Add Component → New C# Component…**에서 클래스 이름을 입력하고 **Create & Add**를 누른다. `Scripts`의 하위 폴더를 보고 있으면 새 스크립트는 그 안에 생성된다. 다른 위치에서는 `Scripts`에 생성된다. Hierarchy에서 오브젝트를 선택하고 목록의 `.cs` 파일을 오른쪽 **Inspector**에 드래그하면 컴포넌트가 붙는다. 같은 파일을 같은 오브젝트에 다시 놓아도 한 번만 붙는다. 목록에서 파일을 더블클릭하거나 컴포넌트 제목 오른쪽의 **⚙ → Edit Script**를 누르면 설정한 외부 에디터에서 해당 파일이 열린다. macOS의 **MoNoon → Preferences…**(Windows·Linux는 **Edit → Preferences…**)에서 **External script editor**로 앱을 지정할 수 있고, 비워 두면 운영체제의 기본 앱을 사용한다. 외부에서 파일을 저장한 뒤 MoNoon로 돌아오면 Inspector가 필드를 다시 읽고 다음 Play에 변경된 코드가 적용된다. 오브젝트별 속성값과 부착 상태는 **File → Save Scene**으로 저장한다. 프로젝트를 복사할 때는 `Scripts/`도 포함한다.

`Scripts/`의 컴포넌트와 공용 C# 파일은 한 프로젝트로 컴파일된다. **Project → + → New Shared C# File…**로 공용 파일을 만든다. [스크립팅 매뉴얼](docs/manual/scripting.md)에서 생명주기 콜백, 타입이 지정된 Inspector 참조, 이름이 겹치는 기존 스크립트의 이전 방법을 확인할 수 있다. Inspector는 상수로 초기화하거나 초기값을 생략한 공개 인스턴스 필드(`int`, `float`, `double`, `bool`, `string`, 오브젝트 참조용 `Guid`)를 표시한다. 새 스크립트는 빈 `GameBehaviour` 클래스로 생성한다. `FixedUpdate(ScriptTick tick)`의 `tick.Move(...)`는 씬의 충돌과 계층 이동을 따르고, 트리거에 붙인 스크립트는 `OnTriggerEnter(ScriptContact contact)`에서 상대 ID를 읽을 수 있다. Play를 시작할 때마다 오브젝트별로 새 스크립트 인스턴스가 생성된다.

**Hierarchy**에서 항목을 우클릭해 **Create Empty Child** 또는 **Add Child Square**를 고르면 해당 항목 아래에 자식이 생성된다. 빈 공간의 우클릭 메뉴에서는 최상위 오브젝트를 추가할 수 있다. 기존 오브젝트는 새 부모 위로 드래그하면 자식이 되고, 빈 공간에 놓으면 최상위로 돌아간다. 부모를 바꿔도 화면 위치가 유지되며 Inspector의 **Local pos.**는 부모 기준 좌표를 표시한다. Hierarchy나 Scene에서 선택한 오브젝트는 맥에서 **⌘Delete**, Windows·Linux에서 **Delete**로 삭제한다. 부모를 삭제하면 직계 자식들은 한 단계 위로 올라간다. Inspector에서 컴포넌트 헤더를 누르면 해당 필드가 접히거나 펼쳐진다.

**Hierarchy → 우클릭 → Duplicate** 또는 **⌘D**(Windows·Linux는 **Ctrl+D**)로 선택한 오브젝트와 모든 자식을 같은 부모 아래에 복제한다. 복제본은 원본과 같은 위치에 생긴다. 스크립트의 오브젝트 참조는 복제한 계층 안에서는 새 오브젝트를, 계층 밖에서는 기존 오브젝트를 가리킨다. **⌘Z** 한 번으로 복제 전체를 되돌린다. 카메라가 포함된 계층도 복제할 수 있으며 시작 카메라 지정은 원본에 유지된다.

**Scene**에서 빈 공간을 드래그하면 편집 시점이 이동하고, 스크롤하면 포인터 위치를 중심으로 확대·축소한다. 음수 좌표와 카메라 프레임 밖에도 오브젝트를 배치할 수 있다. **Scene → Frame Camera**로 게임 카메라 위치를 다시 볼 수 있다.

**Project → Game Resolution…**에서 게임의 가로·세로 픽셀 수를 설정한다. 프로젝트 루트의 `GameSettings`에 저장하며 모든 장면의 Play와 독립 실행에 적용된다. 각 장면의 **Main Camera**는 Transform 위치를 화면 중심으로 사용한다. **Camera 2D → Zoom**을 올리면 더 좁은 영역이 확대된다. 카메라를 Player의 자식으로 두면 이동을 따라간다. 카메라를 삭제한 경우 **Scene → Create Camera**로 다시 만들 수 있다.

장면 형식 v7은 기존 v6 HUD를 UI Rect·Image·Text로 읽는다. v6부터는 시작 카메라 ID를 씬에 저장하고, 스프라이트 종류와 사각형 색상을 Sprite Renderer에 저장한다. v1~v4 장면을 열면 예전 Rectangle과 PNG가 Sprite Renderer로 합쳐지고, 붙어 있던 Arrow Movement와 Pickup은 프로젝트 스크립트로 전환된다. 장면을 저장할 때 필요한 소스가 `Scripts/`에 생성된다. v1~v3 장면에는 기존 화면 중심 (400, 225)에 카메라도 추가된다.

벽 충돌은 Player와 벽을 각각 선택해 **Inspector → Add Component → Box Collider 2D**를 붙여 설정한다. 충돌 범위는 각 오브젝트의 Transform 위치와 크기를 따른다. **Is Trigger**를 켜면 이동 중 상자가 겹칠 때 진입 이벤트가 발생하고 물체가 통과한다. KeyRoom에서 오른쪽으로 가면 잠긴 문에 막힌다. 왼쪽의 노란 열쇠를 주운 뒤 오른쪽으로 돌아가면 열린 문을 통과한다. 다시 Play하면 열쇠와 문이 처음 상태로 돌아온다.

기존 오브젝트에 이미지를 입히려면 **Add Component → Sprite Renderer**에서 **Sprite → Choose PNG…**를 사용한다. 오브젝트의 위치와 크기를 유지하고 PNG의 투명도를 적용한다. 새 스프라이트는 **Project → + → Import Assets…**로 가져온 파일을 Scene으로 드래그하거나 파일 관리자에서 PNG 한 개를 Scene에 드롭해 배치한다. 드롭한 지점이 이미지의 중심이 되며 원본 픽셀 크기로 배치된다.

## 에디터 없이 게임 빌드하기

저장소 루트에서 `./monoon ci`를 실행하면 InventoryLab의 Release와 Native AOT 빌드를 만들고 약초상 대화·보상·저장 복원을 검사한다. 빌드된 두 플레이어에서 약초 의뢰 입력을 창 없이 다시 재생하려면 `./monoon test replay game-builds/InventoryLab`을 쓴다. 전체 엔진 회귀 검사는 `./monoon test`로 실행한다. [개별 빌드 명령과 템플릿 준비](docs/manual/build.md#터미널에서-빌드와-회귀-검사) · [Mac 검증 결과](engine/tests/results/build-cli/README.md)

개발용은 `./monoon build samples/InventoryLab release --profile development`로 만든다. 배포용은 `--profile distribution`이 기본이며 디버그 심볼을 빌드 ID별로 앱 밖에 보관한다. 두 프로필 모두 `native-aot`를 선택할 수 있다. [프로필별 경로와 심볼 사용법](docs/manual/build.md#개발용배포용-프로필과-디버그-심볼)

## 샘플 게임 실행

빌드한 게임은 [game-builds/KeyRoom/KeyRoom.app](game-builds/KeyRoom/KeyRoom.app)을 열면 실행된다. 샘플 소스에서 직접 실행하려면 .NET 10 SDK가 설치된 환경에서 저장소 루트에서 다음 명령을 사용한다.

```sh
dotnet run --project samples/KeyRoom
```

`KeyRoom` 창에서 방향키로 민트색 사각형을 움직인다. 두 방향키를 함께 누르면 같은 속도로 대각선으로 이동하며, 벽에 부딪히면 멈춘다. 창 크기를 바꿔도 프로젝트에서 정한 해상도(기본 800×450)의 비율을 유지한다. Esc 또는 창의 닫기 버튼으로 종료한다.

## 기술 선택

| 영역 | 첫 버전의 선택 | 이유 |
| --- | --- | --- |
| 언어와 배포 | C# / .NET 10, BRUTAL 공식 NuGet 패키지 | 엔진을 독립된 솔루션으로 만들고 검증한 패키지 버전을 고정한다. |
| 에디터 UI | Avalonia Desktop | Hierarchy, Inspector, 프로젝트 파일 선택기와 Scene 뷰를 구성한다. |
| 게임 창·입력·그리기 | `Ahwoo.Brutal.Sdl`의 창, 이벤트, 2D 렌더러 | 에디터의 Save & Run과 독립 게임에서 사용한다. |
| 콘텐츠 | BRUTAL을 통한 SDL 3.4 PNG 로딩, 씬·프리팹·설정은 VYaml | PNG를 렌더러의 텍스처로 올리고 같은 파일을 참조하는 오브젝트끼리 공유한다. |
| 충돌 | 축에 평행한 사각형끼리의 이동 제한과 겹침 판정 | 벽은 이동을 막고 열쇠는 접촉만 감지한다. 회전 강체나 관절이 필요한 게임이 정해지면 `Brutal.Box2D` 도입을 검토한다. |

BRUTAL은 네이티브 바인딩과 기반 라이브러리를 제공한다. 공식 SDL 샘플에는 창을 열고 이벤트를 받아 사각형을 그리는 코드가 있다. MoNoon Engine이 게임 루프, 씬 형식, 스프라이트의 그리기 순서와 스크립트 API를 제공한다. [BRUTAL 저장소](https://github.com/ahwootech/BRUTAL) · [HelloSdl 소스](https://github.com/ahwootech/BRUTAL/blob/main/samples/Hello/HelloSdl.cs)

## 코드 구조와 소유권

| 프로젝트 | 맡는 일 |
| --- | --- |
| `engine/src/Engine.Editor` | 프로젝트·장면 관리, 오브젝트 선택·드래그, Inspector와 Play 모드를 제공한다. 편집 장면은 YAML로 저장한다. |
| `engine/src/Engine.Core` | 장면과 엔티티, 게임 좌표, 카메라, 입력 액션, 충돌을 관리한다. 현재 장면에서 프레임에 그릴 스프라이트 목록을 만든다. 창 없이 실행할 수 있다. |
| `engine/src/Engine.Player` | 빌드에 포함된 시작 씬과 컴파일된 스크립트를 읽고 SDL 게임 창을 연다. |
| `engine/src/Engine.Desktop` | 메인 스레드에서 창과 게임 루프를 운영한다. SDL 이벤트를 입력 액션으로 바꾸고, PNG를 텍스처로 올리고, 그리기 목록을 화면에 출력한다. 네이티브 핸들은 여기서 생성하고 해제한다. |
| `samples/KeyRoom` | 데스크톱 호스트와 게임을 연결한다. 샘플 장면에서 열쇠의 수집자와 열리는 문을 지정한다. |

`Engine.Core`의 `SceneGame`이 Play와 독립 게임의 이동 및 그리기를 처리한다. Apple Silicon에서는 Avalonia의 `NativeControlHost`가 Scene 패널에 자식 `NSView`를 만들고 BRUTAL의 SDL Metal 렌더러가 그 안에 직접 그린다. 편집 격자와 선택 테두리도 같은 렌더러로 그린다. 마우스 좌표는 패널 기준으로 받아 월드 좌표로 변환하고, 출력은 Retina 배율에 따라 조절한다. Windows와 Linux 에디터에서는 Avalonia가 장면을 그린다.

## 한 프레임의 동작

1. **입력을 모은다.** 키보드·마우스·패드를 프로젝트 액션에 대응시켜 이동 방향과 한 번 누른 액션을 구분한다. 한 번 누른 액션은 다음 시뮬레이션 틱에서 한 번만 소비한다.
2. **게임 시간을 진행한다.** 누적한 시간만큼 1/60초 간격으로 게임 로직을 실행한다. 각 틱에서 원하는 이동량을 계산하고, 벽까지 이동한 뒤 트리거 접촉을 게임에 전달한다. 열쇠 제거와 문 상태 변경은 틱 끝에 반영한다. 창이 포커스를 잃으면 눌린 키와 누적 시간을 초기화한다.
3. **화면을 그린다.** 이전·현재 위치를 보간하고 카메라를 적용한 뒤 배경, 사물, 전경 순으로 그린다. 같은 레이어의 그리기 순서는 장면에 기록한다. 창 크기가 바뀌면 논리 해상도의 종횡비를 유지하고 남는 영역에 여백을 둔다.

월드 좌표는 오른쪽이 +X, 아래가 +Y이며 카메라 줌이 1일 때 1단위는 논리 화면의 1픽셀이다. 물체의 위치와 충돌 상자는 월드 좌표로 보관한다. 창 크기와 Retina 배율에 따른 변환은 출력 단계에서 한 번만 적용한다.

## 장면과 에셋

씬은 `schemaVersion`, `defaultCameraId`, 오브젝트 목록을 저장한다. KeyRoom의 Key 트리거는 Player와 닿으면 문을 열고 Door Camera를 1초간 선택한다. Exit 트리거는 두 번째 방을 요청한다. 두 방의 HUD는 Play 세션의 `key` 값을 읽는다.

## 편집과 게임 빌드

- **Scene → Create Camera**로 시점을 추가하고 **Camera 2D → Set as start camera**로 시작 시점을 지정한다. **Frame Camera**는 선택한 카메라에 맞춘다. 스크립트의 `tick.Cameras.TrySetActive(id)`와 `contact.Cameras.TrySetActive(id)`는 고정 틱 끝에 시점을 바꾼다.
- **Project → Input Actions…**에서 [프로젝트 입력 액션](docs/manual/input-actions.md)을 추가하고 키보드·마우스·패드를 연결한다. KeyRoom의 안내는 H·마우스 오른쪽·패드 Start로 열 수 있다. **Project → Console…**에서 컴파일 오류를 더블클릭하면 외부 에디터가 열린다. VS Code와 Cursor 실행 파일은 오류 줄까지 이동한다.
- Hierarchy에서 오브젝트를 Project의 파일 목록에 끌면 현재 폴더에 프리팹이 생긴다. `.prefab`을 Scene에 끌어 여러 번 배치할 수 있다. 인스턴스 루트의 우클릭 메뉴 **Apply to Prefab**은 수정한 하위 트리를 원본에 반영한다. 각 인스턴스의 위치와 내부 참조는 유지된다.
- 에디터와 배포 게임은 같은 YAML 형식과 VYaml 로더를 사용한다. 씬은 `.scene`, 프리팹은 `.prefab`, 애니메이션은 `.clip`로 저장한다. `.meta` 확장자는 유지하며 내용은 `id: <GUID>`인 YAML이다.
- 기존 프로젝트 폴더를 에디터에서 열면 `Door.clip.yaml`은 `Door.clip`으로, 짝이 되는 `.meta`와 에셋 목록의 경로도 함께 바뀐다. 씬과 프리팹의 GUID 참조는 유지한다. 예전 JSON 프로젝트도 같은 이름 규칙의 YAML로 변환한다. 같은 이름의 YAML이 이미 있고 내용이 다르면 원본을 남기고 중단한다. 터미널에서는 `editor/MoNoon.app/Contents/MacOS/MoNoon --migrate-project <프로젝트 폴더>`로 같은 작업을 할 수 있다.
- 프로젝트를 복사할 때는 `.monoon-assets`과 각 에셋의 `.meta`도 포함한다. Finder에서 에셋을 옮길 때 `.meta`를 함께 옮기면 이름과 내용을 바꿔도 ID가 유지된다. 에셋만 옮긴 경우 기존 파일과 내용이 일치하는 고유한 후보를 찾아 연결한다.
- **Sprite Renderer → Layer / Order**로 그리기 순서를 지정한다. **Project → + → New Animation Clip…**에서 클립을 만들거나 Project에서 `Assets/Door.clip`을 더블클릭한다. 타임라인에서 썸네일을 끌어 순서를 바꾸고 **Seconds**에서 선택 프레임의 시간을 정한다. **Add → Sheet cells…**에서 PNG의 셀 크기·여백·간격을 정하고 원하는 셀을 클릭한 뒤 **Insert cells**로 추가한다. KeyRoom의 문 시트는 투명 여백을 잘라 48×96픽셀에 담았으며, **Frames**에서 네 프레임을 고를 수 있다. 위쪽 눈금을 드래그하면 해당 시각을 미리 볼 수 있다. **Save Clip**(⌘/Ctrl+S)으로 저장한 뒤 씬을 Play하면 **Sprite Animator**가 같은 순서와 시간으로 재생한다. [Mac 검증 기록](engine/tests/results/clip-timeline/macos-arm64/README.md) **Audio Source**는 프로젝트의 WAV를 선택한다. `contact.PlayEffect(objectId)`가 해당 오브젝트의 애니메이션과 소리를 시작한다. 인게임 UI는 **Hierarchy 우클릭 → UI**에서 Canvas와 Image·Text·Button으로 만든다. 앵커와 Offset으로 게임 화면에 배치한다. KeyRoom의 **Controls**를 클릭하면 조작 안내가 열리고 다시 클릭하면 닫힌다. [설계와 사용법](docs/design/in-game-ui.md)
- **Build → Build Game…**에서 시작 씬, 포함할 씬, 플랫폼을 선택한다. **Build**를 누르면 KeyRoom은 `game-builds/KeyRoom/`에, 저장소 밖의 프로젝트는 프로젝트 폴더와 나란히 있는 `game-builds/<프로젝트 폴더명>/`에 빌드된다. **Build To…**로 다른 출력 위치를 고를 수 있다.
- 배포용 플레이어는 `dotnet publish -c Release --self-contained true`로 만들고, 장면에서 쓰는 C# 스크립트도 Release 최적화로 컴파일한다. 게임에는 해당 플랫폼의 .NET 런타임, 컴파일된 스크립트와 콘텐츠가 들어간다.
- **Build type → Native AOT**를 선택하면 현재 컴퓨터와 같은 OS·아키텍처용 게임을 출력 폴더의 `NativeAOT/`에 만든다. 기본 **Release**는 기존 경로를 사용한다. Native AOT 빌드에는 .NET 10 SDK와 플랫폼용 네이티브 링커(이 Mac에서는 Xcode Command Line Tools)가 필요하다. 첫 빌드에서 SDK가 대상용 NuGet 패키지를 복원한다.
- AOT 빌드는 `player-templates/native-aot/`의 프로젝트에서 씬·에셋·설정용 VYaml 직렬화 코드를 생성하고, 포함한 장면에서 사용하는 스크립트와 Inspector 필드 연결 코드를 플레이어 실행 파일에 함께 컴파일한다. 소스에서 AOT 키트만 갱신할 때는 `./engine/scripts/build-native-aot-kit.sh`를 실행한다. Mac 터미널에서도 `editor/MoNoon.app/Contents/MacOS/MoNoon --build-native-aot samples/KeyRoom game-builds/KeyRoom`으로 같은 빌드를 실행할 수 있다.
- 편집과 Play에는 에디터만 필요하다. 게임을 내보낼 때는 저장소 루트의 `player-templates/<플랫폼>/`을 사용한다. 앱을 다른 곳으로 옮길 때는 필요한 플랫폼을 담은 `player-templates` 폴더를 앱 옆에 두면 된다. 템플릿을 따로 보관한다면 `MONOON_PLAYERS_DIR`에 해당 상위 폴더의 절대 경로를 지정해 에디터를 실행한다.
- 에디터와 템플릿은 독립적으로 빌드한다. 저장소 루트에서 `./engine/scripts/build-players.sh osx-arm64`를 실행하면 Mac 템플릿만 생성하고, 인수를 생략하면 세 플랫폼을 모두 생성한다.

## 구현 순서와 완료 기준

| 단계 | 구현 | 직접 확인할 결과 |
| --- | --- | --- |
| 1. Mac에서 첫 화면 | 별도 솔루션과 데스크톱 호스트, 키 입력, 사각형 출력 | Apple Silicon Mac에서 방향키로 사각형을 움직인다. 창을 조절해도 비율이 유지되고 닫기 버튼으로 종료된다. |
| 1b. 독립 에디터 | 프로젝트 생성·열기, 사각형 장면 편집, 저장과 재생 | 장면에 추가한 오브젝트가 다시 열어도 보인다. Play에서 움직인 뒤 Stop을 누르면 편집한 위치로 돌아온다. |
| 2. 데이터로 만든 장면 | VYaml 로더, PNG 스프라이트, 카메라와 레이어 | YAML에서 물체의 위치를 바꾸고 재실행하면 새 위치에 그려진다. 카메라가 캐릭터를 따라가고 배경은 뒤에 남는다. |
| 3. Mac v0.1 | 고정 틱 이동, 벽·트리거 판정, 열쇠와 문, 재시작 | 열쇠를 줍기 전에는 문이 막히고, 주운 뒤에는 통과한다. 재시작하면 열쇠와 문이 원래 상태로 돌아온다. 30Hz와 120Hz로 그려도 같은 틱 입력의 최종 위치가 같다. |
| 4. 세 OS 검증 | `osx-arm64`, `win-x64`, `linux-x64`용 앱과 콘텐츠 패키징 | 각 OS의 데스크톱에서 배포본을 직접 열어 같은 방을 끝까지 플레이한다. 창 리사이즈, 키 포커스 복귀, PNG 경로와 네이티브 라이브러리 로딩을 확인한다. |

**1단계 검증 결과**

- Apple Silicon Mac과 .NET SDK 10.0.101에서 공식 `HelloSdl.cs`의 창과 사각형 출력을 확인했다. 같은 Mac에서 `KeyRoom`의 네 방향 이동, 창 확대 후 종횡비 유지, 닫기 버튼 종료를 확인했다. [공식 샘플 실행 안내](https://github.com/ahwootech/BRUTAL/blob/main/samples/README.md)
- 이 Mac에 보존한 QEMU Windows·Linux x64 환경으로 현재 YAML 게임을 다시 검사하는 방법: [KeyRoom 데스크톱 재검증](engine/tests/KeyRoom.Desktop/README.md). 가상 디스크와 전용 자격 증명은 로컬 `.test-env/`에만 둔다. UI v7 도입 전 YAML 배포본의 실제 게임 창 캡처와 게스트에서 계산한 해시: [Windows x64](engine/tests/results/keyroom-desktop/yaml-windows-x64/) · [Linux x64](engine/tests/results/keyroom-desktop/yaml-linux-x64/).
- Debian 12.15 x86_64 게스트의 X11 가상 화면에서 Linux 배포본을 두 번 실행했다. 방향키와 WASD 각각으로 열쇠를 얻고 두 번째 방에 들어가 상하로 이동했다. 960×432 창에서 게임은 768×432, 640×600 창에서 게임은 640×360으로 표시됐다.
- Apple Silicon Mac의 QEMU에서 Windows 11 Enterprise LTSC 24H2 x64를 부팅해 Windows 배포본을 검증했다. 방향키와 WASD 각각으로 열쇠를 얻고 두 번째 방에 들어가 상하로 이동했다. 960×432 창은 좌우 96px, 640×600 창은 상하 120px에 검은 여백이 생겨 게임 영역이 16:9를 유지했다. Pillow가 설치된 Windows x64 데스크톱에서 `python engine/tests/KeyRoom.Desktop/verify.py game-builds/KeyRoom/KeyRoom-win-x64 results`로 재현할 수 있다.

Windows x64 빌드는 BRUTAL 바인딩과 SDL 공식 3.4.12 DLL을 함께 배포한다. 공식 DLL은 Windows 시스템 라이브러리만 사용하므로 Visual C++ 재배포 패키지를 별도로 설치할 필요가 없다. `engine/native/windows-x64/SDL3.dll`의 SHA-256은 `2a2704678bf6c9c6a944270ab35079df76f5afe92b780394ed72d9c8218b98d8`이다. [SDL 3.4.12 Windows x64 릴리스](https://github.com/libsdl-org/SDL/releases/download/release-3.4.12/SDL3-3.4.12-win32-x64.zip)

외부 배포본에는 BRUTAL 라이선스와 고지를 포함하고, 시작 로고와 크레디트 표시를 반영한다. 함께 배포하는 패키지별 제3자 라이선스도 확인한다. [BRUTAL 라이선스 1.0–4.0항](https://github.com/ahwootech/BRUTAL/blob/main/LICENSE.txt)
