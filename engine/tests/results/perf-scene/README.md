# 씬·프리팹 성능 (패키지 B·C)

2026-11-23, macOS 27.0.1 arm64, Apple M4 Max, AC 전원, .NET SDK 10.0.101. **측정 내내 QEMU Debian x64 게스트(`qemu-system-x86_64`, TCG 4코어)가 실행 중이었다.** 기준과 후보를 라운드마다 번갈아 실행해 부하가 양쪽에 같이 걸리도록 했다. 매 실행 전 qemu 상태를 기록했고 열 번 모두 실행 중이었다.

- 기준: 커밋 `b524375`을 `git archive`로 풀어 낸 별도 폴더. 그 폴더에서 `build-players.sh osx-arm64`로 만든 자체 플레이어 템플릿·AOT 키트로 빌드했다.
- 후보: 이 변경. 같은 방식으로 자체 템플릿을 만들었다.
- 실행: 기준 → 후보 순서로 5라운드. 표의 값은 중앙값, 괄호는 최소–최대. 두 중앙값의 차이가 두 범위의 합보다 클 때만 "개선"으로 적었다. [실행별 값](runs.tsv)

## 바꾼 것

| 패키지 | 파일 | 내용 |
| --- | --- | --- |
| B | `Engine.Core/SceneDocument.cs` | `SceneObject.GetComponent<T>()`를 `OfType<T>().FirstOrDefault()`(호출마다 열거자·반복기 할당)에서 `ImmutableArray` 구조체 열거자 반복으로 바꿨다. `RemoveComponent<T>()`도 이 경로를 쓴다. |
| B | `Engine.Core/Transform2D.cs` | 생성마다 호출되는 `CopyTransform`이 `Groups` 세터(SelectMany·Distinct·ToArray)를 다시 거치지 않고 이미 정리된 그룹 배열을 복제한다. |
| C | `Engine.Core/RuntimeObjects.cs` | `ReadPrefab`이 월드(`SceneGame`)마다 프리팹 ID별로 해석한 템플릿을 보관한다. 경로와 파일 수정 시각(에디터 Play에서는 중첩 프리팹 파일 포함)이 같으면 YAML을 다시 읽지 않는다. 인스턴스는 모든 컴포넌트 레코드를 복제하고 스크립트 `Values`·`EventLinks`, UI 텍스트 `Icons`·`Translations`, 드롭다운 `Options`, 타일맵 `ChunkFiles` 컬렉션을 새로 만든다. 타일 청크는 불변 사전이라 쓸 때 교체된다. |

GetComponent 옆의 다른 LINQ 도우미:

| 위치 | 호출 경로 | 처리 |
| --- | --- | --- |
| `SceneObject.Groups` 세터 | 생성마다(`CopyTransform`) | 위 표대로 수정 |
| `SceneObject.AddComponent`의 `Any` + 클로저 | 에디터 편집·프리팹 머티리얼라이즈 | 런타임 생성 경로가 아니어서 유지 |
| `SceneDocument.Cameras`·`Camera`·`PrimaryCameras` | 씬 로드 1회(`SceneGame` 생성자) | 유지 |
| `SceneDocument.DrawOrder()`·`WorldItems()` | 에디터 Scene 뷰만 | 유지 (패키지 E 범위) |
| `StoredComponents` 게터의 `ToArray` | 직렬화만 | 유지 |

`Instantiate` 안의 `ToDictionary`·`Select`·`ParentFirst` 할당과 Commit의 전체 재색인은 이번 범위가 아니다(증분 재색인 보류).

## BarrageLab 프리팹 풀 (C)

[재생 실행기](../../BarrageLab.PrefabPools/README.md), `--profile distribution`. 다시 시작한 뒤 601–1,799틱(1,199프레임)의 프레임당 업데이트 시간과 관리 할당.

