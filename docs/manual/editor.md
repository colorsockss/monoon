# 에디터와 프로젝트

[매뉴얼 목차](README.md)

## 데스크톱 배포 에디터

| 플랫폼 | 열기 |
| --- | --- |
| macOS arm64 | `MoNoon.app`을 연다. |
| Windows x64 | `MoNoon-win-x64.zip`을 풀고 `editor\MoNoon.exe`를 실행한다. |
| Linux x64 | 에디터를 지원하지 않는다. Linux는 [게임 내보내기 대상](build.md)과 CI(헤드리스 빌드·리플레이)로만 쓴다. |

Windows 에디터 패키지에는 .NET 런타임과 프로젝트의 C# 스크립트를 컴파일하는 Roslyn이 들어 있다. 게임을 내보낼 때 쓰는 `player-templates/`는 추출한 `editor/`와 같은 상위 폴더에 둘 수 있다. 별도 위치를 쓰면 `MONOON_PLAYERS_DIR`에 그 절대 경로를 지정한다. Native AOT 게임을 빌드할 때는 대상 OS의 .NET SDK와 네이티브 빌드 도구도 필요하다.

**KeyRoom으로 편집해 보기**

1. 쓰기 가능한 폴더에 `KeyRoom-sample.zip`을 푼다. **File → Open Project…**에서 `GameSettings`가 들어 있는 `KeyRoom` 폴더를 고른다. 명령줄에서는 에디터의 첫 인자로 프로젝트 폴더 경로를 넘길 수 있다.
2. **Play**를 누르고 이동하지 않은 채 **I**를 누른다. 가방에 `Quest / Find Brass key`가 보인다. **Stop**을 누른다.
3. Hierarchy에서 **Player**를 선택한다. **Inspector → Transform → Position X**를 `274`에서 `190`으로 바꾸고 Enter를 누른다. **File → Save Scene**으로 저장한 뒤 에디터를 닫고 같은 프로젝트를 다시 연다.
4. X가 `190`인지 확인하고 **Play → I**를 누른다. 플레이어가 열쇠 위에서 시작해 가방에 `Quest complete / Brass key`가 표시된다. 게임 진행을 저장하려면 가방을 닫고 **F5**를 누른다. 시작 배치를 되돌리려면 편집 모드에서 X를 `274`로 저장한다.

저장소 루트에서 배포본을 생성한다. Mac 앱은 Apple Silicon Mac에서 첫 번째 명령으로 만든다. Windows 에디터에는 Python 3과 .NET 10 SDK가 필요하며 샘플 ZIP은 Python 3만으로 만들 수 있다. 에디터 배포본은 `editor/`, 샘플 ZIP은 `samples/dist/`에 생긴다.

```sh
engine/scripts/build-editor-mac.sh
python3 engine/scripts/package-editor.py win-x64
python3 engine/scripts/package-editor.py keyroom
```

## 네 패널에서 하는 일

![MoNoon 에디터에서 KeyRoom의 Player를 선택한 화면](editor-workspace.png)

