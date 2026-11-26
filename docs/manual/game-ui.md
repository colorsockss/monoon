# 게임 화면 UI

[매뉴얼 목차](README.md)

## 화면 오른쪽 위에 버튼 두기

1. Hierarchy에서 우클릭해 **UI → Canvas**를 만든다. Canvas 위에서 우클릭하고 **UI → Button**을 선택한다. **Group**은 여러 위젯을 함께 이동하거나 조건부로 표시할 때 쓴다. **Text**, **Image**도 같은 메뉴에 있다.
2. 버튼을 선택하고 **UI Rect → Anchor**를 **TopRight**로 바꾼다. Transform의 **Offset**을 X = −12, Y = 12로 두면 게임 화면의 위와 오른쪽에서 12단위 안쪽에 배치된다. **Size**로 클릭 영역을 정하고 **UI Text → Text**에 `Controls`를 입력한다.
3. **UI Button → Interactable**을 켠다. Play에서 버튼 위에서 누르고 같은 버튼 위에서 놓으면 한 번 활성화된다. 밖으로 끌어 놓으면 클릭이 취소된다. **Tab / Shift+Tab**으로 포커스를 옮기고 **Enter** 또는 **Space**로 누를 수 있다.

UI 좌표는 게임 해상도 단위다. 앵커는 가로·세로 세 지점을 조합한 9개 위치 중 하나를 선택한다. 자식은 부모 영역에 맞춰 배치되고 버튼 그림과 클릭 판정은 같은 영역을 사용한다. 카메라가 이동해도 화면 UI는 화면에 고정된다. 여러 Canvas가 겹치면 **Order**가 큰 Canvas가 위에 그려진다.

## 가장자리 고정과 해상도 미리보기

**UI Rect → Left · Right · Top · Bottom**에서 고정할 변을 체크한다. 처음 체크하면 현재 위치에서 부모의 해당 변까지 거리가 입력된다. 값은 논리 픽셀이며 음수는 부모 바깥으로 나가는 여백이다.

| 한 축에서 고정한 변 | 부모 크기가 달라질 때 |
| --- | --- |
| 없음 | 기존 Anchor, Transform의 Offset과 Size를 사용한다. |
| 한쪽 | 해당 변의 여백과 위젯 크기를 유지한다. |
| 양쪽 | 양쪽 여백을 유지하며 가운데 영역이 늘거나 줄어든다. 두 여백의 합이 부모보다 크면 해당 축의 크기는 0이 된다. |

Scene에서 위젯을 드래그하면 고정된 변의 여백도 갱신된다. **UI Layout** 또는 **Scroll View**의 직계 자식은 부모의 자동 배치 규칙을 따른다. 자동 배치 영역 자체를 늘리려면 그 영역의 UI Rect에 변을 고정한다.

1. KeyRoom의 Hierarchy에서 **Interface → Inventory**를 선택한다. 가방의 좌우 여백은 각각 64, 상하 여백은 각각 36이다.
2. **Scene → UI Layout Preview…**에서 Width, Height, Language, Text (%)를 바꾸고 **Preview**를 누른다. 예를 들어 **480 × 720**, **ko**, **200**으로 설정하면 세로 화면에서 한글 줄바꿈을 볼 수 있다. 하단 상태 표시줄에 미리보기 설정이 나온다.
3. **Play → I**로 가방을 열고 **나침반 관리**와 **닫기**를 눌러 본다. 하위 확인창도 같은 패널 영역을 채운다. **Stop → Scene → UI Layout Preview… → Use Project**로 원래 표시 설정을 복원한다.

미리보기는 현재 에디터 창의 Scene 편집과 Play에 적용된다. 배포 기본값을 바꿀 때에는 **Project → Game Resolution…**과 **Project → UI Accessibility…**에서 저장한다. [KeyRoom 검증 기록](../../engine/tests/results/ui-layout/README.md)

## Controls로 안내 열기와 닫기

