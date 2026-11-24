# 탑다운 자유 이동 길찾기 (16.15)

2026-11-24, Apple M4 Max, macOS 27.0.1 arm64, .NET 10. 실행 대상: Scene 편집(Yard 선택 미리보기), Scene Play(패키지 에디터 + 프로브), Release, Native AOT(모두 Distribution). 기준은 wip/next1 `2ed10fd`에서 시작해 `1c34b81`, `556b1ae`를 병합한 `dc6d570`이고, 병합 뒤 `./monoon test`, 세 경로 재생, Scene 캡처, 비용 측정을 다시 실행했다. 측정 시작과 끝에 `qemu-system`은 없었다.

SeedLab에 **Crowd yard** 씬을 더했다. 배달원 200명이 벽 2개와 기둥 2개를 돌아 각자의 목표 칸에 도착한다. 걸을 수 있는 영역은 기존 **Navigation Region 2D**(Spacing = 칸 크기, Agent W / 2 = 반지름, Mask)에서 로드할 때 한 번 굽는다. 경로는 8방향 A*(정수 비용 10/14, 모서리 자르기 없음, 칸 번호로 동점 처리)를 시야 검사로 줄인 모서리 점이다(string pulling). 문은 동적 장애물(`AddObstacle`/`RemoveObstacle`)이다. 국소 회피는 조사(E-15)의 "간단한 분리 회피"대로 이웃과 겹친 거리의 절반씩 밀어내고, 앞을 막은 이웃은 옆으로 비켜 간다. 씬 스키마는 바꾸지 않았다(새 컴포넌트·필드 없음, Crowd.scene은 버전 18).

[샘플 스크립트](../../../../samples/SeedLab/Scripts/CrowdRun.cs) · [씬](../../../../samples/SeedLab/Scenes/Crowd.scene) · [사용 설명](../../../../docs/manual/navigation.md#탑다운-자유-이동-길찾기) · [하네스](../../SeedLab.NavigationFree/README.md) · [벤치](../../../benchmarks/Navigation/Program.cs)

| 실행 경로 | 캡처 |
| --- | --- |
| Release 헤드리스, 260·1000프레임(문을 연 뒤 이동 중, 200명 도착) | [260](release-0260.png) · [1000](release-1000.png) |
| Native AOT 헤드리스, 같은 프레임(Release와 PNG 바이트 동일) | [260](native-aot-0260.png) · [1000](native-aot-1000.png) |
| 에디터 Scene 편집, Yard 선택: 열린 칸 청록 음영, Inspector `Top-down 3,775 / 4,042 cells open` | [editor-gizmo.png](editor-gizmo.png) |

## 배포본 영향

| 항목 | 값 |
| --- | --- |
| 용량 게이트(KeyRoom·PlatformerLab·InventoryLab, osx-arm64 Distribution) | Release 앱 각 **+512 B**(Engine.Core.dll), Native AOT 앱·실행 파일 **변화 0 B**(쓰지 않는 게임에서는 트리밍됨). 기준 +1% 이내, 기준 파일 갱신 없음 |
| SeedLab 배포본(Crowd yard 포함) | Release 29.9 MB, Native AOT 12.9 MB. 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) 0개 |
| 굽기 | 로드할 때 한 번(`OnCreate`). Yard 94×43칸, 벽 5개: 2.0–2.8 ms(첫 호출, JIT 포함) |
| 메시 데이터 | 구운 칸 상태 1 B/칸 = Yard 4.0 KB(동적 장애물 카운트 2 B/칸 별도). 배포본에 따로 저장하지 않는다(로드할 때 콜라이더에서 굽는다). 탐색 버퍼는 칸당 32 B(Yard 129 KB), 200명의 경로 버퍼 51 KB. 모두 생성할 때 한 번 할당 |
| 경로 요청 1회(Yard 가로지르기, 200쌍 반복, 5회 교대 측정) | **NavArea2D 91.2 µs**(90.6–92.9), 0 B / 기존 GridNavigation 같은 칸 8방향 168.8 µs(167.4–170.1), 65,301 B. 범위가 겹치지 않으므로 약 1.85배 빠르다. 결과 점 수: 5개 / 103칸 |
| 틱당 비용(200명 `Step`, 출발부터 모두 도착까지 892틱) | **중앙값 113.4 µs**(112.4–162.4), 최악 틱 1.59 ms(처음 13틱은 틱당 16명씩 첫 경로 요청). 틱당 경로 요청 평균 3.10회(최대 16) |
| 틱당 할당 | 0 B(벤치 5회 모두, `nav-free` 검사의 1,000회 요청 + 장애물 추가·제거 + 1,200틱) |

BRUTAL에는 내비게이션·길찾기 기능이 없다([기능 정리](../../../../docs/brutal.md)). `Brutal.Collections`의 비관리 컬렉션은 쓰지 않았다. 버퍼 크기가 생성 시점에 정해져 관리 배열 몇 개로 할당 0을 지킬 수 있고, Engine.Core에 패키지 참조를 늘리지 않기 위해서다. 새 패키지는 내려받지 않았다.