| 패널 | 조작 |
| --- | --- |
| Hierarchy | 이름을 눌러 선택하고 삼각형으로 자식을 펼친다. 이름 왼쪽 아이콘은 오브젝트의 주 컴포넌트 종류(스프라이트·충돌체·카메라·UI 등, 없으면 Transform)다. 빈 곳의 우클릭 메뉴는 루트, 항목의 우클릭 메뉴는 해당 오브젝트 아래에 항목을 만든다. 탭 아래 도구 줄의 검색 필드에 이름 일부를 입력하면 일치하는 오브젝트와 그 부모만 남고, Esc로 지운다. 도구 줄 **+**는 Square를 만든다. |
| Scene | 머리줄 왼쪽에서 이동·회전·크기·Rect 도구와 Snap을 고른다. 스프라이트를 드래그해 배치한다. 빈 영역을 드래그해 이동하고 스크롤로 포인터 주변을 확대·축소한다. 타일 편집 중에는 우클릭 드래그로 이동한다. **Scene → Frame Camera**로 카메라 프레임을 다시 찾는다. |
| Inspector | 탭 바로 아래부터 이름과 Transform을 편집한다. 자식은 **Local pos.**, UI는 **Offset**으로 표시된다. 컴포넌트 머리말 왼쪽에 종류 아이콘이 있고, 머리말을 눌러 접고 편다. 접은 상태는 컴포넌트 종류별로 사용자 설정에 기억되어 다른 오브젝트와 다음 실행에도 같다. 머리말 오른쪽 톱니바퀴 메뉴에서 **Remove**(스크립트는 **Edit Script**도)를 고른다. 여러 개를 선택하거나 Play 중이거나 프리팹 인스턴스를 선택하면 탭 아래에 한 줄짜리 작업 줄이 나타나 선택 개수, **Apply selected**, **Overrides…**를 보여 준다. 긴 선택 값(예: `Assets/Unlock.wav`)은 말줄임표로 줄고 툴팁에 전체 값이 나온다. |
| Project | 탭 아래 도구 줄에 검색 필드, 격자 보기 전환, **+**가 있다. 그 아래 경로 줄에 프로젝트 이름부터 현재 폴더까지 폴더 버튼이 이어지고, 버튼을 누르면 그 폴더로 간다. 경로가 길면 앞쪽 폴더가 `…`로 줄고 현재 폴더는 항상 보인다. 왼쪽 위쪽 화살표(**Parent folder**)는 한 단계 위로 간다. 폴더를 더블클릭해 들어가고 파일을 더블클릭하면 종류에 맞는 편집기가 열린다. 파일을 경로 줄의 폴더 버튼이나 위쪽 화살표에 놓으면 그 폴더로 옮긴다. 격자 보기에서는 PNG가 축소 이미지로, 다른 파일은 종류 아이콘으로 76px 칸에 표시된다. 보기 방식은 사용자 설정에 기억된다. Project 패널 너비가 360px 이상이면(예: **Window → Layout → Animation**의 아래쪽 탭) 왼쪽에 폴더 트리가 나타나 파일 영역과 같은 폴더를 선택한다. 검색과 **+**에서 [에셋 검색·임포트, 참조 확인, 데이터·테이블 편집](project-assets.md)을 할 수 있다. |

### Scene에서 숨기기와 잠그기

Hierarchy 행에 포인터를 올리면 오른쪽에 눈(표시)과 자물쇠(선택 잠금) 버튼이 나타난다. 켠 버튼은 포인터가 없어도 계속 보인다. 두 버튼의 자리는 항상 비워 두므로 버튼이 나타나도 이름 위치는 바뀌지 않는다.

- **눈**: 그 오브젝트와 자식을 Scene 편집 화면에서 그리지 않고 클릭으로도 고르지 않는다. 행 이름이 흐리게 표시된다.
- **자물쇠**: Scene에 계속 그리지만 Scene 클릭으로 고르지 않는다. 겹친 오브젝트 아래를 고를 때 쓴다. Hierarchy에서는 그대로 선택하고 Inspector로 편집할 수 있다.
- 두 상태는 에디터 전용이다. 씬 파일의 내용, 오브젝트의 활성 상태, Play, 내보낸 게임은 바뀌지 않는다. 씬별로 사용자 폴더의 `editor-state.json`(레이아웃 파일과 같은 폴더)에 저장되어 다음 실행에도 남는다. 프로젝트 폴더에는 아무 파일도 생기지 않으므로 다른 사용자와 공유되지 않는다.
- 숨긴 UI 위젯은 Scene의 화면 UI 미리보기에서도 빠진다. 같은 레이아웃 그룹의 다른 위젯 배치가 미리보기에서 바뀔 수 있다.

### Console

Console 패널 위쪽 도구 줄에는 **Clear**, **Collapse**, 검색 필드, 로그·경고·오류 필터 버튼이 있다. 필터 버튼의 숫자는 보관 중인 메시지(최대 1,000개)의 종류별 개수이고, 버튼을 끄면 그 종류가 목록에서 빠진다. **Collapse**를 켜면 종류·내용·소스 위치가 같은 메시지를 한 줄로 묶고 오른쪽에 개수를 표시한다. 목록에는 각 메시지의 첫 줄만 나온다. 메시지를 선택하면 아래에 전체 내용과 스택, 소스 위치가 나오며 텍스트를 선택해 복사할 수 있다. 소스 위치가 있는 메시지는 더블클릭해 스크립트를 연다.

글자 없는 아이콘 버튼은 포인터를 올리면 이름이 툴팁으로 표시된다. 창 위쪽 가운데 도구 모음의 일시정지·한 틱 진행과 Scene의 도구 버튼도 아이콘 버튼이다. 패널, 플로팅 창, 대화 상자의 색·글자 크기·간격은 같은 디자인 토큰을 쓴다. 긴 오브젝트·필드 이름은 말줄임표로 줄이고 툴팁으로 전체 이름을 보여 준다. [토큰과 아이콘 캡처](../../engine/tests/results/editor-theme/README.md)

