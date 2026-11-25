# 프로젝트 C# 스크립팅

[매뉴얼 목차](README.md) · [KeyRoom으로 확인한 실행 결과](../../engine/tests/results/scripting/README.md)

## 컴포넌트와 공용 파일

**Add Component → New C# Component…**는 선택한 오브젝트에 붙일 스크립트를 만든다. **Project 패널 → + → New Shared C# File…**는 여러 스크립트가 쓸 일반 C# 파일을 만든다. 둘 다 `Scripts/`에 저장되고 더블클릭하면 설정한 외부 에디터가 열린다. `Scripts` 아래의 폴더를 보고 있으면 해당 폴더에 만들어진다.

게임은 `Scripts/**/*.cs`와 Project 패널에서 다른 폴더로 옮긴 게임 스크립트를 함께 컴파일한다. 일반 클래스, 열거형, 정적 함수, 추상 기반 클래스, 네임스페이스를 파일 사이에서 사용할 수 있다. Add Component와 Inspector 드롭은 **처음 지정한 스크립트 ID의 마지막 이름과 같은 공개 클래스**이며, `GameBehaviour`를 상속하고 인수 없는 공개 생성자를 가진 구체 타입만 받는다. 공용 파일도 Project에는 보인다. 공용 파일의 컴파일 오류는 사용 중인 컴포넌트까지 영향을 준다. **Project → Console…**에서 파일과 줄을 확인한다.

KeyRoom의 [LootRules.cs](../../samples/KeyRoom/Scripts/Shared/LootRules.cs)는 상자를 열 수 있는 거리와 획득 플래그 이름을 정의한다. [OpenChest.cs](../../samples/KeyRoom/Scripts/OpenChest.cs)와 [CollectCoin.cs](../../samples/KeyRoom/Scripts/CollectCoin.cs)는 같은 네임스페이스를 가져와 이 규칙을 사용한다.

## 생명주기

다음 메서드는 필요한 것만 `override`한다. `ScriptLife`로 내 오브젝트 ID, 세션, 오브젝트와 UI에 접근한다.

