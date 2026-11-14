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

**Controls** 버튼을 직접 클릭해도 같은 안내가 열린다. KeyRoom은 [ToggleControls.cs](../../samples/KeyRoom/Scripts/ToggleControls.cs)에서 최근 입력 기기의 첫 바인딩을 표시한다. 그 기기에 해당 액션의 바인딩이 없으면 키보드 바인딩을 표시한다.

## 스크립트에서 읽기

`FixedUpdate(ScriptTick tick)`에서 같은 틱의 모든 컴포넌트가 같은 입력을 읽는다.

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