| 배포본 | 방식 | 지표 | 기준 | 후보 | 판정 |
| --- | --- | --- | ---: | ---: | --- |
| Release | `Instantiate`·`Destroy` | 평균 | 1,171.0 µs (1,115.3–1,251.8) | 935.0 µs (781.0–1,008.5) | 범위가 겹쳐 개선으로 보지 않음 |
| Release | `Instantiate`·`Destroy` | p95 | 2,708.8 µs (2,135.1–2,839.2) | 1,753.3 µs (1,559.8–1,864.2) | 범위가 겹쳐 개선으로 보지 않음 |
| Release | `Instantiate`·`Destroy` | 프레임당 할당 | 967,661 B | 418,650 B | 개선 (−56.7%) |
| Release | `Instantiate`·`Destroy` | Gen0 | 147 | 65 | 개선 |
| Native AOT | `Instantiate`·`Destroy` | 평균 | 1,010.7 µs (990.0–1,060.2) | 535.0 µs (518.2–636.0) | 개선 (−47%) |
| Native AOT | `Instantiate`·`Destroy` | p95 | 1,664.3 µs (1,611.9–1,836.8) | 1,424.8 µs (1,370.2–1,561.9) | 범위가 겹쳐 개선으로 보지 않음 |
| Native AOT | `Instantiate`·`Destroy` | 프레임당 할당 | 969,429 B | 418,801 B | 개선 (−56.8%) |
| Native AOT | `Instantiate`·`Destroy` | Gen0 | 147 | 64–65 | 개선 |
| Release | `Rent`·`Return` | 평균 | 346.2 µs (277.3–471.2) | 326.1 µs (312.2–385.9) | 차이 없음 |
| Release | `Rent`·`Return` | 프레임당 할당 | 58,713 B | 53,870 B | 개선 (−8.2%, GetComponent·Groups) |
| Native AOT | `Rent`·`Return` | 평균 | 129.6 µs (127.6–131.8) | 129.0 µs (127.2–133.6) | 차이 없음 |
| Native AOT | `Rent`·`Return` | 프레임당 할당 | 58,688 B | 53,840 B | 개선 (−8.3%) |

할당 값은 실행마다 수십 바이트 이내로 같았다. 기준의 생성·제거 할당(967 KB)은 [10.x 측정](../prefab-pools/README.md)의 841 KB보다 크다. `b524375`까지 쌓인 변경 때문이며 이번 비교는 같은 날 같은 기계의 기준끼리만 한다.

## Scene.Loading과 나눠 읽기 (B)

[프레임 프로파일러 재생](../../CollisionLab.FrameProfiler/README.md)(1만 개 씬 진입 프레임, development Release)과 [나눠 읽기 재생](../../CollisionLab.SplitLoading/README.md)(`--runs 1`을 라운드마다, development Release).

| 지표 | 기준 | 후보 | 판정 |
| --- | ---: | ---: | --- |
| 로딩 프레임 시간 | 171.95 ms (168.51–184.86) | 146.39 ms (146.03–160.38) | 범위가 겹쳐 개선으로 보지 않음 |
| 그 프레임의 Scene.Loading | 155.45 ms (152.15–168.75) | 129.73 ms (128.92–144.07) | 범위가 겹쳐 개선으로 보지 않음 |
| 로딩 프레임 관리 할당 | 88,835,736 B (88.83–88.88 MB) | 54,438,376 B (54.40–54.45 MB) | 개선 (−38.7%) |
| 추가 로딩, 예산 4 ms: Scene.Instantiate 합계 | 60.38 ms (58.85–62.94) | 26.98 ms (25.45–27.93) | 개선 (−55%) |
| 추가 로딩, 예산 4 ms: Scene.Commit | 23.61 ms (22.90–37.08) | 18.83 ms (17.45–20.44) | 범위가 겹쳐 개선으로 보지 않음 |
| 추가 로딩, 예산 4 ms: 최장 프레임 | 42.91 ms (41.70–54.91) | 26.07 ms (24.60–32.29) | 범위가 겹쳐 개선으로 보지 않음 |
| 추가 로딩, 예산 0: Scene.Commit | 21.81 ms (21.25–24.54) | 16.06 ms (15.12–16.26) | 개선 (−26%) |
| 교체 로딩, 예산 4 ms: Scene.Instantiate 합계 | 67.06 ms (64.53–72.62) | 35.26 ms (26.05–67.56) | 후보 한 번이 67.56 ms여서 개선으로 보지 않음 |
| 교체 로딩, 예산 4 ms: Scene.Commit | 8.05 ms (7.65–8.37) | 7.38 ms (6.90–7.98) | 차이 없음 |

