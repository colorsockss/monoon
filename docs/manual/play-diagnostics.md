# Play 중 실행 진단

KeyRoom을 열고 **Play**를 누른다. 진단 도구는 에디터에서 실행 중인 월드를 관찰한다. 진단선과 관찰한 상태값은 Play 세션에만 표시된다.

## 오브젝트 상태

Hierarchy에서 오브젝트를 선택하고 Inspector의 **Runtime**을 펼친다. 활성 상태(자신이 꺼졌는지, 부모가 꺼졌는지), 월드 좌표, 생성된 프리팹의 출처, 트리거 겹침 수를 읽을 수 있다. 애니메이터에는 현재 상태·프레임, 오디오 소스에는 재생 상태가 나온다. 스크립트는 단순 타입의 인스턴스 필드를 표시한다. 비공개 필드를 포함하며 프로퍼티의 getter는 실행하지 않는다. Runtime에 표시하는 값은 읽기 전용이다.

KeyRoom에서는 **E**로 상자를 열고 Hierarchy 맨 아래에 생긴 **Coin**을 선택한다. Runtime에서 `Coin.prefab`, `Trigger`, `CollectCoin.available`을 확인한다. 플레이어가 코인을 획득하면 Hierarchy에서 사라진다. 값을 천천히 보려면 **Pause**, 한 물리 틱만 전진하려면 **Step**을 누른다.

## Play 중 값 조정과 선택 적용

1. KeyRoom에서 **Player**를 선택하고 **Play**를 누른다. Inspector의 **PlayerMovement → MoveSpeed**에 `320`을 입력하고 Enter를 누른다. 다른 필드로 포커스를 옮겨도 입력이 확정된다. 게임 화면에서 움직이면 곧바로 빨라진다.
2. **Stop**만 누르면 다음 Play는 원본 속도 `240`으로 시작한다.
3. 값을 유지하려면 Play 중 MoveSpeed를 바꾼 뒤 오른쪽 체크를 켜고 Inspector 상단의 **Apply selected**를 누른다. 직접 바꾼 필드는 처음에 체크된다. 함께 바꾼 **Local X**의 체크를 끄면 속도만 적용된다.
4. **Stop** 후 macOS에서 **Cmd+Z**, Windows·Linux에서 **Ctrl+Z**를 한 번 누르면 그 적용 전체가 되돌아간다. 복원한 값을 파일에도 남기려면 씬을 저장한다.

실시간 편집은 월드 오브젝트의 로컬 위치·회전·스케일과 스크립트가 Inspector에 공개한 `int`·`float`·`double` 필드를 지원한다. **Apply selected**는 현재 선택한 오브젝트에서 직접 입력한 값만 원본 씬으로 옮긴다. Play 중 스크립트나 물리로 달라진 위치는 자동 적용 대상에 들어가지 않는다.

저장된 씬에 미저장 편집이 없으면 선택 적용과 함께 파일도 저장한다. 새 씬이거나 기존 편집이 남아 있으면 Stop 후 **Save Scene**으로 저장한다. 런타임에 생성된 프리팹과 추가 로드한 씬의 오브젝트는 Play 안에서만 조정할 수 있다. 로컬 Transform을 원본에 적용하려면 원본과 현재 부모가 같아야 한다.

[KeyRoom 세 OS 조작과 측정 결과](../../engine/tests/results/play-inspector/README.md)

## 충돌체와 질의

| Play 상단의 Debug를 눌러 여는 하단 도구 | 읽는 방법 |
| --- | --- |
| **Colliders** | 초록은 일반 충돌체, 분홍은 트리거다. 비활성 충돌체는 제외한다. 선택한 충돌체는 두껍게 표시한다. 원·다각형·회전한 상자와 타일맵의 충돌 셀은 해당 카메라에 보이는 월드 위치에 그려진다. |
| **Queries** | 켠 뒤 실제로 호출된 Overlap, Raycast, Sweep과 이동 충돌 검사(Movement)를 표시한다. 파랑은 빗나감, 노랑은 적중이다. 캐스트의 흰 십자와 짧은 선은 접점과 법선이다. 아래 상태줄에서 최근 질의의 결과와 첫 대상을 읽는다. |
| **Cast ray…** | 활성 오브젝트를 선택하고 버튼을 누른 뒤 게임 화면의 한 지점을 클릭한다. 오브젝트 중심에서 클릭한 위치까지 레이를 한 번 쏜다. 자신을 제외하고 트리거를 포함한다. 이 클릭은 게임 입력에 전달되지 않는다. |

선은 최근 60 물리 틱, 최대 64개 질의까지 표시한다. **Pause**하면 그대로 멈춘다. 카메라가 여러 개면 각 뷰의 좌표와 클리핑을 적용하고, 클릭한 곳에서 가장 위에 그려지는 카메라로 레이를 계산한다. 질의 표시를 끄거나 Play를 중지하면 기록을 비운다.