1. Canvas 아래에 **UI → Group**을 추가하고 그 아래에 **UI → Text**를 둔다. 안내 문구를 입력하고 Group의 **UI Rect → Show if**를 `controls`로 지정한다. 편집 중에는 배치를 볼 수 있고, Play에서는 세션에 해당 플래그가 있을 때 자식까지 함께 보인다.
2. 버튼에는 **Add Component**로 `ToggleControls`를 붙인다. 샘플의 `OnUiClick`은 플래그가 있으면 지우고 없으면 설정한다. 클릭을 반복하면 같은 안내가 열리고 닫힌다. KeyRoom에서는 스크립트의 **Hint**에 안내 Text를 연결한다. [ShowControls 액션](input-actions.md)으로도 안내를 열 수 있고 표시되는 키는 현재 기기와 바인딩을 따라간다. [ToggleControls.cs](../../samples/KeyRoom/Scripts/ToggleControls.cs)
3. 버튼과 안내 그룹을 서로 다른 자식으로 둔다. 버튼을 숨겨지는 그룹 안에 두면 첫 클릭을 받을 수 없다. 창의 가로·세로를 바꾸고 버튼의 네 가장자리에서 다시 눌러 배치를 확인한다.

## 텍스트, 이미지, 동적 상태

**UI Image**의 **Sprite**를 선택하면 PNG가 영역에 그려진다. 단색 패널은 **Fill**로 색을 준다. **UI Text**에서는 **Size**, **Color**, **Inset X / Y**를 지정한다. 하나의 오브젝트에 이미지·텍스트·버튼을 함께 붙여 배경과 표시 문구를 구성할 수 있다.

스크립트는 `tick.Ui`, `contact.Ui`, `click.Ui`에서 `SetText(id, text)`, `SetVisible(id, visible)`, `SetInteractable(id, enabled)`를 호출할 수 있다. 대상은 Inspector에 연결한 오브젝트 GUID다. 런타임 변경은 Play 세션에 적용된다. `TextOf(id)`는 지금 그리는 문구(텍스트 필드는 값)를, `Bounds(id)`는 이번 프레임의 위젯 사각형(게임 픽셀)을 돌려준다. `Focused`, `FocusWidget(id)`, `ClearFocus()`로 키보드·패드 포커스를 읽고 옮긴다. [KeyRoom의 UI가 들어 있는 씬](../../samples/KeyRoom/Scenes/KeyRoom.scene)

## 자동 배치와 스크롤

**Hierarchy → UI → Layout** 아래에 위젯을 넣으면 Hierarchy 순서대로 배치된다. **Axis**에서 세로 또는 가로를 선택하고 **Padding**으로 가장자리 여백, **Gap**으로 자식 사이 간격을 정한다. **Stretch children**은 진행 방향에 수직인 축을 부모의 안쪽 폭 또는 높이에 맞춘다. 숨긴 자식은 간격도 차지하지 않는다. 레이아웃 안에서 자식의 Transform 크기는 기본 크기로 쓰고 위치는 레이아웃이 결정한다.

**Axis → Grid**는 자식을 같은 크기의 칸에 왼쪽 위부터 한 줄씩 채운다. **Columns**에 한 줄의 칸 수를 넣는다. **Stretch children**을 켜면 칸 폭은 안쪽 폭을 칸 수로 나눈 값이고 높이는 가장 높은 자식에 맞춘다. 끄면 각 자식의 크기를 그대로 쓴다. **Axis → Wrap**(또는 Columns 0)은 가장 넓은 자식이 들어가는 만큼 한 줄에 놓고 나머지는 다음 줄로 넘긴다. 패널 폭이 줄면 줄당 칸 수도 줄어든다. 두 축 모두 세로로 스크롤되고, 방향키·D-pad는 화면에 보이는 칸 배치를 따라 위아래·좌우로 이동한다.

## 가방 칸 끌어 놓기

{{InventoryLab-BagLedger}}

*InventoryLab 가방과 장부 쇼케이스. 가방 칸을 끌어 아이템을 옮기고 바꾸며, 오른쪽 장부는 1,000행을 행 오브젝트 몇 개로 그리면서 열 머리글로 정렬한다.*

[InventoryLab](../../samples/InventoryLab/)의 **Scenes/ShowcaseStock.scene**을 열고 Play를 누른다(약초 마당에서 **C**로도 들어온다). 왼쪽 **Bag grid**는 Grid 4열 레이아웃이고 칸 20개는 **UI Button → Drag and drop slot**을 켠 버튼이다.