## 패널 배치와 레이아웃

Hierarchy, Project, Scene, Game, Inspector와 아래 도구 패널은 각각 독립 패널이다. 패널 위쪽 탭을 끌어 다른 곳에 놓으면 배치가 바뀐다. 끄는 동안에는 놓일 영역만 반투명 강조로 겹쳐 표시되고 주변 패널은 움직이지 않는다.

| 놓는 위치 | 결과 |
| --- | --- |
| 다른 패널의 탭 줄 또는 가운데 | 그 패널과 같은 묶음의 탭이 된다. 탭을 눌러 앞에 보일 패널을 고른다. |
| 다른 패널 안쪽의 위·아래·왼쪽·오른쪽 1/4 | 그 패널을 둘로 나누어 해당 방향에 놓는다. |
| 창 가장자리 16px 안쪽 | 전체 배치를 나누어 창의 그쪽 끝에 놓는다. |

- 패널 사이의 분할선을 끌어 크기를 바꾼다. 창 크기를 바꾸면 Scene이 남는 공간을 차지하고 옆 패널은 너비를 유지한다. Scene이 다른 곳으로 옮겨지거나 창이 좁아지면 옆 패널이 최소 크기까지 줄어 Scene에 40% 이상을 남긴다.
- 패널마다 최소 크기가 있다. Inspector는 218px보다 좁아지지 않아 레이블 열(56px)과 입력 열(레이블 열 + 4px 간격)의 위치가 유지된다. Hierarchy·Project는 140px, Scene은 240px이다.
- 탭의 **×** 또는 탭 우클릭 → **Close**로 패널을 닫는다. Scene은 닫을 수 없다. 패널을 옮기거나 탭 뒤로 숨겨도 Play는 멈추지 않는다.
- 탭이 묶음 너비를 넘으면 탭 줄이 옆으로 밀리고 앞에 보이는 탭이 항상 보인다.

### 도구 패널

예전에 별도 창으로 열리던 편집기는 패널로 열린다. 처음 여는 도구 패널은 Scene 아래를 나눈 영역에 들어가고, 이후 패널은 그 영역에 탭으로 추가된다. 이미 열린 패널을 다시 열면 새로 만들지 않고 그 탭을 앞으로 가져온다.

| 패널 | 여는 방법 |
| --- | --- |
| Console | **Project → Console…**, Play의 **Debug → Console…** |
| Profiler | Play의 **Debug → Profiler**, **Run → Frame Profiler** |
| Animator | Project의 `.controller` 더블클릭, **+ → New Animator Controller…** |
| Clip | Project의 `.clip` 더블클릭, **+ → New Animation Clip…** |
| Tileset | Project의 `.tileset` 더블클릭, **+ → New Tileset…** |
| Input Actions | **Project → Input Actions…** |
| Dialogue | 대화 데이터 더블클릭, **+ → New Dialogue…** |
| Data | 데이터 에셋·테이블 더블클릭, **+ → New Data Asset…**, **New Data Table…** |

에셋 편집기는 종류마다 패널 하나를 쓴다. 같은 에셋을 다시 열면 그 탭으로 이동한다. 다른 에셋을 열면 열린 편집기를 닫고 새 에셋으로 바꾼다. 저장하지 않은 클립이나 애니메이터가 있으면 먼저 저장 여부를 묻고, **Cancel**을 고르면 기존 편집기가 남는다. 탭 제목에는 에셋 이름과 종류가 표시되고 클립은 저장하지 않은 변경이 있으면 `*`가 붙는다. 데이터 에셋과 테이블은 같은 Data 패널을 쓴다. 편집기의 **Save** 또는 **Cancel**·**Close**를 누르면 패널이 닫힌다.

- 편집기가 패널보다 작게 줄어들 수 없을 때는 패널 안에서 스크롤된다.
- Play 중에는 Animator·Tileset·Input Actions·Dialogue·Data 편집기가 비활성 상태로 남는다. Stop하면 다시 편집할 수 있다. Clip은 Play 중에도 편집할 수 있다.
- Clip에 포커스가 있으면 **File → Save Scene**(⌘S)과 **Edit → Undo / Redo**는 클립에 적용된다. 다른 편집기의 텍스트 필드에서는 그 필드의 Undo가 적용된다.
- 씬이나 프로젝트를 바꾸거나 에디터를 닫으면 열린 편집기도 닫는다. 저장하지 않은 클립·애니메이터는 먼저 묻는다.

### Scene과 Game 탭, 도구 모음

