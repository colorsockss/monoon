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

## 버튼 테마와 9-slice

KeyRoom에서 **M**으로 방 안내를 열고 **L**로 한글로 바꾼다. **글자 크게**를 두 번 누르면 100% → 150% → 200%로 바뀐다. 최대 배율에서는 크게 버튼이 비활성화된다. **닫기**로 돌아간 뒤 **조작법**을 누르면 같은 테두리를 사용하는 조작 안내도 볼 수 있다. 글자 배율은 방을 이동하는 동안 유지되고 새 실행에서는 프로젝트 기본값을 사용한다.

### 버튼 스타일 공유

1. Project 패널에서 **KeyRoomTheme.table**을 더블클릭한다. 표의 `fill`과 `text` 열은 각각 버튼의 배경색과 글자색이며 값은 `#RRGGBB` 형식이다.
2. **Project → UI Accessibility… → Button theme**에서 이 테이블을 선택하고 **Save defaults**를 누른다. 에디터와 내보낸 게임은 같은 테이블을 읽는다.
3. 버튼 Inspector의 **UI Button → Style**에서 스타일을 선택한다. 기본 이름은 `button`이다. 다른 스타일을 추가하려면 같은 테이블에 `이름.상태` 형식으로 다섯 행을 추가한다.

| 상태 행 | 적용되는 순간 |
| --- | --- |
| `button.normal` | 기본 표시 |
| `button.hover` | 포인터가 버튼 위에 있음 |
| `button.focused` | 포커스를 받음. 키보드·패드 탐색에서도 테두리로 표시 |
| `button.pressed` | 버튼 안에서 마우스를 누르고 있거나 포커스된 버튼의 활성화 키를 누르고 있음 |
| `button.disabled` | Interactable을 끄거나 스크립트에서 비활성화함 |

버튼 누름과 해제를 같은 버튼 안에서 마치면 클릭이 발생한다. 다른 방향으로 끌어 놓으면 취소된다. 고대비 설정을 켜면 접근성 색상과 포커스 테두리를 우선한다. KeyRoom 상단 HUD의 **UI Rect → Scale width and X with text**는 글자 배율에 따라 폭과 앵커 기준 가로 오프셋을 함께 조절한다. [KeyRoom 테마 테이블](../../samples/KeyRoom/Assets/KeyRoomTheme.table)

### 늘어나는 안내 패널

**UI Image → Sprite**에서 PNG를 선택한 뒤 **Mode → 9-slice**를 켠다. **L / R**에는 왼쪽과 오른쪽, **T / B**에는 위와 아래 테두리의 원본 PNG 픽셀 수를 입력한다. 서로 마주 보는 두 테두리 사이에는 최소 한 픽셀의 중심 영역이 있어야 한다.

KeyRoom의 **Guide panel**은 24×24 PNG에 네 방향 8픽셀 테두리를 쓴다. 모서리는 게임 해상도 기준 크기를 유지하고, 네 변은 길이 방향으로, 가운데는 가로·세로로 늘어난다. 창 전체의 확대·축소는 게임과 같이 적용된다. 목적지가 양쪽 테두리보다 작아지면 양쪽을 비례 축소한다. **UI Layout**의 패딩은 테두리 안쪽 여백을 정하고, **UI Text → Wrap lines**는 번역 길이와 글자 배율에 맞춰 행 높이를 정한다. 그 결과로 계산한 버튼 사각형이 그리기와 입력 판정에 함께 사용된다. [원본 PNG](../../samples/KeyRoom/Assets/GuideFrame.png) · [방 안내 씬](../../samples/KeyRoom/Scenes/RoomGuide.scene) · [Mac 실행 기록](../../engine/tests/results/ui-theme/README.md)

## 한글 이름 입력과 번역

**UI → Text Field**를 만들고 **UI Text Field → Value / Limit**에서 초기값과 최대 글자 수를 정한다. **UI Text → Text**는 비어 있을 때만 보이는 안내문이다. 필드를 클릭하거나 포커스를 옮겨 Enter를 누른 뒤 macOS 두벌식으로 입력한다. 밑줄은 조합 중인 문자열이다. 스크립트의 `ui.Value(id)`는 확정한 값만 반환하고 `ui.Preedit`은 현재 조합을 반환한다. 좌우 이동, Backspace와 Delete는 결합 문자와 이모지 묶음을 한 글자로 취급한다. 입력값은 실행 중 상태이며 원본 씬을 덮어쓰지 않는다.

## 공용 번역 테이블과 게임 폰트

Project 패널에서 `.table`을 열고 행 키에 문구 ID, **Text** 형식의 열 이름에 `en`, `ko`, `ko-KR` 같은 언어 태그를 쓴다. **Project → UI Accessibility…**의 **Text table**에서 이 에셋을 선택한다. **Fallback locale**에는 테이블에 있는 열을 지정한다. [KeyRoom 번역 테이블](../../samples/KeyRoom/Assets/KeyRoomText.table)

