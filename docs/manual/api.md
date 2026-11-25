# 게임 스크립트 API — InventoryLab의 약초 의뢰

약초 마당에서 **Return**(또는 **T**)으로 약초상에게 말을 걸어 의뢰를 받고, 약초 세 포기를 주워 오면 가방에 수정 한 개가 들어간다. 이 흐름은 `Engine.Core`의 컴포넌트·입력·UI·충돌·저장 API와 프로젝트의 `MoNoon.Inventory`, `MoNoon.Quests`, `MoNoon.Dialogue` 패키지를 연결한 예다. 아래 예제의 `bag`, `journal`, `quest`, `talk`는 `HerbYard`가 연결해 둔 런타임 인스턴스다. [샘플 씬](../../samples/InventoryLab/Scenes/Showcase.scene) · [전체 게임 스크립트](../../samples/InventoryLab/Scripts/Showcase/HerbYard.cs)

## 컴포넌트 생성과 참조

`Scripts/Inventory/InventoryContainer.cs`는 다음과 같이 작성한다. Inspector의 **Catalog**에 `Assets/Inventory/Items.table`을 연결하고 **Slots**를 3으로 두면, 플레이어가 씬을 시작할 때 세 칸 가방을 한 번 만든다.

```csharp
using Engine.Core;

namespace MoNoon.Inventory;

public sealed class InventoryContainer : GameBehaviour
{
    public TableReference Catalog;
    public int Slots = 3;
    public InventoryBag Bag { get; private set; }

    public override void OnCreate(ScriptLife life) =>
        Bag = new InventoryBag(life.Objects.ReadTable(Catalog), Slots);
}
```

컴포넌트는 파일과 이름이 같은 공개 `GameBehaviour` 구체 클래스와 인수 없는 공개 생성자를 사용한다. Inspector에 연결할 값은 공개 인스턴스 필드로 선언한다. `InventoryBag`처럼 여러 컴포넌트가 공유하는 일반 C# 클래스도 `Scripts/`에 둘 수 있다. [컴포넌트 원본](../../samples/InventoryLab/Scripts/Inventory/InventoryContainer.cs) · [스크립트 작성 규칙](scripting.md)

| 콜백과 진입점 | InventoryLab에서 사용하는 시점 |
| --- | --- |
| `OnCreate(ScriptLife life)` | 각 컴포넌트가 자기 데이터를 준비한다. `InventoryContainer`는 가방, `QuestLog`는 의뢰 목록, `DialoguePlayer`는 대화 세션을 만든다. |
| `OnEnable(ScriptLife life)` | `HerbYard`가 `Container.Resolve(life.Objects).Bag`처럼 준비된 인스턴스를 연결하고 화면을 갱신한다. 오브젝트를 껐다 켜면 이 콜백이 다시 호출된다. |
| `FixedUpdate(ScriptTick tick)` | `tick.Input`으로 액션을 읽고 게임 상태를 바꾼다. `tick.DeltaTime`은 이번 시뮬레이션 틱의 초 단위 간격이다. |
| `OnTriggerEnter(ScriptContact contact)` | 약초에 붙인 `HerbPickup`이 `contact.Objects`로 `HerbYard`를 찾아 약초를 가방에 담는다. |
| `OnUiClick(UiClick click)` | 재고 씬(`ShowcaseStock.scene`)의 장부 목록 `LedgerList`가 선택한 행의 아이템을 사용한다. |

씬의 **Yard** 오브젝트에는 `InventoryContainer`, `QuestLog`, `DialoguePlayer`, `HerbYard`가 함께 붙어 있다. `ComponentReference<QuestLog>`의 대상은 씬 오브젝트의 ID다. `Resolve`는 그 오브젝트의 실제 스크립트 인스턴스를 돌려주고, 연결을 비워 두었거나 대상을 제거하면 `null`을 반환한다. 선택적 연결은 조회 결과를 확인한다. 샘플처럼 필수인 연결은 Inspector에서 지정한다. [생명주기와 타입 참조 정의](../../engine/src/Engine.Core/GameBehaviour.cs) · [Yard의 필드 연결](../../samples/InventoryLab/Scenes/Showcase.scene)

## 테이블 한 행과 대화 에셋 읽기

