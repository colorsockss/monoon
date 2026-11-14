# BarrageLab 탄막 배칭과 풀링

[BarrageLab](../../samples/BarrageLab)을 열고 **Play**를 누른다. 발사기가 서로 반대 방향으로 도는 초록색과 주황색 탄막을 만든다. 두 색을 합쳐 초당 1,440발이다. 화면 아래에는 현재 활성 탄환, 누적 회수, 누적 재사용 수가 표시된다.

| 키 | 동작 |
| --- | --- |
| **Space** | 발사를 멈추거나 다시 시작한다. 멈춘 뒤에도 남아 있는 탄환은 계속 이동한다. 전부 빠져나가면 활성 수는 0이 되고, 재개하면 회수했던 슬롯부터 사용한다. |
| **B** | 묶음 제출과 개별 제출을 전환한다. 같은 풀과 PNG를 사용해 현재 탄막의 드로우콜을 비교할 수 있다. |
| **R** | 현재 제출 모드를 유지하고 발사 패턴과 누적 카운터를 처음부터 재시작한다. |

## 다른 게임에서 사용하기

월드 오브젝트에 **Sprite Renderer**를 붙이고 PNG와 크기를 정한다. 여기에 붙인 C# 스크립트에서 풀을 한 번 생성한다. 이 오브젝트의 Sprite Renderer가 풀 전체의 스프라이트와 정렬 순서를 제공한다. 각 탄환은 풀 안에서 위치·속도·색상을 가지며, Hierarchy에서는 발사기 오브젝트 하나로 관리한다.

```csharp
private SpritePool2D sprites;

public override void OnCreate(ScriptLife life)
{
    sprites = life.Objects.CreateSpritePool(life.ObjectId, 4096);
}

public override void FixedUpdate(ScriptTick tick)
{
    if (tick.Cameras.ActiveCameraId is not Guid id || !tick.Cameras.TryGetView(id, out var camera))
        return;
    sprites.Advance(tick.DeltaTime, camera.World);
    if (tick.Input.Pressed("Fire"))
        sprites.TrySpawn(tick.Position, new Vector2(190, 0), Color.White);
}
```

코드는 `System`, `System.Drawing`, `System.Numerics`, `Engine.Core`를 사용한다. 위치는 월드 중심 좌표, 속도는 초당 월드 픽셀이다. **Advance**에 유지할 월드 영역을 넘기면 탄환 사각형 전체가 영역을 벗어나는 틱에 슬롯을 돌려준다. 카메라가 여러 개면 게임이 유지할 영역을 지정한다. 렌더러는 각 카메라의 시야에서 다시 가시성을 검사한다. 텍스처는 공유하고, 반투명 탄환은 발사 순서대로 그린다.

풀은 생성할 때 슬롯과 렌더 버퍼를 할당한다. 용량에 도달하면 `TrySpawn`이 false를 반환하고 `Stats.Dropped`가 증가한다. `Stats.Returned`는 화면을 벗어나 반환된 횟수, `Stats.Reused`는 반환된 슬롯에 다시 발사한 횟수다. `Stats.Peak`은 최대 동시 활성 수다. `Reset()`은 기존 버퍼를 유지하고 빈 풀로 새 기록을 시작한다. [풀 구현](../../engine/src/Engine.Core/SpritePool2D.cs) · [샘플 스크립트](../../samples/BarrageLab/Scripts/SpiralBarrage.cs)

## 드로우콜과 메모리 비교

Mac의 Scene Play에서는 **Debug → Profiler**로 SDL 제출 수를 볼 수 있다. 독립 실행과 동일한 구간으로 측정하려면 새 CSV 경로로 `MONOON_FRAME_CAPTURE`를 지정하고 앱을 실행한다. 캡처는 측정할 때만 활성화한다. 아래 명령은 저장소 루트에서 새 임시 폴더에 캡처를 만든다.

```sh
capture_dir=$(mktemp -d)
MONOON_FRAME_CAPTURE="$capture_dir/release.csv" game-builds/BarrageLab/BarrageLab.app/Contents/MacOS/MoNoonPlayer
```

**draw_calls**는 한 렌더 프레임에서 SDL에 제출한 지오메트리, 사각형, 텍스트의 합이다. **update_bytes**는 직전 렌더부터 누적한 게임 업데이트, **draw_bytes**는 그 프레임의 게임 렌더와 제출 구간에서 실행 스레드가 할당한 관리 메모리다. CSV 기록, 에디터 Inspector 갱신, 백그라운드 스레드와 GPU 메모리는 이 측정에서 제외한다. [Mac 실행 결과와 원본 CSV](../../engine/tests/results/sprite-pools/README.md)