| 표시할 내용 | 연결 방법 | KeyRoom 예시 |
| --- | --- | --- |
| 버튼·제목 | Inspector의 **UI Text → Text key**에 키 입력 | `bag.title` → 가방 |
| 상태에 따라 바뀌는 문구 | `ui.SetTextKey(id, key)`로 새 키 연결 | `bag.equip` 또는 `bag.stow` |
| 아이템 이름·의뢰 진행 | `ui.SetTextKey(id, formatKey, argumentKey)`로 서식과 인자의 번역 키를 연결 | `quest.key.active`의 `{0}`에 `item.key`를 넣어 ‘황동 열쇠 찾기’ 표시 |
| 컷신 대사·화자 | `.cutscene` 행의 `textKey`, `speakerKey` 지정 | `text`, `speaker`에는 원문 입력 |

`ui.Preferences.Locale`을 바꾸면 키가 연결된 위젯은 그 프레임을 그릴 때 새 언어를 사용한다. 서식과 각 인자도 이때 함께 번역하므로, 가방 스크립트는 나침반 장착이나 열쇠 획득으로 표시할 키가 달라질 때만 다시 연결한다. 조회는 행마다 정확한 언어 태그, 상위 언어, 프로젝트의 대체 언어 순으로 진행하며 빈 셀을 건너뛴다. 해당 문구를 찾을 수 없으면 Inspector의 **Text** 또는 `Translate(key, fallback)`의 원문을 사용한다. 원문을 생략한 스크립트 호출은 키를 돌려준다. 기존 씬의 위젯별 **Locales**도 동작한다. [KeyRoom 가방 스크립트](../../samples/KeyRoom/Scripts/BagMenu.cs)

**Project 패널 → + → Import Assets…**로 TTF·OTF를 추가한다. **UI Accessibility…**의 **Default font**를 지정하고 **+ Fallback font**로 대체 글꼴을 순서대로 추가한 뒤 **Save defaults**를 누른다. 엔진은 텍스트를 NFC로 정규화하고 글자마다 필요한 글리프를 가진 첫 번째 글꼴을 사용한다. 게임 글꼴을 지정하면 배포 대상 OS에 설치된 글꼴과 관계없이 같은 순서를 사용한다. 글꼴 에셋을 지정하지 않은 프로젝트는 기존 시스템 글꼴을 사용한다. Release와 Native AOT 내보내기에는 선택한 테이블, 글꼴, 같은 이름의 `.license` 파일이 함께 들어간다.

KeyRoom에서 **I**로 가방을 열고 **L** 또는 언어 버튼으로 영어와 한국어를 전환한다. 가방을 닫고 시작점에서 왼쪽의 열쇠를 주운 뒤 다시 열면 같은 아이템 이름이 의뢰 완료 문구에도 쓰인다. 샘플의 기본 글꼴은 고운돋움 부분 글꼴이며 ‘가방’을 포함한다. ‘휴대용’의 ‘휴’와 ‘황동’의 ‘황’은 대체 글꼴인 나눔고딕에서 가져온다. 두 글꼴은 SIL Open Font License로 배포된다. [고운돋움 원본](https://github.com/google/fonts/tree/main/ofl/gowundodum) · [나눔고딕 원본](https://github.com/google/fonts/tree/main/ofl/nanumgothic)

## 플레이어 표시 설정과 탐색

**Project → UI Accessibility…**에서 언어 태그, 글자 크기 **75–200%**, 고대비를 설정한다. 편집 모드의 **Save defaults**는 프로젝트의 `GameSettings`에 저장하고, **Apply in Play**는 현재 세션만 바꾼다. 게임 안에서도 `click.Ui.Preferences.Locale`, `TextScale`, `HighContrast`를 바꾸면 화면에 반영되고 씬을 전환해도 세션 동안 유지된다. 사용자별로 다음 실행에 복원하려면 [플레이어 저장 API](player-data.md)를 사용한다.

| 조작 | 키보드 | 게임패드 |
| --- | --- | --- |
| 순서대로 / 역순으로 이동 | Tab / Shift+Tab | — |
| 화면상의 방향으로 이동 | 방향키 | D-pad / 왼쪽 스틱 |
| 버튼 활성화 / 입력 시작 | Enter 또는 Space | 아래쪽 페이스 버튼(A / Cross) |
| 입력 끝내기 | Enter, Esc 또는 Tab | 아래쪽 버튼으로 끝내기, 오른쪽 버튼으로 포커스 해제 |

포커스 링으로 현재 위젯을 표시한다. 비활성·숨김 항목은 건너뛴다. 게임플레이가 있는 씬에서는 먼저 Tab이나 클릭으로 UI에 포커스를 주고, UI 전용 씬에서는 방향키·패드로 바로 탐색할 수 있다. [AccessLab 프로젝트](../../samples/AccessLab/)에는 한국어·일본어·영어로 바꿀 수 있는 설정 화면과 [저장 버튼 스크립트](../../samples/AccessLab/Scripts/PlayerOptions.cs)가 있다.

인벤토리 위에 확인창을 겹쳐 열고 각 단계의 입력과 포커스를 관리하려면 UI Rect에서 **Input mode root**를 켠다. [입력 모드 전환과 복귀](input-actions.md#입력-모드-전환과-복귀)
