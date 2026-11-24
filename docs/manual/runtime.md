# 런타임과 컴포넌트

[매뉴얼 목차](README.md)

## Inspector에서 붙일 수 있는 내장 컴포넌트

| 이름 | 역할과 주요 설정 |
| --- | --- |
| Sprite Renderer | 프로젝트 PNG 또는 기본 **Square**를 Transform 크기로 그린다. Square에는 **Color**를 설정할 수 있다. **Layer / Order**로 월드 그리기 순서를 정한다. |
| Sprite Animator | **Clip**에 프레임 애니메이션을 연결한다. **Play on start**를 켜면 씬 시작과 함께 재생한다. |
| Audio Source | 음악·효과음 스트리밍, 루프·페이드, 소스 볼륨과 공간 범위를 설정한다. [믹서와 조작법](audio.md) |
| Audio Listener 2D | 공간 음향을 듣는 기준점이다. 플레이어에 붙이면 이동에 따라 거리와 좌우 균형이 바뀐다. |
| Tilemap 2D | 타일셋, 격자 모양, 셀 크기와 배치를 가진 월드 레이어다. [타일맵 편집](tilemaps.md) |
| Box / Circle / Polygon Collider 2D | 상자·원·볼록 다각형으로 이동을 막거나 진입 이벤트를 받는다. **Layer / Mask**와 **Is Trigger**로 대상을 정한다. [설정과 스크립트 질의](collision.md) |
| Camera 2D | 시점·추적·Zoom·맵 경계와 출력 영역을 설정한다. [분할 화면과 미니맵](cameras.md) |
| UI Canvas | 게임 화면 기준 UI 루트와 그리기 **Order**를 정한다. |
| UI Rect | UI의 **Anchor**와 **Show if** 조건을 정한다. 크기와 오프셋은 Transform에서 편집한다. |
| UI Image | PNG 또는 단색 채움을 UI 영역에 표시한다. |
| UI Text | 텍스트, 글자 크기·색, 가로·세로 안쪽 여백을 지정한다. |
| UI Button | 마우스 클릭과 키보드 활성화를 받는다. **Interactable**로 입력을 제어한다. [UI 제작](game-ui.md) |

Transform은 모든 오브젝트가 가진 기본 정보다. 이동, 열쇠 획득, 문 열기 등 게임 고유의 동작은 프로젝트의 C# 스크립트로 추가한다. 예전 HUD 데이터는 씬을 열 때 현행 UI 구성으로 읽힌다.

## 월드, 시간, 카메라

월드 좌표는 오른쪽이 +X, 아래가 +Y다. 카메라 Zoom 1에서는 월드 1단위가 게임 논리 해상도 1픽셀이다. 타일맵과 스프라이트의 **Layer**, **Order**가 클수록 나중에 그려진다. 같은 값에서는 씬 순서가 적용된다. 게임 UI는 월드 다음에 그린다.

**Project → Game Resolution…**에서 논리 너비와 높이를 지정한다. 기본값은 800×450이며 각 축에 1~16384의 정수를 넣을 수 있다. 창을 늘리면 같은 비율로 확대되고 남는 부분에는 여백이 생긴다. Scene 편집 중에는 카메라 바깥쪽도 작업할 수 있다.

