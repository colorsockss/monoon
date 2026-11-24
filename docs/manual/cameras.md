# Camera 2D: 추적, 분할 화면, 미니맵

**Scene → Create Camera**로 카메라를 만들고 Inspector에서 설정한다. [CameraLab](../../samples/CameraLab)은 두 플레이어의 시점을 좌우로 나누고, 오른쪽 위에 전체 맵을 그리는 예제다. **방향키 / WASD**로 움직이거나 **Space**를 누르면 두 플레이어가 반대 모서리로 이동한다. Space를 다시 누르면 돌아온다.

## 플레이어를 부드럽게 따라가기

1. **Output → Primary**인 카메라에서 **Follow**에 Player를 지정한다. 대상의 부모 변환을 포함한 사각형의 월드 중심을 따라간다. 카메라의 Transform은 시작 시점이므로, 시작 위치를 플레이어 근처에 두면 처음부터 자연스럽다.
2. **Half-life**는 추적 오차가 절반으로 줄어드는 데 걸리는 초다. 예를 들어 0.16이면 가만히 있는 목표까지 남은 거리가 0.16초마다 절반이 된다. 0은 즉시 추적이다. 엔진은 충돌로 결정된 위치를 고정 틱마다 읽고 렌더 프레임 사이에는 위치를 보간한다. Follow가 None이면 기존처럼 카메라 Transform(부모의 이동 포함)을 사용한다.
3. **Bounds → Limit to map**을 켜고 월드 좌표의 **X / Y**와 **W / H**를 입력한다. 예를 들어 `0, 0, 1600, 896`이다. 카메라는 화면 전체가 이 사각형 안에 있도록 멈춘다. 맵이 시야보다 작으면 빈 바깥 공간이 노출되지 않도록 필요한 만큼만 Zoom을 올린다. 이 조정은 해당 뷰에만 적용되고 Inspector의 Zoom은 바뀌지 않는다.

Follow 대상이 비활성화되거나 제거되면 마지막 시점을 유지한다. 같은 대상이 다시 활성화되면 그 위치를 따라간다. 플레이어와 카메라를 같은 서브트리에서 복제하거나 프리팹으로 만들면 새 인스턴스의 플레이어를 따라간다.

## 데드존, 앞보기, 여러 대상 담기

Follow나 Frame(그룹)이 있는 카메라에서 Inspector의 다음 행을 쓴다. 모두 0 또는 비움이 기본값이고, 기존 씬은 그대로 읽힌다.

| 행 | 동작 |
| --- | --- |
| **Deadzone** (W, H) | 화면 중앙의 월드 픽셀 사각형. 대상이 이 안에서 움직이면 카메라가 멈춰 있고, 가장자리를 넘은 만큼만 따라간다. 예: 플랫포머 `120 / 80`. |
| **Lead time** (앞보기) | 대상의 최근 틱 속도에 이 초를 곱한 만큼 진행 방향 앞을 비춘다. 예: `0.25`는 초당 300px로 달릴 때 75px 앞. 멈추면 앞보기도 사라진다. |
| **Frame** (그룹) | 이 오브젝트 그룹의 활성 멤버(와 Follow)를 감싸는 사각형의 중심을 비춘다. 그룹은 Inspector의 오브젝트 **Groups**로 정한다. |
| **Padding** | Frame 그룹 사각형 둘레의 여백(월드 픽셀). 사각형과 여백이 뷰포트에 들어가도록 Zoom을 낮추고, 가까이 모이면 Zoom 값까지 다시 확대한다. Zoom보다 크게 확대하지 않는다. |

Half-life는 위치와 그룹 프레이밍 줌 모두에 적용된다. **Bounds**를 켜면 맵 바깥이 보이지 않도록 줌이 그보다 작아지지 않고, 맵 가장자리에서는 데드존과 앞보기보다 경계가 우선한다. 활성 멤버가 하나도 없으면 마지막 시점을 유지한다. 계산은 고정 틱에서 하고 렌더 프레임 사이에는 위치·줌을 보간한다. 이 설정을 쓴 씬은 schemaVersion 33으로 저장되어 이전 플레이어는 읽지 않는다.

