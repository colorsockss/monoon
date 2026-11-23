# 물리 성능 정리: 접촉 탐색·Box2D 동기화·타일 도형 (성능 묶음 A·F·G)

2026-11-23, macOS 27.0.1 / Apple M4 Max(arm64), AC 전원. 기준(baseline)은 `b524375`를 `git archive`로 따로 풀어 자체 플레이어 템플릿을 만든 사본이고, 후보(candidate)는 이 브랜치다. 두 쪽을 번갈아 5회씩 실행해 중앙값과 범위(최소–최대)를 적었다. **측정하는 동안 QEMU Linux 게스트(`qemu-system-x86_64`, 조정 담당자 소유)가 계속 실행 중이었다.** 그래서 감사 문서의 "게스트 정지" 조건은 지키지 못했고, 부하는 번갈아 실행으로 양쪽에 고르게 나눴다. 중앙값 차이가 두 범위의 합보다 클 때만 개선으로 적었다.

## 바꾼 것

| 묶음 | 파일 | 내용 |
| --- | --- | --- |
| A 접촉 탐색 | [SceneContacts.cs](../../../src/Engine.Core/SceneContacts.cs) | 왼쪽 끝 좌표 정렬을 틱 사이에 유지하고 삽입 정렬로 고친다(순서가 크게 바뀌면 기존 `List.Sort`로 돌아간다. 정렬 기준이 (Left, Id)의 전순서라 결과는 같다). 충돌체가 64개 이상이면 접촉 범위를 평균 높이의 세로 행으로 나누고, 각 충돌체는 자기 행에서 자기보다 뒤 순위인 후보만 이진 탐색으로 찾는다. 같은 쌍은 처음 공유하는 행에서만 세고, 여러 행에 걸친 충돌체는 후보 순위를 정렬한 뒤 방문한다. 그래서 쌍 방문 순서가 (순위 a, 순위 b) 그대로이고 `nextContacts`·`crossings` 삽입 순서와 HashSet 순회 순서가 바뀌지 않는다. 경계 판정은 기존 `<=`(맞닿음 포함) 그대로다. 행 좌표는 y에 단조이므로 맞닿은 두 구간은 반드시 한 행을 공유한다. 4행보다 많이 걸친 충돌체, 높이가 음수·NaN인 경계, 충돌체가 64개 미만인 월드는 기존 왼쪽 끝 스윕을 그대로 쓴다. |
| F Box2D 동기화 | [ScenePhysicsWorld.cs](../../../src/Engine.Core/ScenePhysicsWorld.cs) | 틱마다 만들던 `HashCode` 서명을 정확한 값 비교로 바꿨다. 타일맵은 (타일맵 값, 스트리밍 Loads·Unloads·Edits, PoseRevision, 위치, 각도), 강체가 아닌 콜라이더는 (PoseRevision, 고체 여부)를 저장해 비교하고, 강체는 움직이는 멤버마다 (오브젝트, 고체 여부, PoseRevision) 목록을 비교한다. `HashCode`는 프로세스마다 시드가 달라 충돌하면 같은 게임이 실행마다 다른 틱에 바디를 다시 만들 수 있었다(결정성 수정). Box2D 바디의 활성 상태는 SyncPhysics만 바꾸므로 관리 측 플래그로 기억해 바디마다 하던 `IsEnabled` P/Invoke를 없앴다. |
| G 타일 도형 | [ScenePhysics.cs](../../../src/Engine.Core/ScenePhysics.cs), [Tilemap2D.cs](../../../src/Engine.Core/Tilemap2D.cs) | 타일 후보마다 새로 만들던 월드 좌표 도형(Box2D 헐 포함)을 (타일맵, 셀)마다 한 번 만들어 둔다. 로컬 도형을 만들어 평행 이동하면 부동소수 반올림이 달라지므로 기존 `TileCollision.WorldShape` 결과를 그대로 저장한다. 셀의 타일 ID가 바뀌면 그 셀을, 타일맵의 변환 행렬(비트 단위 비교)·배치·셀 크기가 바뀌면 그 타일맵의 캐시를 다시 만든다. 항목이 65,536개를 넘으면 비운다. 측정해 보니 타일 후보 비용의 대부분은 헐보다 `Tilemap2DComponent.InView`가 청크(최대 1,024셀)를 통째로 순회하는 데 있었다. 검색 범위가 청크의 셀 수보다 작으면 범위 안 셀만 인덱스 순서로 조회하게 했다. 같은 셀을 같은 순서로 돌려준다. |

