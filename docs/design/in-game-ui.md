# MoNoon 인게임 UI: 화면을 기준으로 만드는 Canvas

게임 UI의 기준은 월드 좌표가 아니라 **플레이어가 보는 게임 화면**이다. 카메라가 문으로 전환돼도 열쇠 표시는 왼쪽 위에 남고, 오른쪽 위의 버튼은 보이는 자리에서 눌려야 한다. 타이틀·메뉴처럼 UI만 있는 씬은 카메라 없이 실행하고 빌드할 수 있다. 씬의 오브젝트·프리팹·스크립트는 그대로 쓰되 배치, 그리는 순서, 입력은 월드와 분리한다. 에디터의 Avalonia 컨트롤을 배포 게임에 넣지 않는다.

## 만들고 편집하기

Hierarchy에서 우클릭 → **UI → Canvas / Group / Image / Text / Button**을 선택한다. 같은 메뉴에 Layout, Scroll View, Text Field, Slider, Toggle, Dropdown도 있다([사용법](../manual/game-ui.md)). UI 요소 아래에서 만들면 자식이 되고, 루트에서 위젯을 만들면 첫 Canvas 아래에 배치한다(없으면 같이 생성). **Add Component**로 기존 오브젝트를 위젯으로 만들 수도 있다. UI 위젯은 월드 스프라이트 대신 화면에 그려지므로 물체 위에 나타내고 싶은 설명은 별도 UI 오브젝트로 만든다.

| 구성 요소 | 맡는 일 |
| --- | --- |
| **UI Canvas** | 게임 해상도 전체를 차지하는 루트. Order가 큰 Canvas가 나중에 그려진다. |
| **UI Rect** | 부모의 어느 지점에 붙을지 정한다. 각 축은 아홉 가지 Anchor의 위치와 Offset·Size를 쓰거나, 부모의 변에 논리 픽셀 여백을 고정한다. Show if가 비어 있으면 항상 보이고, 세션 플래그 이름이 있으면 그 값이 켜졌을 때만 자식까지 표시한다. Group은 Rect만 가진 오브젝트다. |
| **UI Image** | 프로젝트의 PNG GUID를 참조한다. PNG가 없으면 Fill 색으로 사각형을 그리므로 패널 배경으로도 쓸 수 있다. |
| **UI Text** | 텍스트, 글자 크기와 색, 왼쪽·위 안쪽 여백을 정한다. Wrap lines를 켜면 폭에 맞춰 줄을 바꾸고, 번역 키·서식 태그·글자 단위 표시를 쓸 수 있다. |
| **UI Button** | Rect가 클릭 영역이다. 포커스 테두리와 누름 상태가 있고 Interactable을 끄면 입력에서 제외된다. Button 프리셋은 UI Text도 붙여 준다. |

Inspector에서 앵커와 크기를 고르고, Scene의 시작 카메라 프레임 안에서 위젯을 드래그해 위치를 잡는다. 편집 중에는 조건부 UI도 보이며, Play에서는 실제 플래그 값을 따른다. 씬 저장·복제·Undo는 월드 오브젝트와 같은 흐름이다. Play 중의 클릭과 플래그 변경은 씬에 저장되지 않는다.

## 배치·그리기·입력의 계약

