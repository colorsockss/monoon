# BarrageLab 탄막 배칭과 풀링

[BarrageLab](../../samples/BarrageLab)을 열고 **Play**를 누른다(`Scenes/Showcase.scene`). 오른쪽 전함의 코어가 서로 반대 방향으로 도는 초록색과 주황색 탄막을 쏜다. 두 색을 합쳐 초당 1,440발이다. 화면 아래 패널에는 현재 활성 탄환, 누적 회수, 누적 재사용 수와 제출 방식이 표시된다.

{{BarrageLab}}

*BarrageLab 쇼케이스. 나선 탄막과 자동 볼트를 풀에서 꺼내 쓰고, B로 묶음·개별 제출을, P로 풀과 생성·제거를 바꾼다. 반사 레이저와 혜성 꼬리는 Line 2D와 Trail 2D다.*

| 키 | 동작 |
| --- | --- |
| **Space** | 탄막을 멈추고 전함의 레이저를 켠다. 다시 누르면 레이저를 끄고 탄막을 다시 쏜다. 멈춘 뒤에도 남아 있는 탄환은 계속 이동한다. 전부 빠져나가면 활성 수는 0이 되고, 재개하면 회수했던 슬롯부터 사용한다. |
| **B** | 묶음 제출과 개별 제출을 전환한다. 같은 풀과 PNG를 사용해 현재 탄막의 드로우콜을 비교할 수 있다. |
| **R** | 현재 제출 모드를 유지하고 발사 패턴과 누적 카운터를 처음부터 재시작한다. |
| **P** | 우주선 볼트의 [프리팹 풀과 생성·제거](#충돌체와-스크립트가-있는-프리팹-풀)를 전환한다. |

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

## 충돌체와 스크립트가 있는 프리팹 풀

스프라이트 풀의 탄환은 그림과 이동만 있는 슬롯이다. 탄환이 트리거에 닿거나 스크립트를 실행해야 하면 프리팹 인스턴스를 풀에 보관한다. BarrageLab 쇼케이스의 왼쪽 우주선은 방향키로 움직이고 7틱마다 [Bolt.prefab](../../samples/BarrageLab/Assets/Showcase/Bolt.prefab)을 한 발씩 쏜다. 볼트에는 Sprite Renderer, 트리거 콜라이더, PooledBullet 스크립트가 붙어 있다. 위아래로 떠다니는 드론 셋에 닿거나 화면을 벗어나면 볼트가 사라지고, 드론은 맞을 때마다 번쩍이다가 네 번째에 터진다. 아래 볼트 줄에서 생성·재사용 수를 확인한다.

| 키 | 동작 |
| --- | --- |
| **P** | 프리팹 풀(`Rent`·`Return`)과 매번 생성·제거(`Instantiate`·`Destroy`)를 전환하고 처음부터 다시 쏜다. 바꾸기 전 구간의 발사·명중·Exit·활성·생성·재사용 수를 로그에 남긴다. |
| **R** | 같은 방식으로 처음부터 다시 쏘고, 직전 구간의 기록을 로그에 남긴다. |
| **R** | 현재 방식을 유지하고 탄환, 표적 위치와 카운터를 처음부터 다시 시작한다. |

P와 R을 누르면 직전 구간의 결과가 Console에 한 줄로 기록된다. 같은 틱 수만큼 쏜 두 방식의 발사·명중·활성 수는 같다. 프리팹 풀에서는 생성 수가 동시에 활성인 최대 탄환 수 근처에서 멈추고 나머지는 재사용으로 집계된다.

```csharp
public PrefabReference Bullet;

// 발사하는 쪽: 풀에 반환된 인스턴스가 없을 때만 새로 생성한다.
var id = tick.Objects.Rent(Bullet, spawnPosition);
tick.Objects.GetComponent<PooledBullet>(id)!.Launch(direction * 260);

// 탄환 쪽: 풀에서 빌린 인스턴스는 반환하고, Instantiate로 만든 인스턴스는 제거한다.
public override void OnTriggerEnter(ScriptContact contact)
{
    if (!contact.Objects.Return(contact.ObjectId)) contact.Objects.Destroy(contact.ObjectId);
}
```

`Rent(prefab, position, parent)`의 위치는 `Instantiate`처럼 월드 좌표이고 반환값은 루트 GUID다. 풀은 월드마다 프리팹별로 하나씩 생긴다. `Return(id)`는 풀에서 빌린 루트에만 true를 반환한다. 이미 반환했거나 Instantiate로 만든 오브젝트에는 false를 반환한다. `PoolStats(prefab)`는 활성·대기·생성·재사용·반환 수를 제공한다. 반환한 인스턴스도 `Destroy`로 제거할 수 있으며, 제거된 인스턴스는 다시 대여하지 않는다.

엔진은 반환할 때 다음 상태를 정리한다.

- **수명 콜백**: 각 프리팹 오브젝트의 스크립트에서 `OnReturn`을 먼저 실행한 다음 `OnDisable`을 실행한다. 대여할 때는 `OnEnable` 다음에 `OnRent`를 실행한다. 처음 생성할 때만 `OnCreate`를 실행한다. 스크립트 필드는 엔진이 초기화하지 않으므로 이전 생애의 속도, 타이머, 카운터는 `OnRent`에서 다시 설정한다.
- **소유자와 변환**: 부모에서 분리해 씬 루트로 옮긴다. 다시 대여하면 지정한 부모 아래 지정한 위치에 놓는다. 루트와 자식의 로컬 위치·회전·크기·기울기, 자식의 활성 상태, 비활성화한 충돌체, 강체 속도는 프리팹에 저장된 값으로 되돌린다.
- **충돌**: 반환한 인스턴스는 물리 조회와 접촉 계산에서 빠진다. 닿아 있던 상대는 같은 틱 또는 다음 접촉 계산에서 `Exit`를 받는다. 같은 틱에 반환하고 다시 빌려도 이전 접촉은 `Stay`로 이어지지 않는다. 새로 빌린 인스턴스는 새로 생성한 인스턴스처럼 다음 틱부터 `FixedUpdate`와 접촉에 참여하고 `Enter`부터 받는다.
- **태그·그룹과 이벤트**: 풀에서 대기하는 동안 `FindByTag`·`FindByGroup` 결과에서 빠지고 게임 이벤트를 보내거나 받지 않는다. 프리팹 내부의 이벤트 연결은 다시 대여하면 같은 인스턴스 안에서 그대로 동작한다.
- **저장**: 반환하면 `TrackForSave` 추적이 해제된다. 다시 대여한 인스턴스를 저장하려면 다시 지정한다.

애니메이션, 트윈, 파티클, 스프라이트 교체(`SetSprite`·`SetSpriteTint`)처럼 다른 컴포넌트에서 바꾼 값은 초기화하지 않는다. 필요하면 `OnRent`에서 다시 설정한다. 처음 대여할 때와 풀이 비었을 때는 `Instantiate`와 같은 비용이 든다. 풀은 크기 제한이 없고 장면이 바뀔 때 함께 사라진다. [풀 구현](../../engine/src/Engine.Core/PrefabPools.cs) · [포탑](../../samples/BarrageLab/Scripts/BulletTurret.cs) · [탄환](../../samples/BarrageLab/Scripts/PooledBullet.cs) · [표적](../../samples/BarrageLab/Scripts/HitTarget.cs)

### 명중 수와 할당 비교

내보낸 게임에서 두 방식을 같은 입력으로 비교하려면 [헤드리스 재생 실행기](../../engine/tests/BarrageLab.PrefabPools/README.md)를 사용한다. 실행기는 각 방식으로 1,800틱을 재생하고 마지막 1,200프레임의 업데이트 시간과 관리 할당을 집계한다. [macOS Release·Native AOT 측정 결과](../../engine/tests/results/prefab-pools/README.md)