예산 0 경로는 Instantiate를 별도 구간으로 기록하지 않아 표에서 뺐다. 열 번의 나눠 읽기 실행 모두 `PASS`(모든 로딩 연결, 취소 후 자원 수 복귀)였다.

## 결정성 게이트 (후보, 이 Mac)

| 검사 | 빌드 | 결과 |
| --- | --- | --- |
| BarrageLab 프리팹 풀 | Release, Native AOT (distribution) | 5라운드 × 2방식 × 2배포본 모두 2,338 명중 / 7,200발, Exit 2,338, 재사용 6,721 (기준도 같음) |
| InventoryLab 입력 재생 `./monoon test replay` | Release, Native AOT | 두 저장 모두 171 B, SHA-256 `EED41DEFCBD7987A527DEB677C60DA8151AD0DE1616C717C646E92B26E70F795` |
| PlatformerLab Joints | Release, Native AOT, Scene Play | 세 trace 동일, 80,853 B, SHA-256 `a00b9e4b00b4599dfc235cd5987a997d736db9e4f9a2829280dc907da4fee8c5`, `Joints crossing: bridge True, pulley True, 280 ticks.` |
| KeyRoom 월드 저장 `KeyRoom.WorldCheckpoints/ci.py` | Release, Native AOT | 통과 (코인 생성·저장·복원·획득 기준 슬롯 일치, CarryCoin outside 1,488 B / collected 833 B) |
| KeyRoom 태그·그룹 Scene Play `KeyRoom.ObjectGroups/verify.py` | 패키징한 에디터 Scene Play | 통과 (Play 중 Coin 프리팹 생성 틱·획득 틱의 그룹·태그 목록, 다음 Play의 CarryCoin) |
| CollisionLab 프레임 프로파일러·나눠 읽기 재생 | development / distribution Release | 열 번 모두 `PASS` |
| `./monoon test` | Engine.Checks | `All checks passed.` 새 검사 `PASS runtime prefab template cache keeps instances independent and rereads a changed file` 포함 |
| 금지 파일 | BarrageLab Release·Native AOT, CollisionLab 빌드 | SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring 파일 0개 |

새 검사 [`RuntimePrefabChecks.Cached`](../../Engine.Checks/RuntimePrefabChecks.cs)는 같은 월드에서 TestChest를 두 번 생성한다. 첫 인스턴스의 스크립트 `Values`를 바꿔도 두 번째에 없고, 각 인스턴스의 Lid 참조는 자기 자식을 가리키며, 루트와 자식의 컴포넌트 레코드가 서로 다른 객체인지 확인한다. 이어 프리팹 파일의 루트 이름을 바꿔 저장하면 다음 생성이 새 이름을 쓰고 이미 만든 인스턴스는 그대로인지 확인한다.

### 참고: `InventoryLab.Replay/build_guest.py`의 Native AOT

`build_guest.py`는 Release 저장이 `EED41DEF…`로 일치한 뒤, 자체 임시 AOT 키트로 만든 Native AOT 플레이어가 시작 직후 `System.IO.FileNotFoundException: Could not find file 'Brutal.Sdl'.`로 중단됐다. 같은 명령을 기준 `b524375` 폴더에서 실행해도 똑같이 실패했으므로 이번 변경과 무관한 기존 문제다. Native AOT 저장은 위 표처럼 `player-templates` 키트로 빌드해 `./monoon test replay`로 확인했다.

## 확인하지 않은 것

- 시간 지표는 qemu 게스트가 실행 중인 상태에서만 측정했다. 할당·Gen0과 일부 시간 지표(AOT 생성·제거 평균, 나눠 읽기 Instantiate, 예산 0 Commit)만 범위 기준으로 개선이 확인됐다.
- 에디터 Play 중 다른 창에서 프리팹을 저장한 뒤 같은 Play에서 다시 생성하는 경로는 Engine.Checks(파일 직접 수정)로만 확인했다. 같은 수정 시각에 다시 쓴 파일은 감지하지 못한다(코드에 shortcut 주석).
- Windows·Linux x64와 게스트 재생, BarrageLab Scene Play 시간, GC 일시정지 시간은 측정하지 않았다.