| 조작 | 결과 |
| --- | --- |
| 칸을 누른 채 4 px보다 많이 끌어 다른 칸 위에서 놓기 | 놓은 칸에 `OnUiDrop`. 끄는 동안 칸 사본이 포인터를 따라온다. 칸이 아닌 곳에서 놓으면 아무 일도 없다 |
| 칸 클릭, Enter·Space, 패드 아래쪽 버튼 | 처음은 칸을 집고(금색 테두리), 다른 칸에서 다시 하면 그 칸에 놓는다. 집은 칸에서 다시 하면 내려놓는다 |
| 방향키, D-pad | 칸 사이로 포커스를 옮긴다. 칸을 집은 채 옮기면 샘플의 아이템이 포커스 칸 위에 떠서 따라온다 |
| Esc, 패드 오른쪽 버튼 | 집은 칸을 취소한다. 포커스는 그대로 둔다 |

끌어 놓기 칸은 `OnUiClick`을 보내지 않는다. `OnUiDrop(UiDrop drop)`은 놓은 칸(`drop.Target`)의 컴포넌트가 받고, 그 칸에 스크립트가 없으면 스크립트가 있는 가장 가까운 부모가 받는다. 샘플은 Bag grid 오브젝트의 [BagSlots](../../samples/InventoryLab/Scripts/Showcase/BagSlots.cs) 하나가 모든 칸의 놓기를 받아 두 칸의 글자를 바꾼다. 샘플은 바꾸기 전에 두 아이템을 떠 있는 사본으로 새 칸까지 날려 보내는 연출을 더했고, 핵심은 아래와 같다.

```csharp
public override void OnUiDrop(UiDrop drop)
{
    string from = drop.Ui.TextOf(drop.Source) ?? "", to = drop.Ui.TextOf(drop.Target) ?? "";
    drop.Ui.SetText(drop.Source, to);
    drop.Ui.SetText(drop.Target, from);
}
```

Grid·Wrap 레이아웃이나 끌어 놓기 칸이 있는 씬은 schemaVersion 41 이상으로 저장된다. 둘 다 쓰지 않는 씬은 이전 버전과 같은 키로 저장된다. [검증 기록](../../engine/tests/results/engine-features-4/README.md)

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

버튼 누름과 해제를 같은 버튼 안에서 마치면 클릭이 발생한다. 다른 방향으로 끌어 놓으면 취소된다. 고대비 설정을 켜면 접근성 색상과 포커스 테두리를 우선한다. KeyRoom 오른쪽 아래 버튼의 **UI Rect → Scale width and X with text**는 글자 배율에 따라 폭과 앵커 기준 가로 오프셋을 함께 조절한다. [KeyRoom 테마 테이블](../../samples/KeyRoom/Assets/KeyRoomTheme.table)

### 늘어나는 안내 패널

**UI Image → Sprite**에서 PNG를 선택한 뒤 **Mode → 9-slice**를 켠다. **L / R**에는 왼쪽과 오른쪽, **T / B**에는 위와 아래 테두리의 원본 PNG 픽셀 수를 입력한다. 서로 마주 보는 두 테두리 사이에는 최소 한 픽셀의 중심 영역이 있어야 한다.

KeyRoom의 **Guide panel**은 24×24 PNG에 네 방향 8픽셀 테두리를 쓴다. 모서리는 게임 해상도 기준 크기를 유지하고, 네 변은 길이 방향으로, 가운데는 가로·세로로 늘어난다. 창 전체의 확대·축소는 게임과 같이 적용된다. 목적지가 양쪽 테두리보다 작아지면 양쪽을 비례 축소한다. **UI Layout**의 패딩은 테두리 안쪽 여백을 정하고, **UI Text → Wrap lines**는 번역 길이와 글자 배율에 맞춰 행 높이를 정한다. 그 결과로 계산한 버튼 사각형이 그리기와 입력 판정에 함께 사용된다. [원본 PNG](../../samples/KeyRoom/Assets/GuideFrame.png) · [방 안내 씬](../../samples/KeyRoom/Scenes/ShowcaseGuide.scene) · [Mac 실행 기록](../../engine/tests/results/ui-theme/README.md)

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