## 콘솔

**Project → Console…** 또는 **Debug → Console…**에서 연다. Play 시작·종료, 씬 전환, 수동 레이 결과, 컴파일·실행 오류와 스크립트 로그가 실시간으로 들어온다. 등급(Info/Warning/Error)과 검색어로 필터링한다. **Clear**는 전체 기록을 지운다. 최근 1,000개까지 보관하며, 프로젝트 스크립트 경로가 있는 메시지를 더블클릭하면 설정한 외부 에디터로 연다.

```csharp
tick.Session.Log("Chest opened.");
tick.Session.Log("Loot table is empty.", GameLogLevel.Warning);
```

## 프레임 프로파일러

**Debug → Profiler** 또는 **Run → Frame Profiler**로 Scene 아래에 연다. 최근 180개 프레임을 보관한다. Stop 후에는 마지막 기록을 읽을 수 있고 새 Play는 빈 기록으로 시작한다.

| 지표 | 측정 범위 |
| --- | --- |
| Frame / avg / max | 완료된 Scene 프레임 사이의 경과 시간과 기록 구간 내 평균·최댓값. 그래프의 기준선은 16.7 ms, 하단 노란 눈금은 GC가 발생한 구간이다. |
| Update / Render | 프레임 사이에 게임 루프를 갱신한 시간의 합과 렌더 호출의 경과 시간. Mac의 Render는 SDL Present 호출까지 포함한다. GPU 실행 시간은 측정하지 않는다. |
| SDL draws / Avalonia draws | Mac SDL에서는 배치가 실제 제출된 횟수, 직접 도형·텍스처·텍스트 호출을 센다. Clear·Present와 설정 변경은 제외한다. Avalonia 경로는 그리기 명령 수이므로 두 경로의 숫자를 그대로 비교하지 않는다. 오버레이도 수치에 포함된다. |
| Alloc / GC 0/1/2 | 해당 구간에서 UI·렌더 스레드가 프레임당 할당한 평균 바이트(KiB)와 프로세스의 세대별 GC 횟수다. Inspector·콘솔 등 에디터 작업도 포함한다. |

[이 Mac의 KeyRoom 검증 기록](../../engine/tests/results/play-diagnostics/README.md)

## 느린 프레임의 작업 분석

프로파일러 아래 표는 프레임 하나에서 엔진이 수행한 작업을 호출 관계대로 나눠 보여 준다. 그래프를 클릭하면 해당 프레임이 선택되고 주황 세로선이 표시된다. 선택하지 않았거나 선택한 프레임이 기록 범위를 벗어나면 기록된 프레임 중 가장 느린 프레임을 표시한다. Stop 후에도 마지막 기록에서 프레임을 고를 수 있다.

| 열 | 의미 |
| --- | --- |
| Work | 작업 이름. 들여쓴 항목은 위 항목 안에서 호출됐다. 같은 부모 아래의 같은 작업은 한 줄로 합치고 Total이 긴 순서로 나열한다. |
| Calls | 그 프레임에서 호출된 횟수 |
| Total ms | 아래 작업을 포함한 시간 |
| Self ms | Total에서 바로 아래 작업들의 Total을 뺀 시간 |

| 작업 | 측정 범위 |
| --- | --- |
| Frame | 완료된 Scene 프레임 사이 전체. Self는 아래 항목에 속하지 않은 시간이다. 에디터 UI의 배치·그리기, GC 일시정지, 다음 프레임까지의 대기가 여기에 들어간다. |
| UpdateUi · Simulate | 게임 루프의 UI 갱신과 고정 틱. Simulate의 Calls는 그 프레임에서 실행한 고정 틱 수다. |
| `스크립트.메서드` | `FixedUpdate`, `UpdateUi`, `OnUiClick`, `OnUiValueChanged`, 접촉·트리거 콜백과 `OnCreate` 등의 생명주기 콜백. 스크립트 타입 이름으로 구분한다. |
| Physics.Overlap · Raycast · Sweep | `IPhysics2D` 질의 한 번이 호출 한 번이다. 엔진 내부의 발 IK 레이도 Raycast로 집계한다. |
| Physics.Move · Rigidbodies · Contacts | 스크립트 이동과 `TryMoveStep`의 충돌 검사, 리지드바디 단계, 접촉 판정과 접촉 콜백 |
| UI.Input · UI.Layout · UI.Draw | 화면 UI의 입력 처리, 배치 계산, 그리기 |
| Render · Render.World · Present | 프레임 그리기 전체, 카메라별 월드 그리기, SDL Present 호출 |
| Scene.Loading · Tiles.Streaming | 씬 교체·추가 로드, 타일 청크 스트리밍 |
| Lifecycle · Animation · Effects · Cameras · Audio | 생명주기 콜백 처리와 각 갱신 단계 |
| Editor | 게임 갱신 직후 에디터의 Hierarchy·Inspector·진단 갱신 |
| Profiler.Write | 개발용 게임이 직전 프레임을 파일에 쓰는 시간 |