| 콜백 | 발생 시점 |
| --- | --- |
| `OnCreate(ScriptLife life)` | 씬의 첫 고정 업데이트 전 또는 런타임 프리팹 생성 시, 인스턴스당 한 번. Inspector 값이 적용돼 있고 같은 씬·프리팹의 다른 컴포넌트를 찾을 수 있다. |
| `OnEnable(ScriptLife life)` | 처음 활성화되거나 자신과 부모가 다시 활성화될 때. 비활성 부모 아래에 생성되면 부모가 켜질 때까지 기다린다. |
| `FixedUpdate(ScriptTick tick)` | 활성 상태에서 시뮬레이션 틱마다 실행한다. 틱 중에 생성한 프리팹은 그 틱의 구조 변경을 반영한 뒤 업데이트에 참여한다. |
| `OnCollisionEnter/Stay/Exit(ScriptContact contact)`, `OnTriggerEnter/Stay/Exit(ScriptContact contact)` | 물리 틱에서 두 오브젝트의 접촉이 시작되거나 이어지거나 끝날 때. [압력판 예제와 전달 규칙](collision.md#진입유지이탈-이벤트) |
| `OnAnimationEvent(ScriptAnimationEvent frame)` | 같은 오브젝트의 애니메이터가 이름이 지정된 프레임에 진입할 때 호출한다. [파라미터 및 이벤트 예제](sprites.md#파라미터로-애니메이션-전환하기) |
| `OnDisable(ScriptLife life)` | 자신이나 부모가 꺼질 때, 또는 활성 상태에서 제거될 때. 이미 꺼진 오브젝트를 제거하면 다시 호출되지 않는다. |
| `OnDestroy(ScriptLife life)` | 제거, 씬 전환, 정상적인 Play 종료 시 한 번. 제거된 ID를 조회하면 null이므로 종료에 필요한 데이터는 인스턴스 필드에 보관한다. |

부모와 자식을 함께 켜면 부모부터, 끄거나 제거하면 자식부터 처리한다. 같은 활성 상태를 두 번 지정해도 이벤트가 중복되지 않는다. 한 번도 실행하지 않은 씬(예: 리소스 준비가 실패한 전환 대상)은 생명주기를 시작하지 않는다.

## 기다린 뒤 이어서 실행하기

`IEnumerable<ScriptWait>`를 반환하는 메서드에 순서대로 할 일을 쓰고 `yield return`으로 기다릴 조건을 돌려준다. `objects.StartRoutine(소유 오브젝트 ID, 메서드())`는 첫 `yield return`까지 바로 실행하고, 나머지는 조건이 충족된 틱이나 프레임에 이어서 실행한다. `objects`는 콜백의 `life.Objects`, `tick.Objects`, `contact.Objects`, `message.Objects`다.

KeyRoom의 [DoorLock](../../samples/KeyRoom/Scripts/DoorLock.cs)은 문이 열린 뒤 게임 시간 0.5초를 기다려 화면 위쪽에 **The door is open. Head north.**(한국어: **문이 열렸다. 북쪽으로 가자.**)를 띄운다.

```csharp
[GameEventHandler]
public void Unlock(GameEventContext message)
{
    // 문 스프라이트와 충돌체를 바꾼 뒤
    message.Objects.StartRoutine(message.ObjectId, ShowHint(message.Session));
}

private static IEnumerable<ScriptWait> ShowHint(SessionState session)
{
    yield return ScriptWait.Seconds(0.5);
    session.Set("doorHint"); // Door hint UI의 showIf: doorHint가 표시된다.
}
```

| 대기 | 이어서 실행하는 시점 |
| --- | --- |
| `ScriptWait.Seconds(초)` | 게임 시간이 지정한 만큼 흐른 첫 틱. 0.5초는 정확히 30틱이다. 정지 중에는 흐르지 않고 한 틱 진행만큼 흐르며 배속을 따른다. |
| `ScriptWait.UiSeconds(초)` | UI 시간이 지정한 만큼 흐른 첫 화면 프레임. 정지·배속과 관계없이 실제 시간으로 흐른다. |
| `ScriptWait.Frames(틱 수)` | 지정한 수의 게임 틱 뒤. 정지 중에는 세지 않는다. |
| `ScriptWait.Until(() => 조건)` | 매 게임 틱 조건을 확인해 처음 참이 된 틱. |

모든 대기는 시작한 틱보다 뒤에 끝나므로 `Frames(0)`과 `Seconds(0)`은 다음 틱에 이어진다. 게임 시간 대기는 스크립트 `FixedUpdate`와 접촉 이벤트 다음, 구조 변경 반영 전에 시작한 순서대로 이어진다. UI 시간 대기는 각 프레임의 `UpdateUi` 다음에 이어진다. 스레드를 쓰지 않으므로 Scene Play, Release, Native AOT가 같은 입력에서 같은 틱에 실행한다.

소유 오브젝트나 부모가 꺼지거나 제거되면, 그리고 씬이 바뀌거나 Play가 끝나면 대기 중인 실행을 취소한다. 다시 켜도 이어지지 않으며 메서드의 `finally` 블록은 취소 시점에 실행된다. `StartRoutine`이 돌려준 `ScriptRoutine`의 `IsRunning`으로 진행 여부를 읽고 `Stop()`으로 직접 멈춘다. 꺼진 오브젝트로는 시작할 수 없다. 실행 중 예외가 나면 `DoorLock.ShowHint: 메시지` 형식으로 실행 오류를 알린다.

**직접 확인하기** — KeyRoom을 Play하고 왼쪽으로 가서 열쇠를 얻는다. 0.5초 안에 **P**로 정지하면 안내가 나타나지 않는다. 정지한 채 **.**(마침표, 프로젝트의 **StepGame** 액션)을 누를 때마다 한 틱씩 진행되고, **P**로 재개하면 문이 열린 뒤 30틱째에 안내가 표시된다. 에디터 Scene Play에서는 도구 모음의 **Step**도 같은 동작을 한다. 열쇠를 얻은 직후 **F6**으로 문을 제거하면 안내가 표시되지 않는다. [세 실행 경로의 재생 기록](../../engine/tests/results/script-waits/README.md)

## 트윈으로 값 움직이기

`objects.Tween(오브젝트 ID)`가 돌려준 핸들에 단계를 이어 붙이면 순서대로 재생한다. 각 단계는 시작할 때의 현재 값에서 목표 값으로 이동하고, 끝난 단계의 남은 시간은 다음 단계로 넘어가 전체 길이가 정확히 합과 같다. 여러 속성을 함께 움직이려면 같은 오브젝트에 `Tween`을 여러 번 시작한다.

| 단계 | 바꾸는 값 |
| --- | --- |
| `Move(Vector2, 초, ease)` · `Scale(Vector2, …)` · `Rotate(도, …)` | 로컬 Transform(`SetLocalTransform`과 같은 경로) |
| `Color(Color, …)` · `Alpha(0–1, …)` | 스프라이트를 그리는 RGB와 불투명도(클립의 Tint·Opacity 키와 같은 값) |
| `UiRect(RectangleF, …)` | UI 요소의 배치 결과에 더하는 오프셋. X·Y는 이동, Width·Height는 크기 변화이고 자식도 함께 움직인다. `default`로 끝나면 원래 배치로 돌아간다. |
| `Float(시작, 끝, 초, Action<float>, …)` | 임의 값. 매 단계마다 콜백을 부른다. 콜백을 필드에 담아 두면 할당하지 않는다. |
| `Delay(초)` | 아무것도 바꾸지 않고 기다린다. |

`ease`는 Position Tween 2D와 같은 `Linear`·`EaseIn`·`EaseOut`·`EaseInOut`(기본값)이다. 두 번째 인수 `TweenClock.Ui`를 주면 UI 시간으로 재생해 정지 중에도 움직인다. 기본값 `TweenClock.Game`은 게임 시간을 따라 정지·한 틱 진행·배속을 따른다. 게임 시간 트윈은 대기 루틴과 같은 위치(스크립트·접촉 이벤트 다음)에서, UI 시간 트윈은 `UpdateUi` 다음에서 진행한다. 오브젝트가 꺼지거나 제거되면 트윈이 끝나고, 핸들의 `IsPlaying`·`Stop()`으로 상태를 읽고 멈춘다. 끝난 핸들은 이후 호출을 무시한다. 트윈과 단계는 구조체 배열에 담아 다시 쓰므로 재생 중에는 할당하지 않는다. 같은 오브젝트에서 컴포넌트 속성 키 클립이 Tint·Opacity를 재생하면 그 틱의 클립 값이 트윈 색을 덮어쓴다.

KeyRoom의 [SatchelPop](../../samples/KeyRoom/Scripts/SatchelPop.cs)은 코인을 얻으면 가방을 중심 기준으로 1.4배 키웠다가(0.1초) 돌아오고(0.15초) 그동안 금색으로 깜빡인다. [ToggleControls](../../samples/KeyRoom/Scripts/ToggleControls.cs)는 **Controls** 버튼을 누르면 UI 시간으로 2px 들어갔다 나온다.

```csharp
tick.Objects.Tween(tick.ObjectId).Scale(new(1.4f, 1.4f), .1f, TweenEase.EaseOut).Scale(Vector2.One, .15f, TweenEase.EaseIn);
click.Objects.Tween(click.ObjectId, TweenClock.Ui).UiRect(new(2, 2, -4, -4), .05f, TweenEase.EaseOut).UiRect(default, .08f, TweenEase.EaseIn);
```

UI 요소의 현재 위치는 `tick.Ui.Bounds(id)`로 읽는다. [세 실행 경로의 다섯 시점 값](../../engine/tests/results/engine-features-1/README.md#검증)

## 오브젝트 참조 목록

컴포넌트에 `public Guid[] Choices = [];`를 선언한다. Hierarchy에서 오브젝트를 필드의 **Drop object here**에 놓으면 끝에 추가되고, 특정 행에 놓으면 그 행 앞에 추가된다. Inspector 안에서는 오브젝트 이름을 잡아 다른 행에 놓아 그 번호로 옮긴다. **−**는 해당 참조 한 개를 제거한다. 각 편집은 **Cmd+Z**(Windows는 **Ctrl+Z**) 한 번으로 되돌릴 수 있다.

InventoryLab의 **Yard → HerbYard → Choices**에서 **Answer 4**를 **Answer 2**에 놓으면 순서가 **1, 4, 2, 3**이 된다. 저장하고 다시 열어 Play한 뒤 약초상 앞에서 **Return**으로 대화한다. 원래 네 번째 줄에 **2 Ask about the reward**가 표시되고 **2**를 누르면 약초상이 보상을 설명한다. 숫자 키는 현재 목록 순서를 따른다. [Mac 검증 화면과 저장 결과](../../engine/tests/results/script-reference-lists/README.md)

## 에셋 참조 목록과 enum, Vector2

KeyRoom에서 Hierarchy의 **Key → KeySequence**를 펼친다. **Frames**는 문이 열린 후 표시할 스프라이트 목록이다. Project 검색창에서 PNG를 찾아 **Drop .png here**에 놓으면 끝에 추가되고, 기존 행 위에 놓으면 그 행 앞에 추가된다. 목록 안에서 이름을 잡아 옮기면 순서가 바뀐다. 추가·이동·제거 각각을 **Cmd+Z**(Windows는 **Ctrl+Z**) 한 번으로 되돌릴 수 있다.

**RestFrame**에서 **First** 또는 **Last**를 골라 목록의 어느 쪽을 사용할지 정한다. **Offset**의 X와 Y는 문이 열릴 때의 월드 이동량(픽셀)이다. X가 양수면 오른쪽, Y가 음수면 위로 이동한다. **Showcase/Door.png**(닫힌 문)를 첫 번째에 두고 **First**, **24**, **-12**를 지정한 뒤 저장한다. Play에서 열쇠를 얻으면 열림 애니메이션이 끝난 문은 해당 PNG를 표시하고 월드 좌표 **(279, 18)**에 놓인다. 충돌체는 꺼져 있어 통과할 수 있다. 샘플 기본값은 **Last**와 **(0, 0)**이다.

게임 스크립트에서는 다음과 같이 필드를 선언한다.

```csharp
public enum DoorFrame { First, Last }

public sealed class KeySequence : GameBehaviour
{
    public SpriteReference[] Frames = [];
    public DoorFrame RestFrame = DoorFrame.Last;
    public System.Numerics.Vector2 Offset;
}
```

에셋 목록은 `SpriteReference[]`, `PrefabReference[]`, `SceneReference[]`, `DataReference[]`, `TableReference[]`를 지원한다. Project에서 필드와 같은 종류의 에셋을 넣는다. 씬 파일은 목록 순서대로 에셋 GUID를 보관한다. 다른 씬을 참조하는 경우 Build 설정의 **Included scenes**에도 추가한다. enum은 선언된 멤버 하나를 이름으로 저장하고, Vector2는 두 좌표를 저장한다. [실제 드래그·재열기·빌드 화면](../../engine/tests/results/script-asset-fields/README.md)

## 컴포넌트 조회와 Inspector 참조

`tick.Objects.GetComponent<PlayerMovement>(id)`는 그 오브젝트에 붙은 스크립트 인스턴스를 돌려준다. `GetComponent<BoxCollider2DComponent>(id)`처럼 내장 컴포넌트를 읽으면 설정의 복사본을 돌려준다. 이 복사본을 수정해도 충돌이나 렌더링 설정은 바뀌지 않는다. 비활성 오브젝트도 조회할 수 있고, 대상이 없거나 제거되었으면 null이다.

Inspector에서 타입을 제한하려면 필드를 다음처럼 선언한다.

```csharp
public ComponentReference<PlayerMovement> Player;

public override void FixedUpdate(ScriptTick tick)
{
    var movement = Player.Resolve(tick.Objects);
    if (movement == null) return;
    // movement는 연결된 Player의 실제 스크립트 인스턴스다.
}
```

KeyRoom의 **Chest → OpenChest → Player**에는 `PlayerMovement`가 붙은 오브젝트만 표시된다. **None**으로 연결을 해제할 수 있다. 같은 방식으로 구체적인 내장 컴포넌트도 지정할 수 있다. 씬에는 대상 오브젝트 ID만 YAML로 저장한다. 자식까지 복제하거나 프리팹을 생성할 때 내부 참조는 새 인스턴스로 연결된다. 대상에서 필요한 컴포넌트를 떼면 실행 중 조회는 null이며, 배포 시 잘못된 연결은 빌드 오류로 알려준다.

## 게임 정의 이벤트

KeyRoom에서 **Key → KeySequence → KeyAcquired**를 펼친다. 기본 연결은 **Door · DoorLock.Unlock**이다. **−**로 해제하고 **Connect…**에서 **Object → Door**, **Method → DoorLock.Unlock**, **Connect**를 선택해 다시 연결할 수 있다. 연결 행을 누르면 대상을 바꿀 수 있다. 각 변경은 **Cmd+Z**(Windows는 **Ctrl+Z**) 한 번으로 되돌릴 수 있다. **Cmd+S**로 씬을 저장한다.

발신 스크립트는 인자 없는 공개 인스턴스 C# 이벤트를 선언한다. KeyRoom은 처음 열쇠를 얻었을 때만 이벤트를 발생시킨다.

```csharp
public event Action KeyAcquired;

public override void OnTriggerEnter(ScriptContact contact)
{
    if (contact.OtherId != Collector || !contact.Session.Set("key")) return;
    KeyAcquired?.Invoke();
}
```

수신 메서드에는 `[GameEventHandler]`를 붙인다. 매개변수 `GameEventContext`의 `SenderId`와 `Sender`는 발신 오브젝트와 스크립트, `ObjectId`는 수신 오브젝트다. 예를 들어 다음 메서드를 Door에 연결하면 Door의 충돌체가 꺼진다.

```csharp
[GameEventHandler]
public void Unlock(GameEventContext message)
{
    message.Objects.SetColliderEnabled(message.ObjectId, false);
}
```

이벤트 하나에 여러 수신 메서드를 연결할 수 있다. 활성 상태인 대상만 Inspector에 등록한 순서대로 같은 호출 안에서 실행된다. 각 이벤트에서 동일한 오브젝트·스크립트·메서드 조합은 한 번만 등록할 수 있다. 앞선 메서드가 대상을 제거하면 뒤의 메서드는 건너뛴다. 수신 메서드 이름을 바꾸면 Project의 **Check Missing References…**나 빌드에서 끊긴 연결을 확인할 수 있다. 프리팹 내부의 연결은 생성한 각 인스턴스 안의 오브젝트로 다시 연결된다.

KeyRoom의 `DoorLock`은 저장된 열쇠가 있으면 생성 시 `EventSources<KeySequence>(nameof(Unlock))`로 이 메서드에 연결된 열쇠를 찾아 열린 스프라이트와 충돌 상태를 복원한다. 열쇠를 새로 주울 때만 열림 애니메이션과 효과음을 재생한다. [발신 스크립트](../../samples/KeyRoom/Scripts/KeySequence.cs) · [수신 스크립트](../../samples/KeyRoom/Scripts/DoorLock.cs)

**직접 수명 확인하기** — 첫 방에서 열쇠를 얻기 전 **1 → F5**로 저장한다. 왼쪽으로 가서 열쇠를 얻고 **F9**로 첫 방을 다시 연 뒤 한 번 더 얻으면 획득마다 콘솔에 `Door received KeyAcquired.`가 한 줄씩 생긴다. 첫 방에서 **F6**은 문을, **F7**은 열쇠를 하위 오브젝트와 함께 실제로 제거한다. **F9**로 다시 열 수 있다. 이미 열쇠가 있는 슬롯을 불러오면 `Door restored open.`이 표시된다. [Mac 검증 기록](../../engine/tests/results/game-events/README.md)

## 플레이 중 스프라이트와 충돌체 바꾸기

게임 스크립트에 `public SpriteReference OpenSprite;`를 선언한 뒤 Inspector에서 PNG를 지정한다. 빌더는 그 참조를 따라 PNG 또는 패킹된 아틀라스를 Release·Native AOT 게임에 포함한다.

```csharp
objects.SetSprite(door, OpenSprite);
objects.SetSpriteTint(door, "#E0F8FF");
objects.SetColliderEnabled(door, false);
```

`objects`는 콜백의 `life.Objects`, `tick.Objects` 또는 `contact.Objects`다. 변경은 현재 런타임 인스턴스에 적용된다. `GetComponent<SpriteRendererComponent>(door)`로 현재 스프라이트·색상의 복사본을 읽고, `IsColliderEnabled(door)`로 충돌체 자체의 상태를 확인한다. 오브젝트·대상 컴포넌트가 없거나 PNG·색상 값이 유효하지 않으면 설정 메서드는 `false`를 반환한다. `SetSprite(door, default)`는 기본 사각형을, `SetSpriteTint(door, null)`은 기본 색상을 선택한다. 충돌체를 끄면 이동 판정, 트리거, 물리 질의에서 빠지고 오브젝트의 스크립트·렌더러·오디오는 계속 실행된다.

KeyRoom의 기본 설정에서는 먼저 위쪽 문에 부딪힌 뒤 왼쪽으로 가서 열쇠를 얻는다. 문은 Frames 목록의 마지막 스프라이트인 `Showcase/DoorOpen.png`로 바꾸고 충돌체를 끈다. DoorLock에서 호출한 `PlayThenShowSprite()`는 열림 클립을 한 차례 표시한 후 새 스프라이트를 보여 준다. Scene Play에서 Door를 선택하면 Inspector의 실행 상태에 현재 이미지와 충돌체 상태가 표시된다. 문으로 걸어 들어가면 두 번째 방에 도착한다. Stop하면 원래 닫힌 문과 충돌체가 돌아온다. 저장된 열쇠 진행을 불러오면 열린 상태로 시작한다. [열쇠 스크립트](../../samples/KeyRoom/Scripts/KeySequence.cs)

## 디버그 선·원·텍스트 그리기

`FixedUpdate`의 `tick.Debug`와 `UpdateUi`의 `tick.Debug`로 월드 좌표에 선·원·텍스트를 그린다. 모든 Primary·Extra 화면 위에 그리고 미니맵에는 그리지 않는다. 게임 상태에는 영향을 주지 않는다.

| API | 그리는 것 |
| --- | --- |
| `tick.Debug.Line(from, to, color, width = 1)` | 두 월드 점 사이의 선. 너비는 화면 픽셀이다. |
| `tick.Debug.Circle(center, radius, color, width = 1)` | 월드 반지름의 원 윤곽(32분할) |
| `tick.Debug.Text(position, text, color, size = 12)` | 왼쪽 위가 월드 점에 오는 텍스트. 크기는 화면 픽셀이다. |

`FixedUpdate`에서 그린 도형은 다음 게임 틱까지, `UpdateUi`에서 그린 도형은 다음 UI 프레임까지 보인다. 계속 보이려면 매 틱 다시 호출한다. 게임 시간을 멈추면 마지막 틱의 도형이 남는다. `FixedUpdate`와 `UpdateUi`에서 각각 선·원·텍스트를 합쳐 4,096개까지 그리고 나머지는 버린다. 좌표가 유한하지 않은 호출도 버린다. 목록은 용량을 유지하므로 같은 수를 계속 그리면 프레임마다 할당하지 않는다.

**Scene Play와 개발용 빌드(`--profile development`)에서만 그린다.** 두 경로는 스크립트를 `MONOON_DEBUG_DRAW` 기호로 컴파일한다. 배포용 빌드에서는 C# 컴파일러가 `tick.Debug.…` 호출을 인수 계산과 함께 지우고(`[Conditional]`), 게시 단계가 그리기 경로를 트리밍 기능 스위치 `MoNoon.ScriptDebugDraw=false`로 제거한다. 디버그 전용 상태나 계산은 `#if MONOON_DEBUG_DRAW`로 감싸면 배포본에 남지 않는다.

```csharp
#if MONOON_DEBUG_DRAW
        if (tick.Input.Pressed("DebugDraw")) gizmos = !gizmos;
        if (gizmos)
        {
            var center = tick.Position + new Vector2(16, 16);
            tick.Debug.Circle(center, 40, Color.Gold, 2);
            tick.Debug.Line(center, partnerView.Center, Color.Cyan, 2);
            tick.Debug.Text(center + new Vector2(-36, 44), $"{center.X:0}, {center.Y:0}", Color.White);
        }
#endif
```

CameraLab의 프레이밍 쇼케이스(`ShowcaseFraming.scene`)를 Play하고 **G**를 누르면 주인공 발밑의 금색 원, 다음 정지 위치까지의 하늘색 선, 좌표 텍스트, 남은 약초와 데드존·프레이밍 상자가 켜진다. [FramingTour.cs](../../samples/CameraLab/Scripts/Showcase/FramingTour.cs) · [세 경로와 배포본 비교 기록](../../engine/tests/results/engine-features-2/README.md#1614-스크립트-디버그-그리기)

## 외부 IDE로 Scene Play 디버깅

1. MoNoon의 **Preferences → External script editor**에서 C# 확장이 활성화된 VS Code를 선택한다. KeyRoom을 연 뒤 **Project → Open C# Project…**를 누르면 현재 에디터에 연결할 VS Code 작업 공간이 열린다.
2. VS Code에서 `Scripts/KeySequence.cs`를 열고 `OnTriggerEnter`의 `KeyAcquired?.Invoke();` 줄 왼쪽 여백을 클릭한다. **Run and Debug → Attach to MoNoon Scene Play**를 선택하고 F5를 누른다. MoNoon에서 **Play**를 누른다. 프로젝트 메뉴로 작업 공간을 먼저 열었다면 이미 실행 중인 Play에도 연결할 수 있다.
3. KeyRoom에서 먼저 위로 가면 닫힌 문에 막힌다. 왼쪽으로 가서 열쇠를 주우면 문을 여는 호출 직전에 Scene Play가 멈춘다. VS Code의 **Variables → Locals**에서 `contact`와 `this`를 펼치거나, **Watch**에 `contact.OtherId`, `this.Collector`, `this.Door`를 입력한다. 처음 두 값은 플레이어를 가리킨다.
4. VS Code에서 **Continue** 또는 F5를 누른다. MoNoon 창으로 돌아가 오른쪽으로 이동하면 열린 문을 지나 두 번째 방에 도착한다. 중단된 상태에서 디버거 연결을 종료해도 게임은 재개된다.

**Attach to MoNoon Scene Play**는 이 컴퓨터의 MoNoon과 직접 통신한다. 메서드 블록 안에서 문장이 시작되는 줄에 중단점을 둘 수 있다. 중괄호나 빈 줄에 놓으면 연결 대기로 표시되고, Play 중 지원하지 않는 줄에는 안내가 표시된다. 현재 메서드의 인수·초기화된 지역 변수·인스턴스 필드와 자동 속성의 저장값을 읽을 수 있다. 이 연결에서 제공하는 실행 제어는 중단점과 Continue이며, 한 줄씩 실행하거나 Watch에서 메서드를 호출하는 기능은 제공하지 않는다. 스크립트를 수정했으면 Stop 후 다시 Play해 새 소스로 디버깅한다.

게임별 `.monoon/ide/*.Scripts.slnx`와 `*.Scripts.csproj`는 공용 파일과 Project 패널에서 이동한 파일까지 실제 소스 경로로 연결한다. 프로젝트 메뉴를 다시 열거나 Play를 시작할 때 목록을 갱신한다. 실행 중인 에디터에 연결할 때는 메뉴에서 열린 작업 공간을 사용한다. 에디터 UI 없이 솔루션만 생성하려면 저장소 루트에서 다음 명령을 실행한다.

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon --generate-script-project samples/KeyRoom
dotnet build samples/KeyRoom/.monoon/ide/KeyRoom.Scripts.slnx
```

Rider 등 .NET IDE에서는 솔루션을 열고 메뉴 상태줄의 MoNoon PID에 관리 디버거를 연결한 뒤 Play한다. VS Code에도 **Attach .NET debugger to MoNoon PID**가 있다. 이 Mac의 macOS 27.0.1에서는 VS Code와 Rider의 기본 PID 연결 도중 두 디버거가 종료됐다. 위에서 안내한 **Attach to MoNoon Scene Play**로 중단·조회·재개를 확인했다. [Mac 검증 기록](../../engine/tests/results/ide-debugging/README.md)

## Play 중 스크립트 다시 불러오기

에디터에서 Scene Play를 실행한 채 외부 에디터에서 게임 스크립트(`Scripts/`와 Project 패널에서 옮긴 `.cs`)를 저장하면 MoNoon이 약 0.75초 안에 변경을 감지한다. 백그라운드에서 다시 컴파일한 뒤 실행 중인 모든 동작을 새 코드의 인스턴스로 바꾸고 Console에 `Scripts reloaded: …`를 남긴다. 다음 게임 틱부터 새 코드가 실행된다.

| 필드 | 다시 불러온 뒤 |
| --- | --- |
| 이름과 타입이 같은 필드 (공개·비공개, 자동 속성 포함) | 실행 중의 값을 유지한다. 초기값만 바꾼 필드도 실행 중의 값이 남는다. |
| 프로젝트의 enum | 같은 이름의 멤버로 옮긴다. |
| 다른 동작 인스턴스 참조, `ComponentReference<T>` | 새 인스턴스나 같은 오브젝트를 가리킨다. |
| 새로 추가한 필드 | 코드의 초기값 또는 Inspector 값으로 시작한다. |
| 삭제한 필드 | 버린다. |
| 타입을 바꾼 필드, 옮길 수 없는 프로젝트 타입의 값(프로젝트 클래스 목록 등) | 기본값으로 되돌리고 Console에 경고를 남긴다. |

다시 불러오기는 `OnCreate`·`OnEnable`을 다시 호출하지 않는다. 실행 중이던 [대기 루틴](#기다린-뒤-이어서-실행하기)은 모두 이전 코드이므로 중지하고 Console에 개수를 알린다. 새 코드가 루틴을 다시 시작하는 시점(예: 다음 충돌이나 버튼)에 새로 실행된다. 이벤트·람다 필드는 옮기지 않으며, 게임 정의 이벤트 연결은 새 인스턴스에 다시 연결된다. 스크립트가 직접 다른 객체의 C# 이벤트에 구독해 둔 처리기는 이전 코드로 남으므로 Stop 후 다시 Play한다.

컴파일 오류가 나면 Play는 이전 코드로 계속 실행된다. Console에 `Script reload failed. Play keeps running the previous code.` 경고와 `./Scripts/파일.cs(줄,열): error …` 오류가 표시되고, 오류 줄을 더블클릭하면 외부 에디터가 해당 줄을 연다. 고쳐서 다시 저장하면 그때 다시 불러온다. 다시 불러오기는 에디터의 Scene Play 전용이다. Release·Native AOT 빌드에는 이 코드가 들어가지 않는다. [Mac 검증 기록](../../engine/tests/results/script-reload/README.md)

## 게임 스크립트 테스트

프로젝트 루트의 `Tests/` 폴더에 C# 테스트를 둔다. 테스트 파일은 `Scripts/`의 게임 스크립트와 한 어셈블리로 컴파일되므로 `PlayerMovement` 같은 게임 타입과 `internal` 공용 규칙을 그대로 쓸 수 있다. 공개 클래스의 공개 메서드에 `[GameTest("씬 경로")]`를 붙이고 `GameTestRun run` 하나를 받는다. 기다릴 일이 없으면 `void`, 기다리면 [대기](#기다린-뒤-이어서-실행하기)와 같은 `IEnumerable<ScriptWait>`를 반환한다. 테스트 메서드 안의 `ScriptWait.Frames`도 게임 틱을 센다.

```csharp
using System.Collections.Generic;
using Engine.Core;

public sealed class DoorTests
{
    [GameTest("Scenes/Showcase.scene")]
    public IEnumerable<ScriptWait> KeyOpensDoorThenHintAfterHalfSecond(GameTestRun run)
    {
        run.Press("Left");
        yield return ScriptWait.Until(() => run.Session.Has("key"));
        run.Release("Left");
        long opened = run.Time.TickCount;
        run.Assert(!run.Objects.IsColliderEnabled(run.Find("Door")), "the key turns the door's collider off");
        yield return ScriptWait.Until(() => run.Session.Has("doorHint"));
        run.AreEqual(30L, run.Time.TickCount - opened, "ticks from opening to the hint");
    }
}
```

| `GameTestRun` | 용도 |
| --- | --- |
| `Press`, `Release`, `Tap`, `Click(x, y)` | 다음 프레임에 키나 논리 좌표 클릭을 넣는다. `Tap`은 한 프레임 뒤에 뗀다. 컨트롤 이름은 GameSettings와 같다. |
| `Replay("Tests/이름.inputreplay")` | 플레이어 `--record-input`으로 기록한 입력을 다음 프레임부터 원래 프레임 간격으로 재생한다. 반환값을 `yield return`하면 재생이 끝날 때까지 기다린다. |
| `Session`, `Time`, `Objects`, `World`, `SceneId`, `Frame` | 세션 플래그, 게임·UI 시간과 틱 수, 실행 중 오브젝트 조회, 현재 씬과 진행한 프레임 수를 읽는다. |
| `Find(이름)`, `PositionOf(이름)`, `Script<T>(이름)` | 현재 씬에서 이름으로 오브젝트 ID, 월드 위치, 붙은 스크립트 인스턴스를 찾는다. |
| `Assert(조건, 설명)`, `AreEqual(기대값, 실제값, 설명)` | 실패하면 테스트를 멈추고 설명과 프레임 번호를 출력한다. |

KeyRoom의 상자·문 규칙은 [ChestTests.cs](../../samples/KeyRoom/Tests/ChestTests.cs)와 [DoorTests.cs](../../samples/KeyRoom/Tests/DoorTests.cs), 입력 쇼케이스 규칙은 [MoveTests.cs](../../samples/KeyRoom/Tests/MoveTests.cs)에 있다. 저장소 루트에서 `./monoon test project samples/KeyRoom`으로 실행하고 출력과 종료 코드를 확인한다. [명령과 실패 출력](build.md#게임-스크립트-테스트)

`Tests/`와 [에디터 확장](workspace.md#프로젝트-에디터-확장)의 `Editor/`는 게임 빌드와 Play에서 컴파일하지 않으므로 테스트 전용 API(`GameTestRun` 등)와 에디터 API는 게임 스크립트에서 사용할 수 없다. 외부 IDE용 `.monoon/ide/` 프로젝트에도 아직 포함되지 않는다.

## 기존 프로젝트와 빌드

기존의 일반 `Guid` 오브젝트 참조와 `FixedUpdate`, 트리거, UI 콜백은 그대로 쓸 수 있다. 이전에 서로 다른 폴더에 같은 이름의 **전역 클래스**를 두었다면, 각각 다른 C# 네임스페이스에 넣어 전체 타입 이름을 구분한다. 파일 경로를 유지하면 기존 씬·프리팹의 스크립트 ID를 바꿀 필요가 없다.

일반 Release는 `Game/.monoon-compiled/MoNoon.GameScripts.dll` 하나에 프로젝트 스크립트를 담는다. Native AOT는 같은 소스와 생성된 필드 연결 코드를 실행 파일에 정적으로 컴파일한다. 새로 내보낼 때는 현재 빌더와 같은 엔진 버전의 템플릿·AOT 키트를 사용한다. 콘텐츠 버전이 같아도 이전 Release 템플릿에 새 스크립팅 API가 없을 수 있다. 소스에서 업데이트한 경우 저장소 루트에서 `./engine/scripts/build-players.sh`로 템플릿과 키트를 함께 갱신한다. [버전 확인과 업그레이드 절차](upgrade.md)

## 숫자·색·플래그·배열·목록·사전·구조체 필드

InventoryLab의 [SupplyTuning](../../samples/InventoryLab/Scripts/SupplyTuning.cs)은 아래 종류를 하나씩 가진다. 오브젝트에 붙이면 Inspector에서 편집하고, 생성된 연결 코드가 `ScriptValues`로 저장 값을 읽는다(리플렉션 없음, Native AOT 지원). 저장 값이 없으면 스크립트의 초기화식 값을 그대로 쓴다.

| 필드 타입 | Inspector | 저장 형식 |
| --- | --- | --- |
| `long`, `uint` | 숫자 상자 | 숫자 |
| `System.Drawing.Color` | 견본 + 색 선택 창 + `#RRGGBB[AA]` 입력 | `"#RRGGBBAA"`(알파 255면 `#RRGGBB`) |
| `[ColorUsage(false)] string` | 같은 색 필드(`false`면 알파 없음) | 입력한 hex 문자열 |
| `[Flags] enum` | 이름 버튼 + 0이 아닌 멤버마다 체크 | `"Food, Gem"`(아무것도 없으면 `""`) |
| `int[]`·`long[]`·`uint[]`·`float[]`·`double[]`·`bool[]`·`string[]` | 행마다 번호·입력·×, 머리 행에 개수와 + | JSON 배열(초기화식이 없으면 `null`) |
| `List<T>`(T는 위 원소 타입) | 배열과 같다 | JSON 배열 |
| `Dictionary<string 또는 int, T>` | 행마다 번호·키·값·×. 키는 확정 때 검사(중복·int 아님 거부) | JSON 객체(삽입 순서) |
| `[Serializable] struct` | 머리 행의 요약과 펼치기 단추, 펼치면 멤버 행 | 멤버 JSON 객체 |

구조체는 한 단계만 지원한다. 멤버는 공개 인스턴스 필드 중 숫자·`bool`·`string`·enum·`Vector2`·`Color`만 표시·저장하고, 나머지 멤버(배열, 다른 구조체, `readonly`)는 코드 값을 유지한다. 매개변수 없는 생성자를 직접 선언한 구조체와 `[Serializable]`이 없는 구조체는 **Not shown**으로 이유를 알린다. 초기화식은 상수, `{ 1, 2 }`·`[1, 2]`·`new int[3]`, `new() { 4, 5 }`, `new() { ["herb"] = 3 }`·`{ { 1, "one" } }`, `new Wave { Count = 2 }`, `Color.White`·`Color.FromArgb(…)`까지 기본값으로 읽는다. 그 밖의 초기화식(메서드 호출 등)은 **Not shown: set at run time**이다.

```csharp
[Flags] public enum SupplyTags { Food = 1, Gem = 2, Key = 4, Quest = 8 }
[Serializable] public struct RestockWave { public int Count; public float Delay; public CrateRarity Rarity; public string Label; }

public sealed class SupplyTuning : GameBehaviour
{
    public long Seed = 20261124;
    public Color CrateTint = Color.FromArgb(214, 168, 92);
    public SupplyTags Tags = SupplyTags.Food | SupplyTags.Gem;
    public int[] StackSizes = { 5, 3, 1 };
    public List<int> Restock = new() { 2, 4 };
    public Dictionary<string, int> Prices = new() { ["herb"] = 3, ["gem"] = 10 };
    public RestockWave FirstWave = new RestockWave { Count = 3, Delay = 1.5f };
}
```

배포본 크기: 이 종류를 쓰지 않는 게임은 Release·Native AOT 모두 0 B 늘어난다(쓰지 않는 읽기 코드는 잘린다). SupplyTuning을 넣은 InventoryLab은 Native AOT 실행 파일이 33,136 B(0.40%) 늘었고, 그중 `List<int>`와 `Dictionary<string, int>` 두 필드가 16,528 B다. 값 형식 제네릭은 게임이 선언한 원소 타입마다 따로 컴파일되므로 꼭 필요한 곳에만 쓴다. [검증 기록](../../engine/tests/results/inspector-fields/README.md)

## Inspector 필드 속성과 툴팁

Unity와 같은 이름의 속성으로 Inspector 표시를 정한다. 모두 `Engine.Core`에 있고 `[Conditional("MONOON_EDITOR")]`라서 컴파일러가 게임 어셈블리에서 뺀다. 에디터는 스크립트 원본(Roslyn 심볼)에서 읽으므로 Release `MoNoon.GameScripts.dll`과 Native AOT 실행 파일에는 속성 데이터가 없다.

| 속성 | Inspector |
| --- | --- |
| `[Tooltip("…")]` | 필드 행의 툴팁. 없으면 `/// <summary>` 주석을 쓴다. |
| `[Header("…")]` | 필드 위의 굵은 제목 |
| `[Space(12)]` | 필드 위의 빈 간격(px, 기본 8) |
| `[Range(min, max)]` | int·float·double 필드에 슬라이더와 숫자 상자. 범위 밖 값은 경계로 저장한다. |
| `[Min(value)]` | 그보다 작은 값은 그 값으로 저장한다. |
| `[Multiline(4)]` | string 필드를 여러 줄 상자로 |
| `[HideInInspector]` | 저장·로드는 그대로 하고 표시만 하지 않는다. |
| `[ColorUsage(showAlpha)]` | string 필드를 색 필드로(위 표) |
| `[SerializeField]` | private·protected 필드를 공개 필드처럼 표시하고 저장한다. 게임은 생성된 연결 코드가 `UnsafeAccessor`로 값을 넣는다(리플렉션 없음, Native AOT 지원). |

```csharp
public sealed class PlayerMovement : GameBehaviour
{
    /// <summary>Walking speed in pixels per second.</summary>
    [Header("Movement"), FormerlySerializedAs("Speed"), Range(60, 480)]
    public float MoveSpeed = 240f;

    [Tooltip("How far a coin can be picked up or dropped, in pixels.")]
    [SerializeField, Min(16)] private float coinReach = 128f;
}
```

공개 필드나 `[SerializeField]` 필드라도 Inspector가 표시할 수 없으면(지원하지 않는 타입, `readonly`, 상수가 아닌 초기화식) 섹션 끝에 **Not shown: 이름 (이유)**가 회색으로 나온다. 그 필드는 코드가 정한 값을 그대로 쓴다. Inspector의 [Debug 보기](editor.md#inspector-잠금과-debug-보기)는 private 필드까지 초기화식과 함께 보여 준다. [검증 기록](../../engine/tests/results/inspector-components/README.md)

## Inspector 필드 이름 바꾸기

C# 공개 필드의 이름을 변경할 때 새 필드에 `[FormerlySerializedAs("이전이름")]`을 붙인다. 같은 타입의 Inspector 필드에 저장한 값과 오브젝트·에셋 참조가 유지된다. C#에서 해당 필드를 사용하는 다른 스크립트도 새 이름을 쓰도록 변경한다.

```csharp
using Engine.Core;

public sealed class PlayerMovement : GameBehaviour
{
    [FormerlySerializedAs("Speed")]
    public float MoveSpeed = 240f;
}
```

기존 씬이나 프리팹을 열면 Inspector에는 **MoveSpeed**가 표시된다. 해당 씬이나 프리팹을 저장하면 YAML의 필드 이름과 프리팹 비교에 쓰는 기준값도 새 이름으로 기록된다. 여러 에셋을 한꺼번에 저장할 필요는 없다. 원본을 먼저 저장한 뒤 예전 씬을 열어도 인스턴스의 개별 값이 유지된다.

필드 이름이 여러 번 바뀌었다면 가장 최근에 사용한 이름부터 특성을 하나씩 선언한다. 파일에 현재 이름이 있으면 그 값을 우선하고, 현재 이름이 없으면 선언 순서대로 처음 발견한 값을 쓴다. 동일한 컴포넌트의 두 필드가 현재 이름이나 이전 이름을 공유하면 Inspector와 빌드에서 충돌한 이름을 알려준다. 이 특성은 필드 이름을 이전하는 기능이며, 값의 타입을 바꿀 때는 별도의 데이터 변환이 필요하다.

KeyRoom의 Project 패널에서 에디터 검사용 씬 `Scenes/MovementWorkshop.scene`을 열고 Play한다(게임 빌드에는 들어가지 않는다). 위쪽 플레이어는 **MovementPlayer.prefab**의 240을 상속하고 아래쪽 플레이어는 320으로 설정돼 있다. 방향키나 WASD로 두 플레이어를 함께 움직인다. **F3**을 누르면 쇼케이스 첫 방으로 이동한다. [샘플 씬](../../samples/KeyRoom/Scenes/MovementWorkshop.scene) · [저장·재열기와 이동 검증](../../engine/tests/results/script-field-renames/README.md)