| API | 인자와 반환값 | 샘플에서 확인할 값 |
| --- | --- | --- |
| `IGameObjects.ReadTable(TableReference)` | `.table` 에셋의 GUID를 받아 `GameTable`을 읽는다. | `QuestLog.Catalog` → `Assets/Quests/Collection.table` |
| `GameTable.Row(string key)` | 행을 열 이름으로 조회할 수 있는 `GameData`로 돌려준다. 없는 행 키에는 `KeyNotFoundException`이 발생한다. | `Row("herbalist")` |
| `GameData.GetText(key, fallback)`, `GetNumber(key, fallback)` | 키와 타입이 맞으면 값을 돌려준다. 기본 대체값은 빈 문자열과 0이다. | `item`은 `herb`, `goal`은 3, `reward`는 `gem`, `rewardCount`는 1 |
| `IGameObjects.ReadData(DataReference)` | `.data` 에셋을 읽어 이번 호출에서 사용할 `GameData`를 돌려준다. | `DialoguePlayer.Story` → `Herbalist.data`의 시작 노드 `greeting` |

`QuestLog`는 `OnCreate`에서 `new QuestJournal(life.Objects.ReadTable(Catalog))`를 호출한다. 각 행은 목표 수량과 보상 수량이 양의 정수인지 검사하므로 빈 필드나 0을 넣으면 해당 필드를 알려 주는 예외가 발생한다. 에셋을 배포에 포함하려면 포함 씬·프리팹의 Inspector 필드로 연결한다. 이름으로 표를 연 뒤 사용하는 행 키와 열 이름도 게임 데이터의 일부다. [의뢰 테이블](../../samples/InventoryLab/Assets/Quests/Collection.table) · [테이블 API](../../engine/src/Engine.Core/GameTable.cs) · [대화 에셋 리더](../../samples/InventoryLab/Scripts/Dialogue/DialoguePlayer.cs)

## 입력과 UI를 같은 게임 동작에 연결하기

`GameSettings.input.actions`에서 **T**·**Return**은 `Talk`, **1**은 `AddHerb`, **U**는 `Use`, **S**·**F5**는 `Save`에 연결된다. `tick.Input.Pressed("Talk")`는 해당 액션을 누른 틱에 참이고, `Held`는 누르는 동안, `Released`는 놓은 틱에 참이다. 대화가 열려 있으면 `HerbYard`는 `AddHerb`를 첫 번째 선택지로 해석한다. 걷기는 `YardWalker`가 `tick.Direction`(방향키 네 액션)으로 처리하고, 대화 중에는 세션 플래그 `talking`을 보고 멈춘다. [입력 설정](../../samples/InventoryLab/GameSettings) · [입력 API](../../engine/src/Engine.Core/InputActions.cs)

약초는 트리거 충돌체다. 주인공이 지나가면 `HerbPickup`이 같은 `HerbYard`의 메서드를 호출한다. 아래는 그 분기와 `HerbYard.Show`의 화면 갱신에서 사용하는 호출이다.

```csharp
// HerbPickup.OnTriggerEnter 안에서
if (contact.OtherId != Player || contact.Objects.GetComponent<HerbYard>(Yard) is not { } yard) return;
if (!yard.Collect(Item, contact.Ui, contact.Objects)) return;   // 가방이 차면 약초는 그 자리에 남는다
```

```csharp
// HerbYard.Show 안에서
ui.SetVisible(Dialogue, talk.Current != null);
ui.SetText(TrackerTitle, quest.Title);
ui.SetText(Status, message);
ui.SetVisible(Status, message.Length > 0);
```

`IGameUi`에 넘기는 값은 대상 위젯의 오브젝트 ID다. `SetVisible`은 표시 여부, `SetInteractable`은 버튼·입력 필드의 조작 가능 여부, `SetText`는 라벨·입력 필드의 텍스트를 바꾼다. 메서드가 `false`를 반환하면 연결한 오브젝트와 대상 위젯 종류를 확인한다. [약초 스크립트](../../samples/InventoryLab/Scripts/Showcase/HerbPickup.cs) · [UI API](../../engine/src/Engine.Core/ScreenUi.cs)

## 수집·대화·보상을 연결하기

`DialogueSession.Begin()`은 시작 노드를 열고 `Current`는 현재 노드, `Position`은 저장할 노드 ID를 제공한다. `Choose`는 0부터 시작하는 선택지 번호와 행동 콜백을 받는다. 약초상 대화 에셋에는 의뢰를 받는 선택지에 `accept`, 보상을 받는 선택지에 `claim`이 들어 있다. 아래는 실패 메시지 처리를 뺀 요약이다. [대화 노드와 선택지](../../samples/InventoryLab/Assets/Dialogue/Herbalist.data) · [대화 세션](../../samples/InventoryLab/Scripts/Dialogue/DialoguePlayer.cs)