픽셀 아트 게임은 같은 창에서 **Pixel art: integer scale**을 켠다. 배포 게임은 SDL의 정수 배율 표시(`SDL_LOGICAL_PRESENTATION_INTEGER_SCALE`)로 게임 픽셀 하나를 장치 픽셀 1·2·3…개로만 그리고, 남는 부분은 여백으로 둔다. PNG 스프라이트는 보간하지 않고(nearest) 그리며, 카메라가 보는 영역의 왼쪽 위를 게임 픽셀 단위로 반올림해 스프라이트가 움직일 때 줄이 흔들리지 않는다. 글자는 지금처럼 부드럽게 축소한다. 에디터 Game 패널의 **Fit**도 같은 규칙으로 배율을 내림한다. 옵션은 `GameSettings`의 `integerScale: true`로 저장되며, 꺼 두면 키를 쓰지 않는다. [DisplayLab 검증](../../engine/tests/results/engine-features-1/README.md#168-정수-배율-표시)

**Scene → Create Camera**로 여러 카메라를 배치한다. **Camera 2D → Set as start camera**로 시작 시점을 선택하고 **Frame Camera**로 편집 뷰를 맞춘다. Primary 중 한 대가 활성화되고 Extra·Minimap은 동시에 표시할 수 있다. **Follow**로 플레이어를 지정하거나 카메라를 자식으로 두어 따라가게 할 수 있으며, `tick.Cameras.TrySetActive(id)`로 다른 카메라를 요청할 수 있다. 월드 오브젝트가 있는 씬에는 카메라가 필요하다. UI 전용 씬도 실행할 수 있다.

## 이동과 입력 스크립트

`Scripts/PlayerMovement.cs`처럼 파일명과 같은 이름의 공개 클래스가 `GameBehaviour`를 상속한다. [프로젝트 스크립팅](scripting.md)에서 공용 타입, 생명주기, 타입 참조를 확인할 수 있다. 아래 스크립트를 붙이고 Inspector의 **MoveSpeed**를 바꾸면 고정 업데이트에서 초당 이동 거리가 달라진다.

```csharp
using Engine.Core;

public sealed class PlayerMovement : GameBehaviour
{
    [FormerlySerializedAs("Speed")]
    public float MoveSpeed = 240f;

    public override void FixedUpdate(ScriptTick tick)
    {
        tick.Move(tick.Input.Move * MoveSpeed * tick.DeltaTime);
    }
}
```

- 고정 업데이트는 게임 시간 1초당 60회다. 일시정지·배속·UI 시간은 [시간 제어](time.md)에서 설정한다. `tick.Move`에는 해당 틱의 변위를 전달한다. 자식과 충돌체가 있는 오브젝트도 같은 이동 경로를 사용한다.
- **Project → Input Actions…**에서 액션을 만들고 키보드·마우스·패드를 연결한다. `tick.Input.Move`는 네 이동 액션을 길이 1 이하의 벡터로 합친다. `InteractPressed`는 `Pressed("Interact")`의 단축형이다. [액션 편집과 스크립트 API](input-actions.md)
- Inspector는 공개 인스턴스 필드 중 `int`, `float`, `double`, `bool`, `string`, 오브젝트 참조용 `Guid`, 컴포넌트 참조용 `ComponentReference<T>`, 씬·프리팹 참조용 `SceneReference`·`PrefabReference`를 지원한다. 편집할 필드는 초기값을 생략하거나 상수를 사용한다. 같은 스크립트라도 Inspector 값은 오브젝트별로 씬에 저장된다.

## 벽, 열쇠, 다음 방

1. Player와 벽에 **Box Collider 2D**를 붙인다. **Is Trigger**가 꺼져 있으면 이동 중의 경로를 검사해 벽에서 멈추거나 벽면을 따라 이동한다. 타일맵에서는 타일셋의 **Collision**에서 도형을 지정한다.
2. 열쇠에는 **Is Trigger**를 켜고 스크립트를 붙인다. 열쇠 스크립트의 `OnTriggerEnter(ScriptContact contact)`에서 `contact.OtherId`를 Inspector로 지정한 Player 참조와 비교한다. 겹친 다음 틱부터는 `OnTriggerStay`, 벗어날 때는 `OnTriggerExit`를 받는다. [양쪽 오브젝트의 접촉 이벤트](collision.md#진입유지이탈-이벤트)
3. KeyRoom의 `KeySequence`는 `contact.Session.Set("key")`로 획득 상태를 기록하고, 열쇠 그림을 숨긴다. Inspector에서 연결한 `KeyAcquired` 이벤트를 받은 `DoorLock.Unlock`이 열린 PNG로 바꾸고 충돌체를 끈 뒤 열림 애니메이션과 효과음을 재생한다. 문 카메라로 전환했다가 일정 시간이 지나면 플레이어 카메라로 돌아온다. [KeySequence.cs](../../samples/KeyRoom/Scripts/KeySequence.cs)
4. 출구의 `RoomExit`는 `contact.Session.Has("key")`를 확인하고 전환 문구와 함께 `LoadSceneAsync`로 다음 방을 요청한다. [씬 전환과 리소스 수명](scene-streaming.md)에서 추가 씬과 로딩 화면의 동작도 확인할 수 있다. 세션 플래그는 같은 Play의 씬 전환 동안 유지된다. 새 Play는 새 세션을 시작한다. 배포 시 목적지 씬도 **Included scenes**에 포함한다. [RoomExit.cs](../../samples/KeyRoom/Scripts/RoomExit.cs)