**Project 패널 → + → Import Assets…**로 TTF·OTF를 추가한다. **UI Accessibility…**의 **Default font**를 지정하고 **+ Fallback font**로 대체 글꼴을 순서대로 추가한 뒤 **Save defaults**를 누른다. 엔진은 텍스트를 NFC로 정규화하고 글자마다 필요한 글리프를 가진 첫 번째 글꼴을 사용한다(SDL_ttf `TTF_AddFallbackFont`). 대체 글꼴의 글자도 기본 글꼴과 같은 밑선에 놓인다. 게임 글꼴을 지정하면 배포 대상 OS에 설치된 글꼴과 관계없이 같은 순서를 사용한다. 글꼴 에셋을 지정하지 않은 프로젝트는 기존 시스템 글꼴을 사용한다. Release와 Native AOT 내보내기에는 선택한 테이블, 글꼴, 같은 이름의 `.license` 파일이 함께 들어간다.

KeyRoom에서 **I**로 가방을 열고 **L** 또는 언어 버튼으로 영어와 한국어를 전환한다. 가방을 닫고 시작점에서 왼쪽의 열쇠를 주운 뒤 다시 열면 같은 아이템 이름이 의뢰 완료 문구에도 쓰인다. 샘플의 기본 글꼴은 고운돋움 부분 글꼴이며 ‘가방’을 포함한다. ‘휴대용’의 ‘휴’와 ‘황동’의 ‘황’은 대체 글꼴인 나눔고딕에서 가져온다. 두 글꼴은 SIL Open Font License로 배포된다. [고운돋움 원본](https://github.com/google/fonts/tree/main/ofl/gowundodum) · [나눔고딕 원본](https://github.com/google/fonts/tree/main/ofl/nanumgothic)

## 서식 있는 텍스트와 글자 단위 표시

**UI Text → Rich text tags**를 켜면 **Text**, 번역 테이블 문구, `SetText`로 넣은 문자열에서 아래 태그를 읽는다. 태그를 닫지 않거나 모르는 태그·색·아이콘 이름은 글자 그대로 표시되어 오타를 찾을 수 있다. 끄면 `<`와 `>`는 일반 글자다.

| 태그 | 결과 | 예시 |
| --- | --- | --- |
| `<color=#RRGGBB>…</color>` | 글자색. 나머지는 **Color**를 쓴다 | `<color=#9BE27A>새 잎</color>` |
| `<b>…</b>` | 굵게 | `<b>약초상</b>` |
| `<size=N>…</size>` | 글자 크기(px). **Size**와 같은 단위이며 표시 설정의 글자 배율을 따른다 | `<size=24>세 포기</size>` |
| `<icon=name>` | **Icons**에 연결한 PNG를 글자 크기로 그린다 | `<icon=herb>` |

**Icons**에는 한 줄에 `이름=프로젝트 PNG 경로`(예: `herb=Assets/Icons/Herb.png`)를 쓴다. 저장 시 에셋 ID로 바뀌므로 PNG를 옮겨도 연결이 유지되고, 내보내기에 해당 PNG가 들어간다. 한 줄에 크기가 다른 글자가 섞이면 가장 큰 글자에 맞춰 줄 높이를 정하고 글자 밑선을 맞춘다. **Wrap lines**를 켠 서식 텍스트는 일반 텍스트처럼 띄어쓰기에서 줄을 바꾼다. 한국어는 어절 단위로 넘어가고, 한 줄보다 긴 어절은 글자 사이에서 끊되 마침표·쉼표·닫는 괄호로 줄을 시작하지 않는다.

**Reveal /s**에 초당 글자 수를 넣으면 표시 문구가 바뀔 때마다 첫 글자부터 한 자씩 나타난다. 띄어쓰기와 아이콘도 한 글자로 센다. 줄 배치는 처음부터 전체 문장 기준이라 표시 도중 줄이 바뀌지 않는다. 같은 문구를 다시 넣으면 이어서 표시하고, 위젯이나 부모 패널을 숨겼다가 보이면 처음부터 다시 표시한다. 진행은 UI 시간으로 계산하므로 일시 정지 중에도 진행되고 입력 재생에서 같은 프레임에 같은 글자 수가 나온다. Scene 편집 화면은 전체 문구를 보여 준다.

```csharp
if (tick.Input.Pressed("Skip")) tick.Ui.SkipReveal(Line);   // 남은 글자를 한 번에 표시, 이미 끝났으면 false
var progress = tick.Ui.RevealProgress(Line);               // Shown / Total / Complete
```

굵기와 크기는 같은 문구 텍스처로 만든다. 굵은 글자는 같은 텍스처를 글자 크기의 1/20만큼 옮겨 한 번 더 그리고, 크기 태그는 32 px로 그린 문구를 늘이거나 줄인다. 32 px보다 큰 크기는 확대되어 흐려질 수 있다. 글자 위치와 줄 바꿈은 SDL_ttf가 HarfBuzz로 배치한 글자 묶음(`TTF_Text`)의 위치를 쓰므로, 커닝이 있는 문장에서도 색이 바뀌는 지점과 부분 표시의 경계가 실제 글자와 맞는다. 부분 표시는 이미 만든 문구 텍스처를 잘라 그리므로 글자가 늘어날 때마다 새 텍스처를 만들지 않는다. [InventoryLab 약초상 대화](dialogue.md#서식-있는-대사와-글자-단위-표시) · [검증 기록](../../engine/tests/results/rich-text/README.md)

## 월드에 붙는 이름표와 데미지 숫자

화면에 고정되는 UI Text와 달리 **Text 2D**는 월드 오브젝트에 붙어 카메라를 따라 움직이고 확대된다. 일반 오브젝트(Canvas 밖)를 선택하고 **Add Component → Rendering → Text 2D**를 붙인다.

1. [InventoryLab](../../samples/InventoryLab/)을 열고 Play를 누른다(시작 씬 **Scenes/Showcase.scene**). 약초상 머리 위 이름표(**Herbalist name**)는 **Text key**에 `label.herbalist`를 넣어 번역 테이블에서 기본 언어의 ‘Herbalist’를 읽는다(`ko`는 ‘약초상’). 키를 찾지 못하면 **Text**를 쓴다. 이름표와 간판 뒤의 판은 HUD 패널 그림을 **Draw mode → Sliced**로 늘린 스프라이트다.
2. 진열대 앞의 간판(**Stall sign**)을 선택한다. **Rich text tags**를 켜면 UI Text와 같은 `<color>`, `<b>`, `<size>` 태그를 읽는다. `<icon>` 태그는 Text 2D에서 글자 그대로 표시된다.
3. **Size**는 Transform 배율 1에서의 월드 단위 글자 크기다. 오브젝트의 Scale과 카메라 Zoom을 함께 따른다. 회전은 적용하지 않는다. **Align**은 오브젝트 사각형 안의 가로 위치(Left·Center·Right)이고, 세로는 사각형 가운데에 둔다.
4. **Layer / Order**와 **Use foot Y**는 Sprite Renderer와 같은 [앞뒤 정렬](sprites.md#발-위치로-앞뒤-정렬하기)에 참여한다. 같은 오브젝트에 Sprite Renderer가 있으면 스프라이트의 정렬 값을 함께 쓴다.

스크립트는 `tick.Objects.SetWorldText(id, text)`로 문구를 바꾼다. 바꾼 문구는 번역 키보다 우선하며 씬이 끝날 때까지 유지된다. 샘플의 `HerbPickup`은 약초를 주울 때 풀에서 `Damage number` 프리팹을 빌려 `+1`을 넣는다. 숫자는 [RisingNumber](../../samples/InventoryLab/Scripts/Labels/RisingNumber.cs)가 60틱 동안 30 단위 올린 뒤 풀로 돌려주고, 다음 약초는 돌아온 같은 오브젝트를 다시 빌린다. [HerbPickup.cs](../../samples/InventoryLab/Scripts/Showcase/HerbPickup.cs)

```csharp
var id = objects.Rent(Pop, at + new Vector2(PopX, -8));
objects.SetWorldText(id, "<b>+1</b>");   // 문자열 상수: 주울 때마다 새 문자열을 만들지 않는다
```

글자는 화면 텍스트와 같은 SDL_ttf 문구 캐시를 쓴다. 같은 문자열은 텍스처 하나를 공유하고, 문구가 그대로인 동안에는 그리기에서 메모리를 할당하지 않는다. `world-text` 엔진 검사에서 데미지 숫자 100개를 이 Mac의 소프트웨어 렌더러로 그릴 때 한 프레임(단계 + 그리기 + 표시) 중앙값은 약 0.25 ms였다. 캐시는 서로 다른 문자열 128개까지 보관하므로, 한 화면에 그보다 많은 다른 문구를 동시에 띄우면 매 프레임 다시 만든다. Text 2D가 하나라도 있는 씬은 schemaVersion 40 이상으로 저장되고, 빌드는 SDL_ttf를 포함한다. [검증 기록](../../engine/tests/results/engine-features-4/README.md)

## 플레이어 표시 설정과 탐색

{{AccessLab}}

*AccessLab 쇼케이스. 키보드만으로 이름을 입력하고, 언어를 영어·일본어·한국어로 바꾸고, 글자 크기 200%에서 패널이 다시 배치되는 것을 보인 뒤 고대비와 흔들림·번쩍임 줄이기를 켜고 저장한다.*

**Project → UI Accessibility…**에서 언어 태그, 글자 크기 **75–200%**, 고대비를 설정한다. 편집 모드의 **Save defaults**는 프로젝트의 `GameSettings`에 저장하고, **Apply in Play**는 현재 세션만 바꾼다. 게임 안에서도 `click.Ui.Preferences.Locale`, `TextScale`, `HighContrast`를 바꾸면 화면에 반영되고 씬을 전환해도 세션 동안 유지된다. 사용자별로 다음 실행에 복원하려면 `Preferences.SaveTo(settings)`로 [플레이어 설정](player-data.md)에 쓰고 시작할 때 `LoadFrom(settings)`을 호출한다. 같은 객체의 `ShakeScale`, `FlashScale`, `Rumble`은 [화면 흔들림·깜빡임](cameras.md#흔들림과-깜빡임-줄이기)과 [게임패드 진동](input-actions.md#게임패드-진동과-마우스-커서)을 줄이거나 끈다.

| 조작 | 키보드 | 게임패드 |
| --- | --- | --- |
| 순서대로 / 역순으로 이동 | Tab / Shift+Tab | — |
| 화면상의 방향으로 이동 | 방향키 | D-pad / 왼쪽 스틱 |
| 버튼 활성화 / 입력 시작 | Enter 또는 Space | 아래쪽 페이스 버튼(A / Cross) |
| 입력 끝내기 | Enter, Esc 또는 Tab | 아래쪽 버튼으로 끝내기, 오른쪽 버튼으로 포커스 해제 |

포커스 링으로 현재 위젯을 표시한다. 비활성·숨김 항목은 건너뛴다. 게임플레이가 있는 씬에서는 먼저 Tab이나 클릭으로 UI에 포커스를 주고, UI 전용 씬에서는 방향키·패드로 바로 탐색할 수 있다. [AccessLab 프로젝트](../../samples/AccessLab/)에는 한국어·일본어·영어로 바꿀 수 있는 설정 화면과 [설정 스크립트](../../samples/AccessLab/Scripts/Showcase/OptionSetting.cs)가 있다.

인벤토리 위에 확인창을 겹쳐 열고 각 단계의 입력과 포커스를 관리하려면 UI Rect에서 **Input mode root**를 켠다. [입력 모드 전환과 복귀](input-actions.md#입력-모드-전환과-복귀)

## 슬라이더·토글·드롭다운

**Hierarchy → 우클릭 → UI**에서 **Slider**, **Toggle**, **Dropdown**을 추가한다. Canvas 아래 배치하고 **UI Rect**와 Transform에서 공통 위치와 크기를 조절한다. Inspector의 **Style**은 버튼과 같은 테마 테이블을 사용한다. **Interactable**을 끄면 포커스와 입력을 받지 않는다.

| 위젯 | Inspector 설정 | 마우스 | 키보드·게임패드 |
| --- | --- | --- | --- |
| Slider | Minimum, Maximum, Step, Value | 트랙을 클릭하거나 손잡이를 끈다. | 포커스 후 좌우 방향키·D-pad·왼쪽 스틱으로 한 단계씩 바꾼다. |
| Toggle | Default | 클릭해 켜거나 끈다. | Enter·Space 또는 패드 아래쪽 버튼으로 전환한다. |
| Dropdown | Options에 한 줄당 항목 하나, Index에 0부터 시작하는 기본 순번 | 클릭해 목록을 열고 원하는 항목에서 놓는다. 바깥을 누르면 닫힌다. | Enter 또는 패드 아래쪽 버튼으로 열고 위아래로 고른 뒤 같은 버튼으로 확정한다. Esc 또는 패드 오른쪽 버튼으로 취소한다. |

게임 스크립트는 `IGameUi`의 `SliderValue(id)`, `ToggleValue(id)`, `SelectedIndex(id)`로 현재 값을 읽는다. `SetSliderValue`, `SetToggleValue`, `SetSelectedIndex`는 실행 중 값만 바꾼다. 슬라이더는 지정한 간격에 맞추고 드롭다운은 범위 밖 인덱스를 거부한다.

위젯에 붙인 컴포넌트는 `OnUiValueChanged(UiValueChanged change)`에서 사용자의 변경을 받는다. `change.Value`는 슬라이더의 수치, 토글의 0 또는 1, 드롭다운의 선택 인덱스다. 저장값을 복원하는 스크립트 호출은 이 이벤트를 발생시키지 않는다. 게임은 필요한 값만 플레이어 설정에 저장한다. [AudioLab의 믹서 연결](../../samples/AudioLab/Scripts/ListeningWalk.cs) · [플레이어 저장](player-data.md)

## 데이터로 채우는 목록

[InventoryLab](../../samples/InventoryLab/)을 열고 **Save & Run**으로 실행한 뒤 약초 마당에서 **C**를 누른다. 재고 씬 오른쪽 장부 목록에 아이템 1,000개가 표시되고 목록이 키보드·패드 포커스를 받는다. 방향키·D-pad·왼쪽 스틱으로 아이템을 고르고 **R** 또는 패드 위쪽 버튼(Y)으로 번호순·이름순·수량순을 전환한다. 고른 순서의 열 제목이 노랗게 바뀐다. Enter·Space·패드 아래쪽 버튼 또는 클릭으로 선택한 아이템을 하나 사용하면 그 행의 수량이 줄고, 사용한 번호·정렬·행 수가 저장 슬롯 2에 기록된다. 정렬 후에도 선택한 아이템이 그대로 선택되고, 사용 대상도 그 아이템의 ID(번호)다. **B** 또는 패드 Back 버튼으로 약초 마당으로 돌아간다. [재고 씬](../../samples/InventoryLab/Scenes/ShowcaseStock.scene)

**UI → Scroll View** 아래 첫 번째 자식이 행 템플릿이다. 버튼과 텍스트로 한 행의 모양을 만들고 텍스트에 `{name}`, `{count}`처럼 필드 이름을 중괄호로 넣는다. 게임을 실행하기 전에는 템플릿 한 줄만 보인다. 실행 중에 데이터를 연결하면 템플릿이 숨겨지고 화면을 채울 만큼의 복사본만 만들어진다. 스크롤해도 화면 밖으로 나간 행에 다음 아이템을 다시 채우므로 행 수가 늘지 않는다. 장부의 270 높이 목록은 아이템 수와 관계없이 행 9개를 사용한다. 테마 스타일, 9-slice, 번역 키도 템플릿 설정을 그대로 따른다.

게임 스크립트는 `IUiListSource`를 구현한다. `Count`는 현재 순서의 항목 수, `ItemId(index)`는 정렬해도 바뀌지 않는 ID, `Field(index, name)`은 중괄호 안의 필드 값을 반환한다. Scroll View 오브젝트에서 다음을 호출한다.

| 호출 | 동작 |
| --- | --- |
| `BindList(list, source)` | 데이터를 연결한다. 세로 Scroll View에 행 템플릿이 있어야 한다. |
| `RefreshList(list)` | 정렬·필터·수량 변경 후 화면을 다시 채운다. 선택한 ID가 남아 있으면 새 위치로 스크롤하고 포커스도 옮긴다. 빠졌으면 같은 순번의 이웃을 선택한다. |
| `SelectedItem(list)` · `SelectItem(list, id)` | 선택한 ID를 읽거나 스크립트로 선택한다. |
| `FocusWidget(list)` | 선택한 아이템, 없으면 첫 아이템의 행에 포커스를 준다. |
| `ListRowCount(list)` | 만들어진 행 수를 반환한다. |

위아래 이동은 화면의 행이 아니라 데이터 순서를 따르고, 포커스가 뷰 밖으로 나가면 목록이 따라 스크롤한다. 첫 항목에서 위, 마지막 항목에서 아래로 가면 주변 위젯으로 포커스가 나간다. 다른 위젯에서 목록으로 돌아오거나 휠로 멀리 스크롤한 뒤 방향 입력을 하면 마지막에 선택한 아이템으로 돌아온다. 행을 클릭하거나 패드로 누르면 Scroll View에 붙인 스크립트의 `OnUiClick`이 호출되고 `click.ObjectId`는 목록 오브젝트다. 사용 대상은 `click.Ui.SelectedItem(click.ObjectId)`로 읽는다. [장부 스크립트](../../samples/InventoryLab/Scripts/Showcase/LedgerList.cs)

지금은 세로 목록만 지원하며 모든 행의 높이가 같다고 가정한다. 행 간격은 템플릿 높이와 **UI Layout → Gap**으로 정한다.

## 데스크톱 화면 설정

[DisplayLab](../../samples/DisplayLab/)을 열고 **Save & Run**으로 독립 게임 창을 실행한다. 창 크기를 바꾼 뒤 **Borderless fullscreen**을 켰다가 끄면 이전 창 크기로 돌아온다. **Vertical sync**로 VSync를 바꿀 수 있다. **Clicks** 버튼은 누를 때마다 횟수를 표시한다. 여백을 눌렀을 때도 숫자가 그대로인지 직접 확인할 수 있다.

게임 스크립트는 `Session.Display`에서 독립 창의 상태를 읽는다. Scene 패널과 헤드리스 실행에서는 이 값이 `null`이다. `SetFullscreen(bool)`과 `SetVSync(bool)`는 적용 성공 여부를 반환한다. 게임은 `Fullscreen`과 `VSync`에서 실제 적용값을 읽어 위젯을 갱신할 수 있다. 창 관리자나 렌더러가 요청을 거절하면 DisplayLab은 안내 문구와 현재 상태를 표시한다. [DisplayLab 스크립트](../../samples/DisplayLab/Scripts/DisplayOption.cs)

엔진은 게임별 프로필의 `display.settings`에 창 모드의 콘텐츠 너비·높이, 전체 화면 여부, VSync를 저장한다. 크기 단위는 SDL 창 좌표다. 파일이 없으면 프로젝트 해상도의 창, VSync 켬으로 시작하고, 1–32768 밖의 크기는 무시한다. 논리 해상도와 화면 비율은 게임 프로젝트가 정하며, 크기가 다른 창에서는 레터박스를 적용한다. 정수 배율을 켠 프로젝트는 배율을 정수로 내림하고 나머지를 여백으로 둔다([정수 배율](runtime.md#월드-시간-카메라)). DisplayLab 오른쪽 위의 12×12 픽셀 배지로 보간 여부를 확인할 수 있다.

전체 화면은 현재 디스플레이 모드를 유지하는 SDL의 기본 테두리 없는 전체 화면을 사용한다. VSync는 렌더러에서 읽어 온 동기화 간격으로 확인한다. [SDL 전체 화면 API](https://wiki.libsdl.org/SDL3/SDL_SetWindowFullscreen) · [SDL VSync API](https://wiki.libsdl.org/SDL3/SDL_SetRenderVSync)
