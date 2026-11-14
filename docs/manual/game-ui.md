# 게임 화면 UI

[매뉴얼 목차](README.md)

## 화면 오른쪽 위에 버튼 두기

1. Hierarchy에서 우클릭해 **UI → Canvas**를 만든다. Canvas 위에서 우클릭하고 **UI → Button**을 선택한다. **Group**은 여러 위젯을 함께 이동하거나 조건부로 표시할 때 쓴다. **Text**, **Image**도 같은 메뉴에 있다.
2. 버튼을 선택하고 **UI Rect → Anchor**를 **TopRight**로 바꾼다. Transform의 **Offset**을 X = −12, Y = 12로 두면 게임 화면의 위와 오른쪽에서 12단위 안쪽에 배치된다. **Size**로 클릭 영역을 정하고 **UI Text → Text**에 `Controls`를 입력한다.
3. **UI Button → Interactable**을 켠다. Play에서 버튼 위에서 누르고 같은 버튼 위에서 놓으면 한 번 활성화된다. 밖으로 끌어 놓으면 클릭이 취소된다. **Tab / Shift+Tab**으로 포커스를 옮기고 **Enter** 또는 **Space**로 누를 수 있다.

UI 좌표는 게임 해상도 단위다. 앵커는 가로·세로 세 지점을 조합한 9개 위치 중 하나를 선택한다. 자식은 부모 영역에 맞춰 배치되고 버튼 그림과 클릭 판정은 같은 영역을 사용한다. 카메라가 이동해도 화면 UI는 화면에 고정된다. 여러 Canvas가 겹치면 **Order**가 큰 Canvas가 위에 그려진다.

## Controls로 안내 열기와 닫기

1. Canvas 아래에 **UI → Group**을 추가하고 그 아래에 **UI → Text**를 둔다. 안내 문구를 입력하고 Group의 **UI Rect → Show if**를 `controls`로 지정한다. 편집 중에는 배치를 볼 수 있고, Play에서는 세션에 해당 플래그가 있을 때 자식까지 함께 보인다.
2. 버튼에는 **Add Component**로 `ToggleControls`를 붙인다. 샘플의 `OnUiClick`은 플래그가 있으면 지우고 없으면 설정한다. 클릭을 반복하면 같은 안내가 열리고 닫힌다. KeyRoom에서는 스크립트의 **Hint**에 안내 Text를 연결한다. [ShowControls 액션](input-actions.md)으로도 안내를 열 수 있고 표시되는 키는 현재 기기와 바인딩을 따라간다. [ToggleControls.cs](../../samples/KeyRoom/Scripts/ToggleControls.cs)
3. 버튼과 안내 그룹을 서로 다른 자식으로 둔다. 버튼을 숨겨지는 그룹 안에 두면 첫 클릭을 받을 수 없다. 창의 가로·세로를 바꾸고 버튼의 네 가장자리에서 다시 눌러 배치를 확인한다.

## 텍스트, 이미지, 동적 상태

**UI Image**의 **Sprite**를 선택하면 PNG가 영역에 그려진다. 단색 패널은 **Fill**로 색을 준다. **UI Text**에서는 **Size**, **Color**, **Inset X / Y**를 지정한다. 하나의 오브젝트에 이미지·텍스트·버튼을 함께 붙여 배경과 표시 문구를 구성할 수 있다.

스크립트는 `tick.Ui`, `contact.Ui`, `click.Ui`에서 `SetText(id, text)`, `SetVisible(id, visible)`, `SetInteractable(id, enabled)`를 호출할 수 있다. 대상은 Inspector에 연결한 오브젝트 GUID다. 런타임 변경은 Play 세션에 적용된다. [KeyRoom의 UI가 들어 있는 씬](../../samples/KeyRoom/Scenes/KeyRoom.scene)

## 자동 배치와 스크롤