- 좌상단은 (0,0), 단위는 게임의 논리 픽셀이다. 변이 고정되지 않은 축의 시작 위치는 `부모 시작 + (부모 크기 − 내 크기) × 앵커 비율 + Offset`이다. 비율은 0, 0.5, 1 중 하나다. KeyRoom의 버튼은 800×450 게임에서 TopRight, 너비 88, X=-14이므로 왼쪽이 698이고 오른쪽 여백은 14다. 일반 창 리사이즈는 전체 게임을 비율에 맞춰 확대하고, 프로젝트 해상도를 바꾸면 새 크기로 앵커를 다시 계산한다.
- 마주 보는 두 변을 고정한 축은 부모 크기에서 두 여백을 뺀 길이를 사용한다. 한 변만 고정하면 해당 변에 측정된 크기의 위젯을 붙인다. 음수 여백과 0 크기를 지원한다. [Inspector와 미리보기 사용법](../manual/game-ui.md#가장자리-고정과-해상도-미리보기)
- 월드를 먼저 그리고 UI를 그린다. 같은 Order에서는 계층 순서를 유지하며, 노드 안에서는 버튼 배경 → 이미지 → 텍스트 → 포커스 테두리 순이다. 클릭은 반대 순서로 **최상단의 보이고 활성인 버튼** 하나에만 간다. 이미지·문구는 클릭을 가로채지 않는다.
- 독립 SDL 창은 창 좌표를 렌더러의 논리 좌표로, 에디터 Play는 Scene 패널의 표시 영역을 게임 좌표로 변환한다. 검은 여백은 UI 밖이다. 같은 버튼에서 눌렀다 놓아야 클릭이고 포커스를 잃으면 취소한다. Tab/Shift+Tab으로 버튼을 선택하고 Enter/Space로 실행한다. 소비한 키는 게임의 Interact로 다시 보내지 않는다.
- 버튼 입력은 다음 고정 틱에 같은 오브젝트의 게임 스크립트로 전달한다. [KeyRoom의 ToggleControls.cs](../../samples/KeyRoom/Scripts/ToggleControls.cs)처럼 `GameBehaviour.OnUiClick(UiClick)`을 오버라이드한다. 스크립트가 `controls` 플래그를 전환하고 도움말 Rect가 그 플래그를 읽는다. 씬을 바꾸면 버튼 포커스는 초기화되고, [세션](../../engine/src/Engine.Core/GameSession.cs)의 플래그는 유지된다.

체력이나 점수처럼 매번 달라지는 값은 스크립트에서 `tick.Ui.SetText(labelId, "HP 12")`로 갱신한다. `SetVisible`로 그룹 전체를 숨기고 `SetInteractable`로 버튼을 끌 수 있다. 세 함수는 변경할 대상이 없으면 false를 반환한다. 변경은 현재 씬의 실행 상태에만 적용되며, 숨겨진 부모 아래에서는 자식이 입력도 받지 않는다.

## 저장과 검증

[ScreenUi](../../engine/src/Engine.Core/ScreenUi.cs)가 편집 미리보기와 실행 중 배치·그리기·클릭 영역을 계산한다. `.scene`·프리팹에 VYaml 컴포넌트를 저장하고, PNG는 `.meta`의 기존 GUID로 참조한다. [게임 빌더](../../engine/src/Engine.Authoring/GameBuild.cs)가 UI PNG와 버튼 스크립트를 배포본에 포함한다. 배포 게임의 글자는 플레이어가 `Brutal.Sdl.Ttf`(SDL_ttf)로 배포한 TTF·OTF에서 그리며, 텍스트를 쓰는 UI가 없는 게임은 SDL_ttf를 빼고 빌드한다. Avalonia와 SkiaSharp는 배포본에 들어가지 않는다.

초기 Canvas 형식은 v7에서 도입했다. 이후 기능은 쓸 때만 버전을 올린다: 자동 배치·스크롤·텍스트 필드·줄바꿈 v14, 번역 키 v23, 버튼 테마·9-slice v25, 슬라이더·토글·드롭다운 v26, 독립적인 가장자리 고정 **v28**, 월드 Text 2D v40, Grid·Wrap 배치와 끌어 놓기 칸 v41. v6의 `hud`는 읽을 때 같은 오브젝트 ID의 Rect·Text와 선택적 Image로 바꾼다. 이미지 너비와 글자 사이 6픽셀, 글자 크기 16, 표시 플래그를 보존한다. 열기만 해서는 기존 씬 파일을 다시 쓰지 않으며 다음 저장부터 새 형식이다. 새 컴포넌트 목록에는 예전 HUD가 나오지 않는다.

[KeyRoom 쇼케이스](../../samples/KeyRoom/Scenes/Showcase.scene)에서 **Controls 클릭 → 안내 표시 → Enter → 안내 숨김**을 확인할 수 있다. 열쇠를 먹은 뒤에는 시점이 바뀌어도 열쇠 아이콘이 유지되고, 다음 방(ShowcaseRoom2)에서도 표시된다. [통합 검사](../../engine/tests/Engine.Checks/UiChecks.cs)에서는 앵커, 겹친 위젯 선택, 저장 형식 호환을 확인하고, [게임 검사](../../engine/tests/Engine.Checks/Program.cs)에서는 버튼 취소·중복 실행 방지와 두 방 진행을 확인한다.

[Mac 에디터·Release·Native AOT 및 Windows·Linux x64 Release의 실제 실행 기록과 화면](../../engine/tests/results/screen-ui/README.md)

**이후 구현**: 줄바꿈과 폰트 에셋·번역 테이블, 자동 배치·스크롤, 입력 모드 스택(모달 포커스), 키보드·패드 탐색, 월드에 붙이는 이름표(Text 2D)는 [게임 화면 UI](../manual/game-ui.md)와 [입력 모드 전환](../manual/input-actions.md#입력-모드-전환과-복귀)에 있다. 터치 탐색은 없다. 버튼이 메뉴를 열더라도 월드 시뮬레이션은 자동으로 멈추지 않는다. 멈추려면 스크립트에서 [시간 제어](../manual/time.md)를 쓴다. 구현 경위는 [UI·오디오 구현 기록](../history/ui-audio.md)에 있다.
