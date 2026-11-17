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

## 오브젝트 참조 목록

컴포넌트에 `public Guid[] Choices = [];`를 선언한다. Hierarchy에서 오브젝트를 필드의 **Drop object here**에 놓으면 끝에 추가되고, 특정 행에 놓으면 그 행 앞에 추가된다. Inspector 안에서는 오브젝트 이름을 잡아 다른 행에 놓아 그 번호로 옮긴다. **−**는 해당 참조 한 개를 제거한다. 각 편집은 **Cmd+Z**(Windows·Linux는 **Ctrl+Z**) 한 번으로 되돌릴 수 있다.

InventoryLab의 **Bag → InventoryWorkbench → Choices**에서 **Answer 4**를 **Answer 2**에 놓으면 순서가 **1, 4, 2, 3**이 된다. 저장하고 다시 열어 Play한 뒤 **T**로 약초상과 대화한다. 원래 네 번째 버튼에는 **[2] 보상을 문의한다**가 표시되고 이를 클릭하면 약초상이 보상을 설명한다. 숫자 키는 현재 목록 순서를 따른다. [Mac 검증 화면과 저장 결과](../../engine/tests/results/script-reference-lists/README.md)

## 에셋 참조 목록과 enum, Vector2

KeyRoom에서 Hierarchy의 **Key → KeySequence**를 펼친다. **Frames**는 문이 열린 후 표시할 스프라이트 목록이다. Project 검색창에서 PNG를 찾아 **Drop .png here**에 놓으면 끝에 추가되고, 기존 행 위에 놓으면 그 행 앞에 추가된다. 목록 안에서 이름을 잡아 옮기면 순서가 바뀐다. 추가·이동·제거 각각을 **Cmd+Z**(Windows·Linux는 **Ctrl+Z**) 한 번으로 되돌릴 수 있다.

**RestFrame**에서 **First** 또는 **Last**를 골라 목록의 어느 쪽을 사용할지 정한다. **Offset**의 X와 Y는 문이 열릴 때의 월드 이동량(픽셀)이다. X가 양수면 오른쪽, Y가 음수면 위로 이동한다. **DoorOpen2.png**를 첫 번째에 두고 **First**, **24**, **-12**를 지정한 뒤 저장한다. Play에서 열쇠를 얻으면 열림 애니메이션이 끝난 문은 해당 PNG를 표시하고 월드 좌표 **(504, 70)**에 놓인다. 충돌체는 꺼져 있어 통과할 수 있다. 샘플 기본값은 **Last**와 **(0, 0)**이다.

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

KeyRoom에서 **Key → KeySequence → KeyAcquired**를 펼친다. 기본 연결은 **Door · DoorLock.Unlock**이다. **−**로 해제하고 **Connect…**에서 **Object → Wall/Door**, **Method → DoorLock.Unlock**, **Connect**를 선택해 다시 연결할 수 있다. 연결 행을 누르면 대상을 바꿀 수 있다. 각 변경은 **Cmd+Z**(Windows·Linux는 **Ctrl+Z**) 한 번으로 되돌릴 수 있다. **Cmd+S**로 씬을 저장한다.

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

KeyRoom의 기본 설정에서는 먼저 오른쪽 문에 부딪힌 뒤 왼쪽으로 가서 열쇠를 얻는다. 문은 Frames 목록의 마지막 스프라이트인 `DoorOpen3.png`로 바꾸고 충돌체를 끈다. DoorLock에서 호출한 `PlayThenShowSprite()`는 열림 클립을 한 차례 표시한 후 새 스프라이트를 보여 준다. 기존 열린 문 PNG는 투명해서 통로가 비어 보인다. Scene Play에서 Door를 선택하면 Inspector의 실행 상태에 현재 이미지와 충돌체 상태가 표시된다. 오른쪽으로 통과하면 두 번째 방에 도착한다. Stop하면 원래 닫힌 문과 충돌체가 돌아온다. 저장된 열쇠 진행을 불러오면 열린 상태로 시작한다. [열쇠 스크립트](../../samples/KeyRoom/Scripts/KeySequence.cs)

## 외부 IDE로 Scene Play 디버깅