**Hierarchy → UI → Layout** 아래에 위젯을 넣으면 Hierarchy 순서대로 배치된다. **Axis**에서 세로 또는 가로를 선택하고 **Padding**으로 가장자리 여백, **Gap**으로 자식 사이 간격을 정한다. **Stretch children**은 진행 방향에 수직인 축을 부모의 안쪽 폭 또는 높이에 맞춘다. 숨긴 자식은 간격도 차지하지 않는다. 레이아웃 안에서 자식의 Transform 크기는 기본 크기로 쓰고 위치는 레이아웃이 결정한다.

목록의 크기를 고정하려면 **UI → Scroll View**를 만든다. 뷰 밖의 자식은 그려지거나 클릭되지 않는다. 휠·트랙패드, 스크롤 손잡이 드래그, 키보드·패드 포커스로 보이지 않는 행을 드러낼 수 있다. 가로 목록은 **UI Layout → Axis → Horizontal**로 바꾼다. 긴 설명문은 **UI Text → Wrap lines**를 켠다. 폭·번역·글자 배율이 바뀌면 줄 높이가 바뀌고 세로 목록의 다음 행도 이동한다.

## 한글 이름 입력과 번역

**UI → Text Field**를 만들고 **UI Text Field → Value / Limit**에서 초기값과 최대 글자 수를 정한다. **UI Text → Text**는 비어 있을 때만 보이는 안내문이다. 필드를 클릭하거나 포커스를 옮겨 Enter를 누른 뒤 macOS 두벌식으로 입력한다. 밑줄은 조합 중인 문자열이다. 스크립트의 `ui.Value(id)`는 확정한 값만 반환하고 `ui.Preedit`은 현재 조합을 반환한다. 좌우 이동, Backspace와 Delete는 결합 문자와 이모지 묶음을 한 글자로 취급한다. 입력값은 실행 중 상태이며 원본 씬을 덮어쓰지 않는다.

번역할 **UI Text → Locales**에 한 줄씩 `ko=이름`, `ja=名前`처럼 입력한다. 예를 들어 `ko-KR`은 `ko-KR → ko → Text` 순서로 찾는다. 스크립트에서 `SetText`로 지정한 문구가 있으면 그 값이 우선한다. 글리프는 시스템 폰트에서 찾는다. 검증한 문자는 한글·일본어·라틴 문자이며, RTL 양방향 문단과 복합 문자권 전체에 대한 셰이핑은 이번 지원 범위에 포함하지 않는다.

## 플레이어 표시 설정과 탐색

**Project → UI Accessibility…**에서 언어 태그, 글자 크기 **75–200%**, 고대비를 설정한다. 편집 모드의 **Save defaults**는 프로젝트의 `GameSettings`에 저장하고, **Apply in Play**는 현재 세션만 바꾼다. 게임 안에서도 `click.Ui.Preferences.Locale`, `TextScale`, `HighContrast`를 바꾸면 화면에 반영되고 씬을 전환해도 세션 동안 유지된다. 사용자별로 다음 실행에 복원하려면 [플레이어 저장 API](player-data.md)를 사용한다.

| 조작 | 키보드 | 게임패드 |
| --- | --- | --- |
| 순서대로 / 역순으로 이동 | Tab / Shift+Tab | — |
| 화면상의 방향으로 이동 | 방향키 | D-pad / 왼쪽 스틱 |
| 버튼 활성화 / 입력 시작 | Enter 또는 Space | 아래쪽 페이스 버튼(A / Cross) |
| 입력 끝내기 | Enter, Esc 또는 Tab | 아래쪽 버튼으로 끝내기, 오른쪽 버튼으로 포커스 해제 |

포커스 링으로 현재 위젯을 표시한다. 비활성·숨김 항목은 건너뛴다. 게임플레이가 있는 씬에서는 먼저 Tab이나 클릭으로 UI에 포커스를 주고, UI 전용 씬에서는 방향키·패드로 바로 탐색할 수 있다. [AccessLab 프로젝트](../../samples/AccessLab/)에는 한국어·일본어·영어로 바꿀 수 있는 설정 화면과 [저장 버튼 스크립트](../../samples/AccessLab/Scripts/PlayerOptions.cs)가 있다.
