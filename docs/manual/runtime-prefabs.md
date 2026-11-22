# Play 중 프리팹 생성·제거

**KeyRoom에서 해보기.** 첫 방의 시작 위치에서 **Play → E**를 누른다. 상자 뚜껑이 사라지고 오른쪽에 코인이 하나 생긴다. Hierarchy에서 **Coin**을 선택하면 Inspector에 Sprite Renderer, **Box Collider 2D → Is Trigger**, CollectCoin이 표시된다. 게임 화면을 클릭한 뒤 **→** 또는 **D**로 코인에 닿으면 코인이 화면과 Hierarchy에서 사라지고 왼쪽 위에 **COIN: 1**이 나타난다. Play를 멈추면 원래 편집 씬과 선택이 복원된다.

## 태그·그룹으로 오브젝트 찾기

**Project → Assets → Coin.prefab**을 더블클릭하고 루트 **Coin**을 선택한다. Inspector의 **Tags & groups**에서 **Tag**에 `Coin`, **Groups**에 `수집품`을 입력하고 **Save Prefab**을 누른다. 오브젝트에 태그 하나와 여러 그룹을 지정할 수 있다. 그룹 사이는 쉼표로 구분한다. 이름 앞뒤의 공백과 중복 그룹은 정리되며 대소문자는 구별한다.

KeyRoom에서 **Play → Debug**를 열고 **Group** 옆 입력란에 `수집품`을 입력한다. 게임 화면을 클릭하고 **E**로 상자를 열면 결과가 **0 → 1 · Coin**으로 바뀐다. 오른쪽으로 걸어 코인을 먹으면 같은 틱에 **0**이 된다. **Tag**로 바꾼 뒤 `Coin`을 입력해도 같은 오브젝트가 나온다.

코인을 남긴 채 방을 떠나는 경우도 확인할 수 있다. 새 Play에서 **E → C**로 코인을 들고, 왼쪽의 열쇠를 얻은 뒤 오른쪽 문을 통과한다. 첫 방에서는 코인이 계속 조회되고, 두 번째 방이 열리면 결과가 **0**이 된다.

```csharp
var coins = tick.Objects.FindByGroup("수집품");
foreach (var id in coins)
{
    if (tick.Objects.IsActive(id) && tick.Objects.TryGetPosition(id, out var position))
        tick.Session.Log($"수집품 {id} @ {position}");
}
```

`FindByGroup`과 `FindByTag`는 현재 월드에 등록된 오브젝트 ID를 등록 순서로 제공한다. 미리 받아 둔 목록도 생성과 제거를 따라간다. 비활성 오브젝트의 분류는 유지되므로 활성 상태가 필요하면 `IsActive`를 사용한다. 순회 도중 오브젝트를 제거해도 이미 시작한 순회는 시작 시점의 ID를 사용한다. 추가 씬을 내리면 그 씬의 멤버만 빠진다. 주 씬을 교체하면 이전 목록은 비워지며 새 월드에서 다시 조회할 수 있다.

[KeyRoom의 세 OS 실행 기록](../../engine/tests/results/object-groups/README.md)

## 프로젝트에서 사용하기

1. 필요한 컴포넌트가 붙은 오브젝트를 Hierarchy에서 Project 패널로 끌어 프리팹을 만든다. 자식도 포함된다.
2. 생성할 쪽의 C# 컴포넌트에 `public PrefabReference Loot;` 필드를 선언한 뒤 Inspector의 **Loot** 목록에서 프리팹을 지정한다. 파일명이나 GUID를 스크립트에 적을 필요가 없다.
3. 입력, 충돌 또는 UI 클릭 콜백에서 `Objects`로 인스턴스를 생성·제거한다. 반환된 GUID는 그 인스턴스의 루트다.

```csharp
// FixedUpdate(ScriptTick tick) 안에서 원하는 조건이 충족됐을 때
var id = tick.Objects.Instantiate(Loot, tick.LocalToWorld(new Vector2(52, -5)), tick.ObjectId);
tick.Objects.TrackForSave(id);
```

```csharp
// OnTriggerEnter(ScriptContact contact) 안에서 자기 자신을 제거:
contact.Objects.Destroy(contact.ObjectId);
```

생성 위치는 부모 유무와 관계없이 월드 좌표다. 프리팹의 각 오브젝트는 새 GUID를 받고, 프리팹 내부의 오브젝트 참조는 같은 인스턴스의 자식을 가리킨다. 생성된 오브젝트의 렌더링과 Hierarchy는 현재 고정 업데이트 끝에 갱신되며, 스크립트와 충돌은 다음 업데이트부터 실행된다. `SetActive(id, false)`는 하위 트리를 숨기고 충돌·스크립트를 중지한다. `Destroy(id)`는 하위 트리를 제거한다. Play 중 생성한 인스턴스는 Inspector에서 숫자 필드와 로컬 변환을 시험할 수 있다. 이 인스턴스의 변경은 Stop하면 사라진다. [Play Inspector 사용법](play-diagnostics.md#play-중-값-조정과-선택-적용)

빌드할 때 엔진은 Inspector에 연결한 프리팹을 따라가서 그 안의 PNG·클립·오디오·타일셋과 스크립트까지 수집한다. 프리팹에서만 쓰는 스크립트도 Release에서는 컴파일된 어셈블리에, Native AOT에서는 게임 실행 파일에 포함된다. 코드에서 GUID만 만들어 참조하는 방식으로는 빌드 의존성을 선언할 수 없다. 스크립트가 있는 프로젝트를 새로 빌드할 때는 현재 빌더와 같은 버전의 템플릿·AOT 키트를 준비한다. [프로젝트 업그레이드](upgrade.md)

[F5·F9에서 위치와 부모 복원하기](player-data.md#월드와-런타임-프리팹-저장) · [KeyRoom 상자 스크립트](../../samples/KeyRoom/Scripts/OpenChest.cs) · [Coin.prefab](../../samples/KeyRoom/Assets/Coin.prefab) · [획득 스크립트](../../samples/KeyRoom/Scripts/CollectCoin.cs) · [macOS 실행 기록](../../engine/tests/results/runtime-prefabs/README.md)