CameraLab 복사본에서 주 카메라에 데드존 120×80과 앞보기 0.25초를, 두 번째 카메라에 두 플레이어의 그룹 `Players`(여백 48)를 지정한 검증이 [CameraLab.Framing](../../engine/tests/CameraLab.Framing)이다. 플레이어가 서로 멀어지면 오른쪽 화면이 경계가 허용하는 0.446배까지 줌아웃하고, 가운데에서 만나면 1배로 돌아온다. [Mac 세 실행 경로 기록](../../engine/tests/results/engine-features-2/README.md#1611-카메라-데드존앞보기다중-대상)

## 분할 화면과 미니맵 구성하기

| Output | 언제 그리는가 | 용도 |
| --- | --- | --- |
| Primary | 활성 주 카메라 한 대 | 기본 화면, 연출용 카메라 컷. **Set as start camera**로 시작 카메라를 정한다. |
| Extra | 오브젝트가 활성인 동안 주 화면 뒤에 이어서 합성 | 두 번째 플레이어, 감시 카메라. **Order**가 클수록 나중에 그린다. |
| Minimap | Extra까지 그린 뒤 합성 | 기본 영역은 우상단이다. **Show view frames**로 주 카메라(흰색)와 Extra(하늘색)의 월드 시야를 표시한다. |

**Viewport → Custom region**을 켜고 화면 왼쪽 위 기준 **X, Y, W, H**를 0–1 비율로 입력한다. 두 플레이어를 좌우로 나누려면 Primary는 `0, 0, 0.5, 1`, Extra는 `0.5, 0, 0.5, 1`이다. 각자 Follow 대상을 정한다. 위아래 분할도 같은 방식으로 높이를 반씩 나누면 된다. 각 화면의 종횡비를 유지하며, 겹치는 곳에서는 나중 카메라가 자기 영역을 덮는다.

맵 전체를 표시하려면 Minimap의 Follow를 None으로 두고 Transform을 맵 중심에 놓는다. 예제의 1600×896 맵은 **Zoom 0.125**, **200×112 논리 픽셀**에서 정확히 들어맞는다. HUD는 모든 카메라가 그려진 뒤 게임 해상도 기준으로 한 번만 그린다. 샘플은 아래쪽 50px를 HUD 전용으로 비워 둔다.

## 스크립트에서 카메라 읽기

```csharp
if (tick.Cameras.TryGetView(cameraId, out var view))
{
    var cameraCenter = view.Center;                      // 실제 추적·경계 적용 후
    var worldFrame = view.World;                         // 월드 사각형
    var logicalPixel = view.WorldToScreen(worldPoint);   // 전체 게임 화면 기준
    var restored = view.ScreenToWorld(logicalPixel);
}
```

이 API는 마지막으로 완료한 시뮬레이션 상태를 반환한다. 렌더 보간이나 창의 레터박스는 논리 좌표에 포함하지 않는다. `TrySetActive(id)`로 Primary 사이를 전환할 수 있다. Extra와 Minimap은 자신의 오브젝트를 활성화·비활성화해 표시를 제어한다.

[Mac 실행 화면과 측정 기록](../../engine/tests/results/cameras/README.md)

## 화면 흔들림, 줌 펀치, 페이드·플래시·색 보정

CameraLab을 Play하고 **1**(패드 West)을 누르면 두 화면이 흔들리면서 순간 확대되고, **2**(East)는 흰색 플래시, **3**(LeftShoulder)은 검게 페이드 아웃·인, **4**(RightShoulder)는 따뜻하고 어두운 색 보정을 켜고 끈다. **F**(Start)로 게임 시간을 멈춘 뒤 **.**(오른쪽 트리거)를 누르면 한 틱씩 진행하며 효과를 살펴볼 수 있다. [ScreenFxDemo.cs](../../samples/CameraLab/Scripts/ScreenFxDemo.cs)

스크립트에서는 `tick.Session.Screen`을 사용한다. `UpdateUi`에서도 호출할 수 있고 씬을 바꿔도 세션 동안 유지된다.

| API | 동작 |
| --- | --- |
| `Shake(amplitude, seconds, frequency = 18)` | 최대 `amplitude` 논리 픽셀만큼 흔들고 `seconds` 동안 줄어든다. 진행 중인 흔들림을 대체한다. |
| `ZoomPunch(amount, seconds)` | 즉시 `amount`(0.15 = 15%)만큼 확대하고 `seconds` 동안 원래 배율로 돌아온다. |
| `Flash(color, seconds)` | 화면 전체를 `color`로 덮고 `seconds` 동안 사라진다. 알파가 시작 불투명도다. |
| `Fade(color, seconds)` | 화면 전체 덮개를 `color`로 바꾼다. `Color.Black`이면 페이드 아웃, `Color.Transparent`면 다시 보인다. 다음 호출까지 유지된다. |
| `Adjust(tint, brightness, seconds = 0)` | 월드에 `tint`를 곱하고 `brightness`(-1–1)만큼 검정 또는 흰색 쪽으로 옮긴다. `Adjust(Color.White, 0)`으로 끈다. |
| `Clear()` / `Current` | 모든 효과를 끈다. / 마지막 게임 틱의 값(흔들림 오프셋, 배율, 덮개·플래시·보정 색)을 반환한다. |

흔들림과 줌은 Primary와 Extra 화면에만 적용되고 미니맵은 그대로다. 그리는 위치만 바뀌므로 `TryGetView`와 충돌·추적 결과는 달라지지 않는다. 효과는 게임 시간으로 진행해 일시정지하면 멈추고, 흔들림 위상은 고정 시드에서 정해진다. 같은 틱에 같은 호출을 하면 입력 재생과 세 실행 경로에서 같은 프레임이 그려진다.

색 보정은 HUD를 제외한 카메라 화면에, 페이드와 플래시는 HUD까지 덮는다. 플레이어와 Scene Play는 월드를 창 픽셀 크기의 SDL3 렌더 타깃에 그린 뒤 곱하기·더하기 블렌드로 보정하고 창에 합성한다. 보정이 꺼져 있으면 렌더 타깃을 거치지 않는다. Windows·Linux 에디터의 Avalonia 미리보기는 같은 계산을 블렌드 채우기로 그린다.

### 흔들림과 깜빡임 줄이기

`tick.Ui.Preferences.ShakeScale`은 흔들림과 줌 펀치, `FlashScale`은 플래시 불투명도에 곱한다(0–1, 0이면 끔). CameraLab에서는 **5**(Back)로 100%와 25%를 바꾼다. 같은 위상으로 진폭만 줄어든다. 설정은 `Preferences.SaveTo(settings)`로 `player.settings`에 저장하고, 다음 실행의 `OnCreate`에서 `LoadFrom(settings)`로 복원한다. 키는 `accessibility.` 접두사를 쓰고, 범위를 벗어난 값은 무시한다. [플레이어 저장](player-data.md#파일-위치)

[Mac 세 실행 경로의 캡처와 기록](../../engine/tests/results/screen-effects/README.md)

## 패럴랙스 배경

배경 오브젝트에 **Add Component → Parallax 2D**를 붙인다. **Scroll X / Y**는 카메라 이동 중 레이어가 따라가는 비율이다. 1이면 일반 오브젝트처럼 월드와 함께 움직이고, 0이면 화면에 고정되며, 0.25이면 카메라 이동의 4분의 1만큼 화면에서 지나간다. 기준은 각 카메라의 첫 프레임이라 Play를 시작한 화면은 에디터에서 배치한 모습과 같다. **Repeat X / Y**를 켜면 그 축으로 카메라 시야를 채울 때까지 반복한다. Tiled 스프라이트는 타일 크기 단위로 시작점을 맞춰 한 번에 그리고, 그 밖의 스프라이트는 오브젝트 크기 간격으로 복사해 그린다.

Parallax 2D는 Sprite Renderer만 옮겨 그린다. 충돌체·강체·타일맵과는 함께 쓸 수 없고, 자식 오브젝트는 따라오지 않는다. 스크립트에서는 `tick.Cameras.TryGetParallaxOffset(layer, out var offset)`로 현재 카메라가 레이어에 더한 월드 오프셋을 읽는다. 정수 배율 프로젝트에서는 오프셋도 게임 픽셀 단위로 반올림한다.

[PlatformerLab](../../samples/PlatformerLab/)의 **Parallax** 씬을 Play하면 카메라가 Position Tween 2D로 좌우로 움직이고 하늘(0)·언덕(0.25)·나무(0.6)·땅(1)이 다른 속도로 지나간다. [세 실행 경로의 기록](../../engine/tests/results/engine-features-1/README.md#162-패럴랙스와-타일-반복-스프라이트)

## 대형 씬에서 두 카메라 확인하기

CollisionLab을 Play하고 숫자 **2**를 누르면 충돌체 10,000개가 있는 필드가 열린다. 방향키나 WASD로 주황색 사각형을 파란색 사각형 위아래로 움직여 본다. 두 사각형이 겹칠 때 발이 더 아래에 있는 쪽이 앞에 보인다. 오른쪽 위 미니맵에서도 같은 순서로 보이며, 흰 선은 기본 카메라의 시야다.

엔진은 큰 씬에서 각 카메라의 월드 시야와 겹치는 공간 격자 셀을 조회한다. 후보를 기존 레이어·순서·Y 정렬·그룹 순서에 맞춰 그리므로 같은 발 높이에서의 우선순위도 유지된다. 움직이는 스프라이트는 현재 렌더 프레임의 보간된 위치와 회전으로 조회한다. 타일맵·스켈레톤·스프라이트 풀은 소유 오브젝트의 사각형 밖에도 그릴 수 있어 항상 후보에 포함한다. [플랫폼별 비교와 재현 방법](../../engine/tests/results/render-candidates/README.md)
