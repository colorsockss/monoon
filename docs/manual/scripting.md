# 프로젝트 C# 스크립팅

[매뉴얼 목차](README.md) · [KeyRoom으로 확인한 실행 결과](../../engine/tests/results/scripting/README.md)

## 컴포넌트와 공용 파일

**Add Component → New C# Component…**는 선택한 오브젝트에 붙일 스크립트를 만든다. **Project 패널 → + → New Shared C# File…**는 여러 스크립트가 쓸 일반 C# 파일을 만든다. 둘 다 `Scripts/`에 저장되고 더블클릭하면 설정한 외부 에디터가 열린다. `Scripts` 아래의 폴더를 보고 있으면 해당 폴더에 만들어진다.

게임은 `Scripts/**/*.cs`를 하나의 프로젝트로 컴파일한다. 일반 클래스, 열거형, 정적 함수, 추상 기반 클래스, 네임스페이스를 파일 사이에서 사용할 수 있다. Add Component와 Inspector 드롭은 **파일 이름과 같은 공개 클래스**이며, `GameBehaviour`를 상속하고 인수 없는 공개 생성자를 가진 구체 타입만 받는다. 공용 파일도 Project에는 보인다. 공용 파일의 컴파일 오류는 사용 중인 컴포넌트까지 영향을 준다. **Project → Console…**에서 파일과 줄을 확인한다.

KeyRoom의 [LootRules.cs](../../samples/KeyRoom/Scripts/Shared/LootRules.cs)는 상자를 열 수 있는 거리와 획득 플래그 이름을 정의한다. [OpenChest.cs](../../samples/KeyRoom/Scripts/OpenChest.cs)와 [CollectCoin.cs](../../samples/KeyRoom/Scripts/CollectCoin.cs)는 같은 네임스페이스를 가져와 이 규칙을 사용한다.

## 생명주기

다음 메서드는 필요한 것만 `override`한다. `ScriptLife`로 내 오브젝트 ID, 세션, 오브젝트와 UI에 접근한다.

| 콜백 | 발생 시점 |
| --- | --- |
| `OnCreate(ScriptLife life)` | 씬의 첫 고정 업데이트 전 또는 런타임 프리팹 생성 시, 인스턴스당 한 번. Inspector 값이 적용돼 있고 같은 씬·프리팹의 다른 컴포넌트를 찾을 수 있다. |
| `OnEnable(ScriptLife life)` | 처음 활성화되거나 자신과 부모가 다시 활성화될 때. 비활성 부모 아래에 생성되면 부모가 켜질 때까지 기다린다. |
| `FixedUpdate(ScriptTick tick)` | 활성 상태에서 시뮬레이션 틱마다 실행한다. 틱 중에 생성한 프리팹은 그 틱의 구조 변경을 반영한 뒤 업데이트에 참여한다. |
| `OnAnimationEvent(ScriptAnimationEvent frame)` | 같은 오브젝트의 애니메이터가 이름이 지정된 프레임에 진입할 때 호출한다. [파라미터 및 이벤트 예제](sprites.md#파라미터로-애니메이션-전환하기) |
| `OnDisable(ScriptLife life)` | 자신이나 부모가 꺼질 때, 또는 활성 상태에서 제거될 때. 이미 꺼진 오브젝트를 제거하면 다시 호출되지 않는다. |
| `OnDestroy(ScriptLife life)` | 제거, 씬 전환, 정상적인 Play 종료 시 한 번. 제거된 ID를 조회하면 null이므로 종료에 필요한 데이터는 인스턴스 필드에 보관한다. |

부모와 자식을 함께 켜면 부모부터, 끄거나 제거하면 자식부터 처리한다. 같은 활성 상태를 두 번 지정해도 이벤트가 중복되지 않는다. 한 번도 실행하지 않은 씬(예: 리소스 준비가 실패한 전환 대상)은 생명주기를 시작하지 않는다.

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

## 기존 프로젝트와 빌드

기존의 일반 `Guid` 오브젝트 참조와 `FixedUpdate`, 트리거, UI 콜백은 그대로 쓸 수 있다. 이전에 서로 다른 폴더에 같은 이름의 **전역 클래스**를 두었다면, 각각 다른 C# 네임스페이스에 넣어 전체 타입 이름을 구분한다. 파일 경로를 유지하면 기존 씬·프리팹의 스크립트 ID를 바꿀 필요가 없다.

일반 Release는 `Game/.monoon-compiled/MoNoon.GameScripts.dll` 하나에 프로젝트 스크립트를 담는다. Native AOT는 같은 소스와 생성된 필드 연결 코드를 실행 파일에 정적으로 컴파일한다. 새 빌드에는 콘텐츠 버전 19의 템플릿·AOT 키트를 사용한다. 이전에 내보낸 게임은 기존 플레이어와 함께 독립적으로 실행된다.
