# 입력 액션

[매뉴얼 목차](README.md) · [KeyRoom 설정](../../samples/KeyRoom/GameSettings)

게임 스크립트는 키 대신 `Jump`, `ShowControls`처럼 동작의 이름을 읽는다. 여러 입력을 한 동작에 연결하면 게임 코드를 바꾸지 않고 조작법을 바꿀 수 있다.

## 프로젝트에서 지정하기

1. Play를 멈추고 **Project → Input Actions…**를 연다. **+ Action**을 누르고 액션 이름을 입력한다. 이름은 영문자로 시작하고 영문자·숫자·밑줄을 포함해 64자까지 허용한다. 대소문자만 다른 이름은 같은 액션이다.
2. 행의 **+ Bind**를 누른 후 키, 마우스 버튼·휠 또는 패드 버튼·스틱·트리거를 조작한다. 새 입력은 행 끝에 추가된다. **Esc**는 입력 대기를 취소한다. **Controls** 칸에서 쉼표로 직접 편집할 수도 있다. 예를 들어 `H, Mouse.Right, Pad.Start`를 `J, Mouse.Right`로 바꾸면 기존 H와 Start 연결은 사라진다.
3. 빈 Controls는 연결 없는 액션으로 남고, **×**는 액션 자체를 삭제한다. **Save**를 누르면 프로젝트 루트의 YAML 파일 `GameSettings`에 저장된다. 다음 Play와 새로 내보내는 게임 모두 이 맵을 사용한다. 예전 프로젝트의 Left·Right·Up·Down·Interact 설정은 읽을 수 있고, 다음 설정 저장부터 액션 목록으로 기록된다.

| KeyRoom 기본 액션 | 키보드 | 마우스 | 패드 |
| --- | --- | --- | --- |
| MoveLeft / MoveRight / MoveUp / MoveDown | 방향키, WASD | — | D-pad, 왼쪽 스틱 |
| Interact (상자 열기) | E, Space | — | South (아래쪽 버튼) |
| ShowControls (안내 열기·닫기) | H | 오른쪽 버튼 | Start |
| PauseGame (일시정지·재개) | P | — | Back |
| OpenBag (첫 방 인벤토리) | I | Bag [I] 버튼 | West |
| MenuBack (한 단계 뒤로) | Backspace, I | Cancel / Close 버튼 | East |

**Controls** 버튼을 직접 클릭해도 같은 안내가 열린다. KeyRoom은 [ToggleControls.cs](../../samples/KeyRoom/Scripts/ToggleControls.cs)에서 최근 입력 기기의 첫 바인딩을 표시한다. 그 기기에 해당 액션의 바인딩이 없으면 키보드 바인딩을 표시한다.

## 스크립트에서 읽기

`FixedUpdate(ScriptTick tick)`에서 현재 입력 모드에 속한 컴포넌트가 같은 틱의 입력을 읽는다.

| API | 의미 |
| --- | --- |
| `tick.Input.Pressed("Jump")` / `Released("Jump")` | 액션의 첫 입력을 누른 틱 / 마지막 입력을 놓은 틱. 두 키가 연결되어 있어도 하나가 남아 있으면 해제되지 않는다. 틱 사이에 키를 눌렀다 놓으면 두 값 모두 참일 수 있다. |
| `tick.Input.Held("Jump")` / `Value("Jump")` | 현재 눌려 있는지 / 연결된 입력 중 가장 큰 세기(0~1). 버튼은 0 또는 1이며 스틱·트리거는 중간값을 낸다. |
| `tick.Input.Move` / `InteractPressed` | 네 이동 액션을 합친 벡터 / Interact를 누른 순간. Move에는 아날로그 세기가 반영되고 대각선 최대 속도는 일정하다. |
| `tick.Input.PointerPosition` / `PointerInside` | 게임 화면 왼쪽 위를 (0, 0)으로 한 논리 좌표 / 게임 화면 안인지 여부. 아래로 갈수록 Y가 커진다. 검은 여백에서 시작한 클릭은 액션을 누르지 않는다. |
| `tick.Input.ActiveDevice` | 최근 입력 기기: Keyboard, Pointer, Gamepad. 게임 영역 안에서 마우스를 움직여도 Pointer로 바뀐다. |

게임 UI가 먼저 입력을 받는다. UI 버튼을 활성화한 클릭이나 키는 같은 틱에 게임 액션까지 누르지 않는다. Esc는 Play 중지 또는 게임 종료에 쓰인다. 패드는 SDL이 인식한 첫 번째 게임패드 한 대를 사용한다. 스틱은 중심에서 20%, 트리거는 10% 이내의 작은 움직임을 무시한다. 연결을 끊거나 창이 포커스를 잃었을 때 이동이 계속되지 않는다.

## 실행 중 바꾸기

게임 내 설정 화면에서는 `tick.Session.RebindInput("ShowControls", "J", "Pad.Start")`처럼 이미 정의한 액션의 **전체** 바인딩을 교체한다. `tick.Session.BindingFor("ShowControls", tick.Input.ActiveDevice)`는 해당 기기의 첫 바인딩을, 없으면 null을 반환한다. `InputRevision`이 바뀌면 표시 중인 안내를 갱신할 수 있다. 런타임 변경은 현재 게임의 씬 전환 동안 유지되며 다음 실행의 프로젝트 기본값에는 영향을 주지 않는다.