BRUTAL에는 해당하는 기능이 없다(접촉 쌍 관리·타일 셀 질의는 [Box2D 전환](../physics-box2d/README.md)에서 MoNoon 쪽에 남기기로 한 부분). 새 의존성은 없다.

## 측정

| 지표 | 기준 중앙값 [범위] | 후보 중앙값 [범위] | 판정 |
| --- | --- | --- | --- |
| A: CollisionLab Field10000 재생, 프레임 11–150의 Physics.Contacts(µs, Release 개발용) | 3,193 [2,987–3,421] | 668 [654–722] | 개선(−79%) |
| A: 정적 충돌체 1만 개 벤치, Physics.Contacts(ms) | 1.473 [1.457–1.674] | 0.594 [0.490–0.856] | 개선(−60%) |
| F: 같은 벤치(강체 1개 포함), Physics.Rigidbodies(ms, SyncPhysics + Box2D Step) | 0.165 [0.146–0.184] | 0.187 [0.155–0.304] | 차이 없음(범위 겹침) |
| G: 64×64 고체 타일 위 충돌체 1천 개 벤치, Physics.Contacts(ms) | 6.317 [5.975–6.784] | 0.920 [0.893–0.942] | 개선(−85%) |
| G: 같은 벤치, 프레임당 할당 | 1,266,320 B | 632,032 B | −50% |
| G: PlatformerLab 타일 충돌 380틱 재생, 프레임당 Physics.Move+Contacts 중앙값(µs) | 5.0 [5.0–5.2] | 4.9 [4.4–5.1] | 차이 없음(장면이 작음) |

- 개별 실행값(기준/후보): Field10000 3,213·3,193·2,987·2,987·3,421 / 713·722·654·667·668µs, 타일 벤치 6.317·6.784·6.193·5.975·6.370 / 0.920·0.903·0.893·0.942·0.938ms.
- G의 몫을 나누려고 후보에서 도형 캐시만 잠시 끄고 타일 벤치를 3회 돌렸다: 1.114·1.097·1.103ms, 1,306,320B/프레임. 즉 `InView` 조회가 6.3 → 1.1ms, 도형 캐시가 1.1 → 0.92ms와 할당 절반을 맡는다.
- F는 시간 이득이 측정 잡음보다 작다. 따로 잠시 넣은 표식으로 보면 SyncPhysics 안에서 정적 바디 순회가 약 0.08ms, 제거 확인 순회(바디마다 Guid 사전 조회 2회)가 약 0.07ms였다. 이 변경의 목적은 결정성이고, 제거 확인 순회는 손대지 않았다.
- 정적 벤치의 프레임당 할당은 양쪽 모두 848B(기준은 가끔 720B)로 변화가 없다.

### 재현

```sh
# 기준 사본: git archive b524375 | tar -x -C "$base"; 양쪽에서 engine/scripts/build-players.sh osx-arm64
./monoon build samples/CollisionLab release --profile development --output "$work/collision"
./monoon build samples/CollisionLab release --profile distribution --output "$work/collision"
python3 engine/tests/CollisionLab.FrameProfiler/replay.py "$work/collision/Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer" \
  "$work/collision/CollisionLab.app/Contents/MacOS/MoNoonPlayer" "$work/replay"   # development.csv의 Physics.Contacts
dotnet build engine/benchmarks/Physics2D -c Release -m:1
dotnet engine/.artifacts/bin/Physics2D/release/Physics2D.dll              # 정적 1만 개 + 강체 1개
dotnet engine/.artifacts/bin/Physics2D/release/Physics2D.dll tiles .      # 타일 64×64 + 충돌체 1천 개
```