Scene 탭은 편집 화면이다. 머리줄 왼쪽의 도구 버튼과 Snap 토글은 [기즈모](#다중-선택과-기즈모)를 고르고, 오른쪽에는 씬 이름(저장하지 않았으면 `*`)이 표시된다. Game 탭은 기본 배치에서 Scene 옆 탭이다.

창 위쪽 가운데 도구 모음에 **Play**·일시정지·한 틱 진행이 있다. Play를 누르면 Game 탭이 앞으로 오고 게임 화면이 Game 패널에 그려진다. Play 중에는 Play 왼쪽에 **Debug**와 배속 메뉴가 나타난다. **Stop** 또는 **Esc**로 멈추면 Scene 탭으로 돌아간다. 일시정지·한 틱 진행은 Play 중에만 누를 수 있다. [시간 제어](time.md)

| Game 패널 머리줄 | 동작 |
| --- | --- |
| 해상도 메뉴 | 프로젝트 게임 해상도(**Project → Game Resolution…**와 같은 값)를 고른다. 320×180·640×360·800×450·1280×720·1920×1080과 현재 값이 있고 **Custom…**은 해상도 창을 연다. 고르면 프로젝트 설정에 저장되며 Play 중에는 바꿀 수 없다. |
| 배율 메뉴 | **Fit**은 패널에 맞춰 그린다. **1×–4×**는 게임 픽셀 하나를 패널 1–4단위로 고정해 그린다. 넘치는 부분은 잘린다. |
| **Stats** | 최근 60프레임 평균 FPS와 프레임 시간, 마지막 프레임의 SDL 그리기 호출 수를 표시한다. |

Play의 **Debug** 도구(충돌체·질의·레이·Profiler·Console)는 Game 패널 아래에 열린다. [실행 진단](play-diagnostics.md)

Scene과 Game은 SDL 화면 하나를 함께 쓴다. 편집 중에는 Scene 패널에, Play 중에는 Game 패널에 표시된다. 그래서 두 탭을 나란히 놓아도 Play 중의 Scene 패널에는 안내 문구만 보인다.

### 플로팅 창

탭을 메인 창 밖으로 끌어 놓거나 탭 우클릭 → **Float in Window**를 고르면 그 패널이 별도 창으로 떨어진다. Scene, Game, Inspector를 포함한 모든 패널을 띄울 수 있다. 창 위쪽 줄의 **Dock**을 누르거나 창을 닫으면 패널이 메인 창의 원래 기본 위치로 돌아간다. 메인 창에는 패널이 하나 이상 남아야 한다.

- Play 중에 Game 패널을 띄우거나 다시 붙여도 게임이 이어진다. Scene을 띄운 채 Play하면 게임은 메인 창의 Game 탭에 그려지고, Stop하면 띄운 Scene 창으로 돌아온다.
- 플로팅 창이 앞에 있어도 게임 입력과 단축키는 메인 창과 같다. macOS 메뉴 막대는 메인 창이 앞에 있을 때 표시된다.

**Window** 메뉴

| 항목 | 동작 |
| --- | --- |
| 패널 이름(Hierarchy부터 Data까지 모든 패널) | 열린 패널에는 체크가 표시된다. 닫힌 패널을 다시 연다. Hierarchy는 Project 위, Project는 Hierarchy 아래, Game은 Scene 옆 탭, Inspector는 창 오른쪽, 도구 패널은 Scene 아래 영역에 돌아온다. 이미 열린 패널은 탭을, 띄운 패널은 그 창을 앞으로 가져온다. 열린 에셋이 없는 편집기 패널은 여는 방법을 안내한다. |
| Layout → Default | 처음 배치. 왼쪽에 Hierarchy와 Project(176px), 가운데 Scene·Game 탭, 오른쪽에 Inspector(218px). |
| Layout → Editing | Hierarchy·Project를 한 탭 묶음(220px)으로, Inspector를 300px로 넓힌다. |
| Layout → Animation | 위쪽에 Hierarchy·Scene·Game·Inspector, 아래쪽 전체 너비(260px)에 Clip·Animator·Project 탭. |
| Layout → Custom | **Save Layout as Custom**으로 저장한 배치. 저장 전에는 비활성이다. |
| Save Layout as Custom | 현재 배치와 크기, 띄운 창의 위치를 Custom으로 저장한다. |
| Reset Layout | 띄운 패널을 모두 메인 창에 붙이고 Default로 되돌린다. |

배치는 프로젝트가 아니라 사용자 설정에 저장되어 다음 실행에 복원된다. 패널을 옮기거나 분할선을 놓을 때와 에디터를 닫을 때 저장하며, 창 위치·크기, 띄운 패널 창의 위치·크기, Custom 배치도 함께 저장한다. 위치는 macOS `~/Library/Application Support/MoNoon/editor-layout.json`, Windows `%LOCALAPPDATA%\MoNoon\editor-layout.json`, Linux `~/.local/share/MoNoon/editor-layout.json`이다. `MONOON_USER_DATA`를 지정하면 그 폴더를 쓴다. 저장한 패널 크기가 현재 화면보다 크면 Default 배치를, 저장한 창 위치가 연결된 모니터 밖이거나 창이 화면보다 크면 기본 크기(1220×740)를 쓴다. 띄운 패널 창이 사라진 모니터에 있었거나 화면보다 크면 메인 창 가운데에 기본 크기로 연다. 파일을 지우면 Default로 시작한다.

## 오브젝트와 프리팹

- Hierarchy의 **+**는 Square 스프라이트 오브젝트를 만든다. 우클릭 메뉴에서 **Create Empty Object**, **Create Empty Child**, **Add Square**, **Add Child Square**도 사용할 수 있다.
- 오브젝트를 새 부모 위에 드롭하면 현재 월드 위치를 유지하면서 부모가 바뀐다. Hierarchy의 빈 영역에 놓으면 루트로 돌아간다. 부모를 이동하면 자식도 따라간다.
- 우클릭 **Duplicate**는 선택한 오브젝트, 하위 트리, 컴포넌트 설정을 복제한다. 트리 내부를 가리키는 오브젝트 참조도 복제본에 연결된다. 한 번의 Undo로 전체 복제를 되돌릴 수 있다.
- **Edit → Copy**, **Cut**, **Paste**로 Hierarchy나 Scene에서 고른 오브젝트를 같은 씬이나 나중에 연 다른 씬에 붙여 넣는다. Hierarchy 우클릭 메뉴에도 같은 명령이 있다. 자식, 컴포넌트, 프리팹 연결과 오버라이드가 함께 복사된다. 붙여 넣은 오브젝트는 새 ID를 받고, 선택한 오브젝트와 같은 부모 아래(선택이 없으면 루트)에 놓인다. 같은 이름의 형제가 있으면 Duplicate처럼 `이름 (1)`이 된다.
- 복사한 오브젝트끼리의 참조는 붙여 넣은 사본으로 바뀐다. 복사하지 않은 오브젝트를 가리키는 참조는 그대로 둔다. 대상 씬에 그 오브젝트가 없으면 Inspector에 **Missing object or component**로 표시된다. 끊긴 참조가 남은 씬은 게임 빌드가 거부하므로 Inspector에서 다시 연결한다. 붙여 넣기 한 번과 잘라내기 한 번은 각각 Undo 한 번으로 되돌린다.
- 복사한 오브젝트는 OS 클립보드에 `# MoNoon objects`로 시작하는 텍스트로 저장된다. 같은 프로젝트를 연 다른 에디터 창에서도 붙여 넣을 수 있다. 텍스트 필드에 포커스가 있으면 세 명령은 텍스트를 복사하고 붙여 넣는다. [검증 기록](../../engine/tests/results/copy-paste/README.md)
- **Delete Object**로 부모를 지우면 직계 자식은 한 단계 위로 이동한다. 실수로 삭제했을 때는 **Edit → Undo**를 사용한다.
- 재사용할 오브젝트를 Hierarchy에서 Project 파일 목록으로 끌면 현재 폴더에 `.prefab`이 생긴다. 이 파일을 Scene으로 드래그해 인스턴스를 배치한다. 인스턴스 루트의 **Apply to Prefab**으로 수정한 하위 트리를 원본에 반영한다. 공통 설정이 바뀌어야 하는 다른 인스턴스도 확인하고 씬을 저장한다.

## 다중 선택과 기즈모

Hierarchy에서 첫 오브젝트를 클릭하고 마지막 오브젝트를 **Shift+클릭**하면 펼쳐진 행을 범위 선택한다. **Cmd+클릭**(Windows·Linux는 **Ctrl+클릭**)으로 한 개씩 추가하거나 뺄 수 있다. Scene에서는 **Shift+클릭**으로 개별 선택을 전환한다. Inspector 탭 아래 작업 줄에 선택 개수가 표시된다. 여러 월드 오브젝트를 함께 이동·회전·스케일하려면 Scene 기즈모를 사용한다.

| 도구 | 조작 | Snap을 켰을 때 |
| --- | --- | --- |
| Move (W) | 빨간 X축 또는 초록 Y축을 끌어 한 축만 이동한다. 중앙을 끌면 두 축으로 이동한다. | 시작 위치에서 16단위씩 이동 |
| Rotate (E) | 원 위에서 눌러 선택 중심을 기준으로 돌린다. | 시작 각도에서 15°씩 회전 |
| Scale (R) | 축 끝 사각형을 끌면 한 축, 중앙을 오른쪽 위로 끌면 두 축을 함께 확대한다. | 시작 배율의 10%씩 변화 |
| Rect (T) | 오브젝트 하나를 골랐을 때 빨간 축 끝을 끌면 Width, 초록 축 끝을 위로 끌면 Height가 바뀐다. 배율은 그대로다. 중앙을 끌면 이동한다. | 16단위씩 변화 |

도구는 Scene 탭 머리줄 왼쪽의 버튼으로도 고르고, 옆의 자석 버튼으로 Snap을 켠다. 고른 도구는 강조색으로 표시된다. 단축키는 Scene에 포커스를 두고 사용한다. 단일 오브젝트는 자신의 피벗, 그룹은 선택 영역의 중심을 사용한다. 부모와 자식을 함께 골라도 자식의 로컬 변환은 유지된다. 드래그 도중 **Esc**로 취소하고, 드래그를 마친 뒤 **Cmd+Z**로 그룹 전체를 한 번에 되돌린다. 화면 UI는 앵커·레이아웃의 오프셋으로 편집한다.

### Inspector에서 충돌 마스크 함께 편집하기

1. KeyRoom의 Project 패널에서 `Scenes/WallWorkshop.scene`을 열고 Hierarchy에서 **Wall 01**을 클릭한 뒤 **Wall 10**을 **Shift+클릭**한다.
2. Inspector 작업 줄에 **10 selected**가 표시되고 공통 컴포넌트가 나온다. 모든 벽에서 같은 값은 그대로 보이고 서로 다른 값은 **Mixed**로 보인다. 예제에서 너비는 76, 높이는 28이며 충돌 레이어와 마스크는 혼합 상태다.
3. **Box Collider 2D → Mask**에서 **None**을 고르면 10개 마스크가 모두 0이 된다. **All layers**는 32개 비트를 전부 켠다. 개별 레이어의 **✓**는 모든 벽이 켰다는 뜻이고, **−**는 일부 벽만 켰다는 뜻이다. 혼합된 레이어를 클릭하면 그 비트를 전부 켜고, 모두 켜진 레이어를 클릭하면 그 비트를 전부 끈다. 각 벽의 다른 비트는 유지된다.
4. **Edit → Undo** 또는 **Cmd+Z**(Windows·Linux는 **Ctrl+Z**)를 한 번 누른다. 벽 10개가 선택된 채 원래 마스크로 돌아간다. 벽 하나를 선택하고 **Overrides…**를 열면 그 인스턴스의 개별 설정을 볼 수 있다.


## 프리팹의 원본과 인스턴스 편집

1. 오브젝트를 선택해 **Scene → Create Prefab**으로 에셋을 만든다. 선택한 오브젝트가 첫 인스턴스가 된다. Project의 프리팹을 Scene으로 끌어 같은 원본의 인스턴스를 추가한다.
2. 인스턴스의 이름·변환·컴포넌트 속성을 바꾸면 오버라이드가 생긴다. 인스턴스 루트에서 **Scene → Inspect Overrides…**로 수정한 필드, 추가·삭제한 컴포넌트와 자식을 확인한다. 루트의 배치 좌표는 원본을 갱신해도 유지된다.
3. **Apply to Prefab**은 현재 하위 트리를 원본에 반영한다. 다른 인스턴스는 자신이 수정한 필드를 유지하며 나머지 속성을 상속한다. **Revert Prefab Overrides**는 선택한 인스턴스를 원본 상태로 되돌린다. 원본 적용, 되돌리기, 프리팹 생성도 Undo와 Redo를 지원한다.
4. **중첩**하려면 프리팹 인스턴스를 다른 오브젝트의 자식으로 놓고 부모를 프리팹으로 만든다. 예를 들어 Chest Lid를 먼저 프리팹으로 만든 뒤 Chest를 프리팹으로 만들면, 뚜껑 원본의 변경이 상자 안에도 반영된다.
5. **변형**을 만들려면 원본의 인스턴스를 복제하고 일부 속성을 바꾼 뒤 **Create Prefab Variant**를 선택한다. 새 에셋이 원본을 상속하고 현재 오브젝트가 변형의 인스턴스가 된다. 변형 인스턴스에서 Apply는 해당 변형을 갱신하고, Revert는 해당 변형의 값으로 복구한다. 씬 배치와 인스턴스 수정은 **Cmd+S**로 저장한다.

### 프리팹 원본과 속성별 오버라이드

Project에서 `.prefab`을 더블클릭하면 별도 창의 Hierarchy와 뷰포트에 그 프리팹만 열린다. 자식을 선택해 기존 Inspector로 편집할 수 있다. 상단 **Save** 또는 **Cmd+S**로 원본을 저장한 뒤 창을 닫는다. 원본의 변경을 상속하는 인스턴스는 즉시 갱신된다. 인스턴스 루트의 씬 배치 좌표는 각 인스턴스가 유지한다.

KeyRoom 첫 방에서 다음 순서로 두 나무의 상속을 비교할 수 있다.

1. Project에서 `Oak.prefab`을 검색해 더블클릭한다. **Transform → Size → H**를 98에서 112로 바꾸고 저장한다. 씬의 **Oak tree**는 높이 112가 되고, **Courtyard oak**는 자체 높이 70을 유지한다.
2. Hierarchy에서 **Courtyard oak**를 선택하고 Inspector 작업 줄의 **Overrides…**를 연다. **width** 행의 **Source**는 102, **Instance**는 76이다. **Apply to Source**를 누르면 두 나무 모두 너비 76이 된다. **Done**으로 닫은 뒤 **Cmd+Z** 한 번으로 원본과 씬을 함께 되돌릴 수 있다.
3. 너비를 적용한 상태에서 **height → Revert**를 누르면 **Courtyard oak**도 높이 112를 상속한다. 이름과 위치는 유지된다. **Done → Cmd+S**로 씬을 저장하고 다시 열면 이 나무의 오버라이드 목록에는 이름만 남는다.

씬에만 존재하는 오브젝트 참조는 인스턴스에 설정할 수 있다. 프리팹 원본에 적용할 수 있는 참조 대상은 해당 프리팹 안의 오브젝트다.

### 자식·컴포넌트 추가와 삭제의 개별 적용

인스턴스에서 추가하거나 삭제한 자식과 컴포넌트도 **Overrides…** 목록에 한 행씩 표시된다. 행 이름 끝에 **(added object)**, **(removed object)**, **(added)**, **(removed)**가 붙는다. 자식을 추가하거나 삭제하면 그 하위 트리가 한 행이 된다. 행을 고르고 **Apply to Source**를 누르면 그 변경만 원본에 들어가 다른 인스턴스에도 반영된다. **Revert**는 선택한 인스턴스에서만 그 변경을 되돌린다. 삭제한 자식을 복원하면 원래 ID와 부모로 돌아간다. 각 동작은 Undo 한 번으로 되돌릴 수 있다. 원본과 씬은 함께 복원된다. 구조 전체를 한 번에 반영하려면 **Scene → Apply to Prefab**을 사용한다.

KeyRoom의 `Scenes/PrefabWorkshop.scene`에서 자물쇠만 원본에 적용하고 효과음은 한 상자에 남겨 보자.

1. Hierarchy에서 **Chest 2**를 우클릭해 **Add Child Square**를 선택한다. 새 자식의 이름을 **Lock**으로 바꾼다.
2. **Chest 2**를 다시 선택하고 **Add Component → Audio Source**를 추가한다. **Sound**에 `Assets/Unlock.wav`를 지정한다.
3. Inspector 작업 줄의 **Overrides…**를 연다. **Lock (added object)**를 선택하고 **Apply to Source**를 누른다. 세 상자 모두 Lock을 갖게 된다. 목록에는 **Chest 2 · audioSource (added)**만 남고, 효과음은 Chest 2에만 있다.
4. **Done**으로 닫고 **Cmd+Z**를 한 번 누른다. `Chest.prefab`과 Chest 1·3에서 Lock이 빠지고 Chest 2의 두 오버라이드가 다시 나타난다. **Cmd+Shift+Z**로 다시 적용한 뒤 **Cmd+S**로 씬을 저장한다. 다시 열어도 Chest 2에는 효과음 오버라이드가 남아 있다.

자식을 삭제했지만 그 아래의 자식이 다른 위치에 남아 있으면 삭제를 적용할 수 없다. 남은 자식의 부모 변경을 먼저 적용하거나 되돌린다. 원본 오브젝트를 하위에 둔 추가 자식도 적용하거나 되돌리기 전에 원본 오브젝트를 밖으로 옮겨야 한다. 씬 전용 참조를 가진 컴포넌트는 원본에 적용할 수 없다. [검증 기록](../../engine/tests/results/prefab-structure/README.md)

## 컴포넌트와 외부 에디터

1. 오브젝트를 선택하고 **Add Component**를 연다. 메뉴의 **Built-in components**에서 엔진 기능을, **Project scripts**에서 프로젝트에 있는 동작을 고른다. **(Added)**는 이미 붙인 항목이다.
2. 새 동작은 **Add Component → New C# Component… → Create & Add**로 생성과 부착을 함께 할 수 있다. **Project → + → New C# Script…**로 파일을 먼저 만들었다면, Project에서 `.cs`를 선택된 오브젝트의 Inspector로 드래그한다.
3. macOS에서는 **MoNoon → Preferences…**, Windows·Linux에서는 **Edit → Preferences…**를 연다. **External script editor**를 지정하거나 **Use System Default**를 선택한다.
4. Project의 `.cs`를 더블클릭하거나 Inspector의 스크립트 톱니바퀴에서 **Edit Script**를 누른다. 외부 에디터에서 저장하고 MoNoon으로 돌아온 뒤 Play한다. Play 중에 저장하면 필드 값을 유지한 채 새 코드로 바뀐다. [Play 중 스크립트 다시 불러오기](scripting.md#play-중-스크립트-다시-불러오기) 컴파일 오류는 **Project → Console…**에서 찾아 더블클릭하면 파일을 열 수 있다. [C# 작성 규칙과 예제](runtime.md)

5. 프로젝트의 `Editor/` 폴더에 C# 파일을 두면 그 프로젝트에서만 쓰는 메뉴 명령, 스크립트 컴포넌트의 Inspector, 도킹 패널을 추가할 수 있다. KeyRoom은 **Project → Extensions → Place Coin**, CollectCoin의 Inspector 행, **Window → Coins** 패널을 이렇게 추가한다. [프로젝트 에디터 확장](workspace.md#프로젝트-에디터-확장)

## 저장과 단축키

**File → Save Scene**은 현재 씬을 저장한다. **Project → + → New Scene**으로 씬을 추가하고 Project의 `.scene`을 더블클릭해 전환한다. 씬 Undo와 애니메이션 클립 편집기의 Undo는 따로 기록된다. Clip 패널에 포커스가 있으면 ⌘Z / Ctrl+Z가 클립 편집을 되돌린다. 클립과 타일셋 변경은 각각 **Save Clip**, **Save Tileset**으로 에셋에 저장한다.

| 동작 | macOS | Windows·Linux |
| --- | --- | --- |
| 새 프로젝트 / 열기 / 씬 저장 | ⌘N / ⌘O / ⌘S | Ctrl+N / Ctrl+O / Ctrl+S |
| Undo / Redo | ⌘Z / ⇧⌘Z | Ctrl+Z / Ctrl+Shift+Z |
| 선택 오브젝트 잘라내기 / 복사 / 붙여넣기 | ⌘X / ⌘C / ⌘V | Ctrl+X / Ctrl+C / Ctrl+V |
| Hierarchy 선택 복제 | ⌘D | Ctrl+D |
| Hierarchy 선택 제거 | ⌘Delete | Delete |
| Preferences | ⌘, | Ctrl+, |
| Scene Play 종료 / 진행 중인 타일 획 취소 | Esc | Esc |

## 프로젝트 파일 보관

`Scenes/`에는 씬을, `Assets/`에는 PNG·WAV·클립·타일셋 등을, `Scripts/`에는 프로젝트의 C# 동작을 둔다. `GameSettings`에는 게임 해상도와 시작·포함 씬, 키 설정이 들어간다. `.scene`, `.prefab`, `.clip`, `.tileset`, `.meta`, `.monoon-assets`와 `GameSettings`는 VYaml이 읽는 YAML 문서다.

프로젝트를 옮기거나 버전 관리에 넣을 때 숨김 파일인 `.monoon-assets`와 에셋별 `.meta`도 함께 담는다. Finder 등에서 에셋 이름이나 위치를 바꿀 때는 같은 이름의 `.meta`를 같이 옮겨야 기존 GUID를 확실하게 유지할 수 있다. 예전 JSON·이중 확장자 프로젝트의 변환 절차는 [빌드와 배포](build.md)에 있다.