1. MoNoon의 **Preferences → External script editor**에서 C# 확장이 활성화된 VS Code를 선택한다. KeyRoom을 연 뒤 **Project → Open C# Project…**를 누르면 현재 에디터에 연결할 VS Code 작업 공간이 열린다.
2. VS Code에서 `Scripts/KeySequence.cs`를 열고 `OnTriggerEnter`의 `KeyAcquired?.Invoke();` 줄 왼쪽 여백을 클릭한다. **Run and Debug → Attach to MoNoon Scene Play**를 선택하고 F5를 누른다. MoNoon에서 **Play**를 누른다. 프로젝트 메뉴로 작업 공간을 먼저 열었다면 이미 실행 중인 Play에도 연결할 수 있다.
3. KeyRoom에서 먼저 오른쪽으로 가면 닫힌 문에 막힌다. 왼쪽으로 가서 열쇠를 주우면 문을 여는 호출 직전에 Scene Play가 멈춘다. VS Code의 **Variables → Locals**에서 `contact`와 `this`를 펼치거나, **Watch**에 `contact.OtherId`, `this.Collector`, `this.Door`를 입력한다. 처음 두 값은 플레이어를 가리킨다.
4. VS Code에서 **Continue** 또는 F5를 누른다. MoNoon 창으로 돌아가 오른쪽으로 이동하면 열린 문을 지나 두 번째 방에 도착한다. 중단된 상태에서 디버거 연결을 종료해도 게임은 재개된다.

**Attach to MoNoon Scene Play**는 이 컴퓨터의 MoNoon과 직접 통신한다. 메서드 블록 안에서 문장이 시작되는 줄에 중단점을 둘 수 있다. 중괄호나 빈 줄에 놓으면 연결 대기로 표시되고, Play 중 지원하지 않는 줄에는 안내가 표시된다. 현재 메서드의 인수·초기화된 지역 변수·인스턴스 필드와 자동 속성의 저장값을 읽을 수 있다. 이 연결에서 제공하는 실행 제어는 중단점과 Continue이며, 한 줄씩 실행하거나 Watch에서 메서드를 호출하는 기능은 제공하지 않는다. 스크립트를 수정했으면 Stop 후 다시 Play해 새 소스로 디버깅한다.

게임별 `.monoon/ide/*.Scripts.slnx`와 `*.Scripts.csproj`는 공용 파일과 Project 패널에서 이동한 파일까지 실제 소스 경로로 연결한다. 프로젝트 메뉴를 다시 열거나 Play를 시작할 때 목록을 갱신한다. 실행 중인 에디터에 연결할 때는 메뉴에서 열린 작업 공간을 사용한다. 에디터 UI 없이 솔루션만 생성하려면 저장소 루트에서 다음 명령을 실행한다.

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon --generate-script-project samples/KeyRoom
dotnet build samples/KeyRoom/.monoon/ide/KeyRoom.Scripts.slnx
```

Rider 등 .NET IDE에서는 솔루션을 열고 메뉴 상태줄의 MoNoon PID에 관리 디버거를 연결한 뒤 Play한다. VS Code에도 **Attach .NET debugger to MoNoon PID**가 있다. 이 Mac의 macOS 27.0.1에서는 VS Code와 Rider의 기본 PID 연결 도중 두 디버거가 종료됐다. 위에서 안내한 **Attach to MoNoon Scene Play**로 중단·조회·재개를 확인했다. [Mac 검증 기록](../../engine/tests/results/ide-debugging/README.md)

## 기존 프로젝트와 빌드

기존의 일반 `Guid` 오브젝트 참조와 `FixedUpdate`, 트리거, UI 콜백은 그대로 쓸 수 있다. 이전에 서로 다른 폴더에 같은 이름의 **전역 클래스**를 두었다면, 각각 다른 C# 네임스페이스에 넣어 전체 타입 이름을 구분한다. 파일 경로를 유지하면 기존 씬·프리팹의 스크립트 ID를 바꿀 필요가 없다.

일반 Release는 `Game/.monoon-compiled/MoNoon.GameScripts.dll` 하나에 프로젝트 스크립트를 담는다. Native AOT는 같은 소스와 생성된 필드 연결 코드를 실행 파일에 정적으로 컴파일한다. 새로 내보낼 때는 현재 빌더와 같은 엔진 버전의 템플릿·AOT 키트를 사용한다. 콘텐츠 버전이 같아도 이전 Release 템플릿에 새 스크립팅 API가 없을 수 있다. 소스에서 업데이트한 경우 저장소 루트에서 `./engine/scripts/build-players.sh`로 템플릿과 키트를 함께 갱신한다. [버전 확인과 업그레이드 절차](upgrade.md)