```csharp
bool advanced = talk.Choose(index, action => action switch
{
    "accept" => quest.TryAccept(),
    "claim"  => quest.TryClaim(bag.TryAdd),
    _        => false
});
```

1. `TryAccept()`가 성공하면 의뢰가 `Available`에서 `Active`가 된다. 잘못된 선택지나 실패한 행동은 `Choose`가 `false`를 반환하고 현재 노드를 유지한다.
2. 약초를 주울 때는 먼저 `bag.TryAdd("herb")`를 호출한다. 성공한 경우에만 `journal.RecordCollection("herb")`를 호출한다. 가방에 못 담은 아이템이 진행도에 포함되는 것을 막는다.
3. 수집량이 3이면 `Ready`가 된다. `TryClaim(bag.TryAdd)`는 보상의 ID와 수량을 가방에 넘긴다. 전량 지급하면 `Claimed`가 되고, 자리가 부족하면 `Ready`를 유지해 다시 시도할 수 있다. 이미 받은 보상은 재요청해도 지급되지 않는다. [인벤토리 구현](../../samples/InventoryLab/Scripts/Inventory/InventoryBag.cs) · [의뢰 구현](../../samples/InventoryLab/Scripts/Quests/QuestJournal.cs)

## 저장 슬롯과 버전 이전

`tick.Session.Storage`와 `click.Session.Storage`는 같은 `PlayerStorage`에 접근한다. 슬롯 번호는 1~999다. InventoryLab은 가방, 의뢰 수령 기록, 현재 대화 위치를 **슬롯 1의 같은 문서**에 담아 저장한다.

```csharp
var data = bag.Capture();
data.Version = 3;
data.Values.Add("quests", journal.Capture());
data.Set("dialogue", talk.Position);
storage.SaveSlot(1, data);
```

`LoadSlot(int slot, int version, Action<int, GameData>? migrate = null)`는 슬롯이 없으면 `null`을 돌려준다. 예전 슬롯은 `migrate(from, data)`를 버전마다 한 번씩 호출해 메모리에서 올린다. InventoryLab은 v1 → v2에서 `quests`를 빈 매핑으로 추가하고 v2 → v3에서 `dialogue`를 빈 문자열로 추가한다. `WorkbenchSave.Read`가 가방·의뢰·대화 상태를 복원한 뒤 다음 `SaveSlot`에서 새 버전이 디스크에 기록된다. 현재 코드보다 높은 슬롯 버전은 읽기 오류가 난다. 대화 노드 ID나 아이템 키를 바꿀 때도 이전 저장의 값을 연결할 규칙이 필요하다. [저장·읽기 전체 코드](../../samples/InventoryLab/Scripts/WorkbenchSave.cs) · [PlayerStorage](../../engine/src/Engine.Core/PlayerStorage.cs) · [게임 데이터 버전 이전](../../engine/src/Engine.Core/GameData.cs)

## 다른 런타임 API 찾기

| 목적 | 스크립트에서 접근하는 API | 예제 |
| --- | --- | --- |
| 프리팹 생성·제거와 이동 | `tick.Objects.Instantiate(prefab, position, parent)`는 새 루트 ID를 반환한다. `Destroy(id)`는 제거 성공 여부를 반환한다. `tick.Move(displacement)`로 틱의 이동량을 전달한다. | [KeyRoom의 상자·코인](runtime-prefabs.md) · [Transform 2D](transform-2d.md) |
| 충돌 질의와 카메라 | `tick.Objects.Physics`에서 레이·겹침·스윕을 조회한다. `tick.Cameras.TryGetView`로 카메라의 현재 뷰를 얻는다. | [충돌 질의](collision.md) · [카메라](cameras.md) |
| 애니메이션과 소리 | `tick.Objects.Animator(id)`로 애니메이터를 찾는다. `OnAnimationEvent`에서 프레임 이벤트를 받고 `tick.Objects.Audio`로 소리를 제어한다. | [애니메이션](sprites.md) · [오디오](audio.md) |
| 씬 전환과 진단 | `tick.Session.LoadSceneAsync(id)`로 씬을 전환하고 `tick.Session.Log(message)`로 실행 진단에 기록한다. | [씬 전환](scene-streaming.md) · [Play 진단](play-diagnostics.md) · [배포본 크래시](build.md#크래시-진단) |

[업그레이드와 두 형식의 빌드 검증](upgrade.md) · [전체 매뉴얼](README.md)
