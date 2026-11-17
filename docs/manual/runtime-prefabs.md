# Play 중 프리팹 생성·제거

**KeyRoom에서 해보기.** 첫 방의 시작 위치에서 **Play → E**를 누른다. 상자 뚜껑이 사라지고 오른쪽에 코인이 하나 생긴다. Hierarchy에서 **Coin**을 선택하면 Inspector에 Sprite Renderer, **Box Collider 2D → Is Trigger**, CollectCoin이 표시된다. 게임 화면을 클릭한 뒤 **→** 또는 **D**로 코인에 닿으면 코인이 화면과 Hierarchy에서 사라지고 왼쪽 위에 **COIN: 1**이 나타난다. Play를 멈추면 원래 편집 씬과 선택이 복원된다.

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

생성 위치는 부모 유무와 관계없이 월드 좌표다. 프리팹의 각 오브젝트는 새 GUID를 받고, 프리팹 내부의 오브젝트 참조는 같은 인스턴스의 자식을 가리킨다. 생성된 오브젝트의 렌더링과 Hierarchy는 현재 고정 업데이트 끝에 갱신되며, 스크립트와 충돌은 다음 업데이트부터 실행된다. `SetActive(id, false)`는 하위 트리를 숨기고 충돌·스크립트를 중지한다. `Destroy(id)`는 하위 트리를 제거한다. Play Inspector는 읽기 전용이며 플레이 중 상태는 씬에 저장되지 않는다.

빌드할 때 엔진은 Inspector에 연결한 프리팹을 따라가서 그 안의 PNG·클립·오디오·타일셋과 스크립트까지 수집한다. 프리팹에서만 쓰는 스크립트도 Release에서는 컴파일된 어셈블리에, Native AOT에서는 게임 실행 파일에 포함된다. 코드에서 GUID만 만들어 참조하는 방식으로는 빌드 의존성을 선언할 수 없다. 스크립트가 있는 프로젝트를 새로 빌드할 때는 현재 빌더와 같은 버전의 템플릿·AOT 키트를 준비한다. [프로젝트 업그레이드](upgrade.md)

[F5·F9에서 위치와 부모 복원하기](player-data.md#월드와-런타임-프리팹-저장) · [KeyRoom 상자 스크립트](../../samples/KeyRoom/Scripts/OpenChest.cs) · [Coin.prefab](../../samples/KeyRoom/Assets/Coin.prefab) · [획득 스크립트](../../samples/KeyRoom/Scripts/CollectCoin.cs) · [macOS 실행 기록](../../engine/tests/results/runtime-prefabs/README.md)