[Physics2D 벤치](../../../benchmarks/Physics2D/Program.cs)는 헤드리스로 60틱 예열 뒤 300틱의 `FrameProfiler` 기록에서 중앙값·최소·최대를 출력한다. PlatformerLab 수치는 [TileCollision](../../PlatformerLab.TileCollision/README.md)의 준비 사본을 `--profile development`로 빌드해 `MONOON_FRAME_PROFILE`과 `Traverse.inputreplay`로 실행한 CSV에서 얻었다.

## 결정성 기준값

변경 뒤 후보 빌드에서 확인했다. 모두 기존 값과 정확히 같다.

| 검사 | Release | Native AOT | 기준값 |
| --- | --- | --- | --- |
| PlatformerLab.Joints 300틱 기록 SHA-256 | `a00b9e4b00b4599d…` | `a00b9e4b00b4599d…` | `a00b9e4b…` |
| PlatformerLab.TileCollision 380틱 기록 SHA-256 | `f30ed6864b3cf99f…` | `f30ed6864b3cf99f…` | `f30ed686…` |
| CollisionLab.Spatial 대상 순서 해시(1천·1만 개) | `32FA7AB7…`·`B5C817DB…` | `32FA7AB7…`·`B5C817DB…` | 같음 |
| BarrageLab.PrefabPools 명중 수(풀·생성 두 방식) | 2,338 | 2,338 | 2,338 |
| CollisionLab.FrameProfiler 판정(Release 개발용·배포용, 5회 × 2) | 통과 | – | – |
| `Engine.Checks --physics-only` | 통과 | – | – |
| `./monoon test`(KeyRoom 세 플랫폼 내보내기 포함) | 통과(`All checks passed.`) | – | – |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·벤치) | 없음 | 없음 | – |

에디터 Scene Play(이 브랜치로 만든 `editor/MoNoon.app`): Joints 기록 SHA-256 `a00b9e4b…`(`verify.py compare`로 Release·AOT와 80,853바이트 동일), TileCollision 기록 `f30ed686…`(타일셋 편집·복원 포함), CollisionLab.FrameProfiler `PASS`(R 뒤 115번 프레임, 질의 4,032회가 181.6ms 중 89.4%).

### 새 검사

- [ContactChecks.Order](../../Engine.Checks/ContactChecks.cs): 같은 왼쪽 끝의 열 10개 × 12행(맞닿은 셀·겹친 행·원형·트리거 섞음), 여러 행에 걸친 높은 벽 2개, 움직이는 충돌체 5개(빠른 트리거 통과 포함), 20·22틱의 충돌체 끄고 켜기를 40틱 실행해 Enter/Stay/Exit 15,414개의 순서를 SHA-256으로 비교한다. 기대값 `E55DF236…`은 변경 전 엔진(`b524375`)에서 같은 검사를 실행해 얻었다(두 번 실행해 같은 값).
- [CollisionChecks.Tiles](../../Engine.Checks/CollisionChecks.cs): 네 배치(직교·아이소·육각 두 종)에서 셀을 지우면 겹침이 사라지고, 다시 쓰면 돌아오고, 타일맵을 1,000px 옮기면 새 위치에서만 맞는지 확인한다. 기존 [TileTerrainChecks](../../Engine.Checks/TileTerrainChecks.cs)의 SetTile 벽→진흙 검사도 그대로 통과한다.

## 확인하지 않은 것

- Windows·Linux x64(조정 담당자 몫, Windows는 보류). 위 해시가 x64에서도 같은지는 게스트에서 다시 봐야 한다.
- QEMU 게스트가 꺼진 상태의 시간 측정.
- F의 시간 이득(잡음 이하). 강체가 많은 씬이나 스트리밍 타일맵에서의 SyncPhysics 비용은 따로 재지 않았다.