`MaxPlansPerStep`(기본 16)은 측정 뒤에 넣었다. 문을 열 때 200명이 한 틱에 다시 탐색하면 그 틱이 약 18 ms(200 × 90 µs)가 되기 때문이다. 이제 바뀐 영역의 재탐색은 칸 번호 순서로 여러 틱에 나뉘고, 그동안 이전 경로를 따라간다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| [NavArea2D.cs](../../../src/Engine.Core/NavArea2D.cs) | 굽기(반지름 원과 충돌체·타일 겹침, 영역 가장자리), 동적 장애물 카운트, 무할당 A*(이진 힙, 세대 번호), 시야 검사(DDA, 모서리 통과는 양옆 칸 검사), string pulling, 막힌 출발·목적지는 가장 가까운 열린 칸, 에디터용 문서 굽기 |
| [NavCrowd2D.cs](../../../src/Engine.Core/NavCrowd2D.cs) | 유닛 버퍼, 버킷 격자 이웃 찾기, 분리·옆 비키기, 축별 미끄러짐, 시차 재탐색과 틱당 탐색 상한 |
| [Navigation2D.cs](../../../src/Engine.Core/Navigation2D.cs) | `INavigation2D.BakeArea(region)`: 현재 정적 콜라이더와 로드된 Solid 타일(동적 강체 제외)로 굽기 |
| [SceneViewport.Navigation.cs](../../../src/Engine.Editor/Scene/SceneViewport.Navigation.cs), [SceneViewport.Gizmos.cs](../../../src/Engine.Editor/Scene/SceneViewport.Gizmos.cs) | 선택한 영역의 열린 칸을 행 단위 사각형으로 칠한다. 영역 변경 시 즉시, 그 밖에는 0.5초마다 다시 굽는다 |
| [MainWindow.Presentation.cs](../../../src/Engine.Editor/Inspector/MainWindow.Presentation.cs) | Navigation Region 2D에 읽기 전용 `Top-down` 줄 하나 |
| [CrowdRun.cs](../../../../samples/SeedLab/Scripts/CrowdRun.cs), [Crowd.scene](../../../../samples/SeedLab/Scenes/Crowd.scene) | 200명 샘플(스프라이트 풀로 그림, 문은 동적 장애물). 테스트 사본만 `partial void Traced`를 구현한다 |
| [RouteSceneSwitch.cs](../../../../samples/SeedLab/Scripts/RouteSceneSwitch.cs), Route.scene, GameSettings, .monoon-assets | `Action` 필드(기본 `SwitchLab`), Courier routes에서 **C**로 Crowd yard, `CrowdLab` 입력, 포함 씬 |
| [NavigationFreeChecks.cs](../../Engine.Checks/NavigationFreeChecks.cs), Program.cs | `--only nav-free`(템플릿 불필요), 전체 실행에도 포함 |
| [SeedLab.NavigationFree](../../SeedLab.NavigationFree/README.md) | `verify.py`(prepare·play·compare), `editor.py`, `gizmo.py`, 프로브 |
| [benchmarks/Navigation](../../../benchmarks/Navigation/Program.cs) | 비용 측정 |
| docs/manual/navigation.md, README.md, agent-workflow.md | 사용 설명, 검사 이름 |

## 검증

| 검사 | Scene 편집 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- |
| 200명 모두 자기 목표 좌표에 정확히 도착(880틱), 벽 안 위치 없음, 문 열기 1회 기록 | — | 통과 | 통과 | 통과 |
| 세 경로 기록 동일: 위치 해시 `D38B7A6871B36FA2`, trace SHA-256 `9b006ee8…a913c92b`, 232줄 | — | 통과 | 통과 | 통과 |
| 헤드리스 캡처 260·1000프레임 PNG 바이트 동일 | — | — | 통과 | 통과 |
| 선택한 Yard의 열린 칸 음영과 Inspector `3,775 / 4,042 cells open` | 통과 | — | — | — |
| 금지 파일 없음 | — | — | 통과 | 통과 |
| `nav-free`: 벽 우회(점 4개), 반지름 팽창, 막힌 출발, 동적 장애물로 막고 열기(같은 경로 복원), 버퍼가 짧을 때 이어서 요청, 갇힌 목표 0, 할당 0, 16명 통과 | 문서 굽기 = 게임 굽기(4,042칸 일치) | | | |
| `nav-free` Yard(검사 자체 군중, 문 닫힘): 200/200이 892틱에 도착, 최근접 쌍 10.0 px, FNV `56FDB4FCD37C034E` | | | | |
| `./monoon test` 전체(결정성 API 검사 232파일, 용량 게이트 포함) | 통과 | | | |

기록: [checks.txt](checks.txt) · [bench.txt](bench.txt) · [Release 결과](release.json) · [Native AOT 결과](native-aot.json) · [Scene Play 결과](scene-play.json) · [Release trace](trace.txt) · [입력 기록](Crowd.inputreplay)

Scene Play와 Release의 도착 틱(880)이 `nav-free`(892)와 다른 것은 정상이다. 하네스는 130프레임에 문을 열고, 검사는 문을 닫은 채로 돌린다.

## 재현

```sh
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only nav-free
dotnet run --project engine/benchmarks/Navigation -c Release -- 5 2000
```

세 경로 재생과 Scene 캡처는 [하네스 README](../../SeedLab.NavigationFree/README.md)의 명령을 그대로 쓴다.

## 확인하지 않은 것

- Windows·Linux x64: Windows는 보류, Linux 게스트 재생은 coordinator 몫이다. 이동 계산은 스칼라 `float` 연산만 쓰고(`Vector2.Transform`·`Lerp` 없음, 결정성 API 검사 통과) 경로 탐색은 정수라 같은 결과를 기대하지만 x64에서 실행하지 않았다.
- 화면이 있는 창(Metal)에서 직접 키를 눌러 본 것은 없다. 캡처는 헤드리스 소프트웨어 렌더러와 에디터 Scene 창이다.
- 회전하거나 크기가 바뀐 영역은 월드 경계 상자로 굽는다(축 정렬 칸). 스트리밍 타일은 굽는 시점에 로드된 칸만 들어간다.
- 군중 회피는 분리와 옆 비키기뿐이다(RVO 같은 속도 장애물 없음). 좁은 통로에서 마주 오는 큰 무리는 밀고 지나가는 데 시간이 걸릴 수 있다.