## 입력 모드 전환과 복귀

KeyRoom 첫 방에서 **I** 또는 **Bag [I]**로 인벤토리를 연다. **Manage compass**를 선택하고 **Apply**를 누르면 나침반이 장착되고 HUD에 출구 방향이 표시된다. 다시 적용하면 가방에 넣는다. **Cancel**은 장비 상태를 유지한다. **Backspace**를 한 번 누르면 확인창에서 인벤토리로, 한 번 더 누르면 게임으로 돌아간다. **Tab / Shift+Tab**과 **Enter**로도 모든 버튼을 조작할 수 있다. [KeyRoom 예제 스크립트](../../samples/KeyRoom/Scripts/BagMenu.cs)

1. Hierarchy에서 패널로 쓸 UI Group을 만들고 Inspector의 **UI Rect → Input mode root**를 켠다. 버튼과 해당 모드에서 입력을 읽을 스크립트를 그 아래에 둔다. 확인창도 같은 방식으로 인벤토리 아래에 만들 수 있다. Scene 편집 중에는 Hierarchy에서 패널이나 자식을 선택해 해당 배치를 본다.
2. 여는 버튼의 스크립트에서 `click.Ui.PushInputMode(panelId)`를 호출한다. Play 시작 시 닫혀 있던 패널이 표시되고 처음 사용 가능한 버튼이나 텍스트 필드에 포커스가 간다. `UpdateUi`에서도 같은 API를 사용할 수 있다. 중첩된 패널은 부모 패널이 열려 있어야 한다.
3. 닫을 때는 `click.Ui.PopInputMode(panelId)`를 호출한다. 전달한 ID가 맨 위 패널일 때만 닫히며 이전 포커스가 복원된다. `ActiveInputMode`는 맨 위 패널의 ID이고 게임 기본 입력으로 돌아오면 `null`이다. 두 메서드는 성공 여부를 반환한다.

열려 있는 동안 클릭·스크롤·포커스 탐색과 스크립트의 `tick.Input`은 맨 위 패널과 그 자식에게 전달된다. 뒤쪽 스크립트도 빈 입력으로 업데이트되므로 애니메이션과 물리는 계속 진행된다. 전환에 쓴 클릭이나 누른 채 둔 이동 키는 다음 모드로 이어지지 않는다. 패널을 숨기거나 비활성화·제거하면 그 위에 열린 모드까지 정리한다.

## 월드 오브젝트 클릭과 드래그

KeyRoom 첫 방에서 **Play → F8**을 누르면 `PointerWorkshop`이 열린다. 다시 **F8**을 누르면 첫 방으로 돌아간다. [샘플 씬](../../samples/KeyRoom/Scenes/PointerWorkshop.scene)

1. 파란 상자와 주황 상자가 겹치는 부분에서 버튼 바깥을 클릭한다. 화면 아래에 `Front crate / Click`이 표시된다. 같은 곳을 잡고 움직이면 주황 상자만 따라온다.
2. 마우스 왼쪽 버튼을 누른 채 포인터를 멈추고 **D** 또는 **→**를 누른다. 주 카메라가 움직여도 상자에서 처음 잡은 지점이 포인터 아래에 머문다.
3. 주황 상자를 옆으로 옮긴 뒤 오른쪽 위 **Detail camera**에서 파란 상자를 잡고 작은 화면 밖까지 끌어 본다. 버튼을 놓을 때까지 작은 카메라의 확대율로 움직인다. 상자 위 **UI clicks** 버튼을 누르면 숫자만 올라간다.

오브젝트의 `GameBehaviour`에 `IWorldPointerHandler`를 구현하면 `OnWorldPointer(WorldPointerEvent)`가 호출된다. 왼쪽 버튼을 누른 위치에서 게임 UI를 먼저 검사하고, 그 위치를 표시하는 최상단 카메라에서 렌더링 순서상 가장 앞에 있는 입력 가능한 오브젝트를 찾는다. 맞닿은 영역은 오브젝트의 너비·높이와 월드 변환으로 정해진다. 회전·스케일·부모 변환과 Y 정렬·정렬 그룹이 적용된다. [엔진 API](../../engine/src/Engine.Core/WorldPointer.cs)

`Press`에서 정한 `ObjectId`, `CameraId`, `GrabLocalPosition`은 한 번의 드래그 동안 유지된다. `ScreenPosition`은 게임 해상도 기준 논리 좌표이며 `WorldPosition`은 처음 집은 카메라로 환산한 월드 좌표다. 샘플은 `Drag`마다 현재 변환으로 잡은 지점을 구하고 `TryMoveStep`으로 그 지점과 `WorldPosition` 사이의 차이만큼 이동한다. [DragCrate.cs](../../samples/KeyRoom/Scripts/DragCrate.cs)

버튼을 놓으면 `Release`를 받는다. 누른 지점에서 4 논리 픽셀 미만으로 움직이고 같은 카메라와 오브젝트 위에서 놓으면 이어서 `Click`을 받는다. 포커스를 잃거나 모달 입력 모드가 열리거나 대상이 제거되는 등 조작이 중단된 경우에는 `Cancel`로 정리한다.