[CollisionLab](../../samples/CollisionLab)에서 Play 후 **2**로 충돌체 1만 개 씬을 열고 **R**을 누른다. 그래프에서 R 직후 솟은 프레임을 클릭하면 `QueryField.FixedUpdate` 아래 Physics.Overlap·Raycast·Sweep이 각각 1,344회(경로 96개 × 14회) 표시된다. 씬 전환 프레임에서는 Scene.Loading 외에 Frame의 Self 시간이 크게 나타난다. 이 시간은 에디터가 1만 개 오브젝트를 표시하는 데 사용한다.

게임 스레드에서 실행된 작업만 기록한다. 다른 스레드에서 호출한 질의는 집계하지 않는다. 시간은 `Stopwatch` 타임스탬프로 측정하며 샘플을 기록할 때 관리 메모리를 할당하지 않는다. 프레임당 노드는 최대 256개, 깊이는 32단계까지 기록한다. 이를 넘은 호출은 제목에 `samples dropped`로 표시된다. 측정 비용은 시간에 포함되며, 이 Mac에서 질의 1회의 p95가 약 0.1 µs 늘었다. GPU 실행 시간은 측정하지 않는다.

### 개발용 게임에서 기록

`--profile development`로 빌드한 플레이어를 `MONOON_FRAME_PROFILE`에 CSV 경로를 지정해 실행하면 모든 프레임의 작업을 기록한다. 창 실행, `--replay-input` 헤드리스 재생, `--smoke-test`에서 사용할 수 있다. 이미 있는 파일을 지정하면 덮어쓰지 않고 오류로 종료한다. `./monoon profile`은 프레임 시간의 평균·p95·최대와 가장 느린 프레임 다섯 개를 출력하고 가장 느린 프레임이나 `--frame`으로 지정한 프레임을 표로 출력한다.

```sh
./monoon build samples/CollisionLab release --profile development
MONOON_FRAME_PROFILE=/tmp/collision-frames.csv \
  game-builds/CollisionLab/Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer
./monoon profile /tmp/collision-frames.csv
./monoon profile /tmp/collision-frames.csv --frame 91
```

플레이어는 실행 파일 옆 `MoNoonBuild`의 프로필이 `development`일 때만 기록한다. 배포용 빌드(에디터의 **Build Game…** 포함)는 경고 한 줄을 출력하고 변수를 무시한다. 이때 프로파일러를 설치하지 않으므로 각 측정 지점의 비용은 null 확인 한 번이다. 작업 표와 그래프는 에디터에만 있으며 게임에는 프로파일러 화면이 없다.

`--replay-input` 헤드리스 재생은 텍스처와 오디오를 준비하지 않는다. `MONOON_REPLAY_OFFSCREEN=1`을 함께 지정하면 창과 오디오 장치 없이 SDL 소프트웨어 렌더러에 텍스처를 올리고, 기록된 프레임 시간에 맞춰 프레임 사이를 기다린다. 기다린 시간은 프로파일에 넣지 않는다. 재생 시작과 끝에 상주 파일·디코딩 버퍼·SDL 텍스처 수를 출력한다. 씬 로딩을 측정할 때 사용한다. [나눠 읽기 측정](../../engine/tests/CollisionLab.SplitLoading/README.md)

### CSV 형식

첫 줄은 열 이름이다. 프레임마다 Frame부터 깊이 우선 순서로 노드 한 개당 한 행을 기록한다. 다른 검사에서 프레임 시간 목록이 필요하면 `node`가 0인 행의 `inclusive_us`를 읽는다.

| 열 | 값 |
| --- | --- |
| `frame` | 1부터 증가하는 프레임 번호 |
| `node` · `parent` · `depth` | 프레임 안의 노드 번호와 부모 노드 번호, 깊이. Frame은 0 · -1 · 0이다. |
| `name` · `calls` | 작업 이름과 호출 수 |
| `inclusive_us` · `self_us` | Total과 Self 시간(µs, 소수 첫째 자리) |
| `alloc_bytes` · `gen0` · `dropped` | Frame 행에만 기록한다. 게임 스레드의 관리 할당 바이트, Gen0 수집 횟수, 기록하지 못한 호출 수 |

[CollisionLab의 Scene Play·개발용 게임 검증 기록](../../engine/tests/results/frame-profiler/README.md)
