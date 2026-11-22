# CollisionLab 느린 프레임 작업 분석

2026-11-22 · macOS 27.0.1 (26A434) · Apple M4 Max · .NET SDK 10.0.101. 같은 소스로 플레이어 템플릿, 패키징한 에디터, CollisionLab의 개발용·배포용 빌드를 만들었다. Windows·Linux에서는 실행하지 않았다. [실행 방법](../../CollisionLab.FrameProfiler/README.md)

QueryField는 충돌체 1만 개 씬에 들어올 때와 **R**을 누를 때 한 고정 틱 안에서 겹침·레이캐스트·스윕을 각각 1,344회 실행한다. 세 실행 모두 이 프레임의 작업 표에 정확히 1,344회씩 기록됐고, 다른 프레임에는 시작 씬 Probe의 질의 5회만 있었다.

## 질의 프레임

질의 시간은 Physics.Overlap·Raycast·Sweep의 Total 합이고, 비율은 프레임 시간 대비 값이다.

| 실행 | 프레임 | 프레임 시간 | QueryField.FixedUpdate | 질의 시간 | 비율 | 관리 할당 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 개발용 Release, 헤드리스 재생 | 4 (씬 진입 측정) | 147.911 ms | 141.900 ms | 130.736 ms | 88.4% | 4,090,184 B |
| 개발용 Release, 헤드리스 재생 | 91 (R, R 이후 최장) | 131.407 ms | 128.093 ms | 127.466 ms | 97.0% | 3,819,096 B |
| 개발용 Native AOT, 헤드리스 재생 | 4 (씬 진입 측정) | 142.988 ms | 139.725 ms | 138.948 ms | 97.2% | 4,175,784 B |
| 개발용 Native AOT, 헤드리스 재생 | 91 (R, R 이후 최장) | 147.265 ms | 144.010 ms | 143.413 ms | 97.4% | 3,905,184 B |
| 에디터 Scene Play | 33 (씬 진입 측정) | 364.359 ms | 256.739 ms | 216.184 ms | 59.3% | 6,380,208 B |
| 에디터 Scene Play | 81 (R, R 이후 최장) | 137.839 ms | 114.296 ms | 112.299 ms | 81.5% | 4,008,240 B |

- 헤드리스 재생은 150프레임이고 프레임마다 고정 틱이 한 번 실행됐다. 프레임 시간 중앙값은 Release 3.182 ms, Native AOT 3.020 ms였다. 2번째 프레임에 **2**, 90번째 프레임에 **R**을 눌렀다.
- 에디터는 SDL Scene 패널에서 실제 프레임을 121개 기록했고 중앙값은 20.985 ms였다. R 프레임에서는 Simulate가 2회(밀린 고정 틱 포함) 실행됐다. 질의 외에 Physics.Contacts 5.421 ms, Frame Self 17.025 ms가 기록됐다. 그래프 클릭과 같은 경로로 이 프레임을 선택하자 [작업 표](scene-play/profiler-1400.png)에 세 질의가 1,344회씩 표시됐다. 창 너비 1400px과 [820px](scene-play/profiler-820.png)에서 머리글과 행의 열 위치 차이는 0px이었다.

## 씬 전환 프레임

질의 직전 프레임은 1만 개 씬을 만드는 프레임이다.

| 실행 | 프레임 시간 | Scene.Loading | 관리 할당 | Gen0 |
| --- | ---: | ---: | ---: | ---: |
| 개발용 Release | 147.107 ms | 132.827 ms | 81,348,408 B | 9 |
| 개발용 Native AOT | 108.094 ms | 104.785 ms | 82,388,200 B | 12 |
| 에디터 Scene Play | 5,488.723 ms | 370.861 ms | 6,390,117,720 B | – |

에디터의 이 프레임에서는 Editor 샘플(Hierarchy·Inspector·진단 갱신)이 595.241 ms, 어느 샘플에도 속하지 않은 Frame Self가 4,517.881 ms였다. UI 스레드 할당 약 6.4 GB도 이 프레임에 기록됐다. 이 시간과 할당이 에디터 UI의 어느 부분에서 발생하는지는 이번에 분석하지 않았다.

## 배포용 빌드

- 배포용 Release와 Native AOT 플레이어에 `MONOON_FRAME_PROFILE`을 지정하고 같은 입력을 재생했다. 두 플레이어 모두 경고 한 줄만 출력하고 CSV를 만들지 않았다. 질의 결과의 대상 ID 해시와 후보 수는 개발용 플레이어와 같았다.
- 배포용 Release 앱과 개발용 앱의 파일 목록 차이는 개발용에만 남는 PDB 4개와 `libSDL3.dylib` 사본 1개였다. 배포용 앱에는 SkiaSharp, Avalonia, 에디터·빌드 도구 어셈블리가 없었다.
- [CollisionLab.Spatial 검증기](../../CollisionLab.Spatial/verify.py)를 배포용(프로파일러 없음)과 개발용(`MONOON_FRAME_PROFILE` 지정)으로 번갈아 세 번씩 실행했다. 모두 통과했으며 대상 ID 해시는 기존 기록과 같았다. 질의 1회의 p95(µs, 1,728개 표본)는 다음과 같다. 개발용 실행의 CSV에도 측정 프레임마다 종류별 1,344회가 기록됐다.

| 충돌체 | 방식 | 배포용 1 / 2 / 3 | 개발용 + 프로파일러 1 / 2 / 3 |
| ---: | --- | --- | --- |
| 1,000 | 전체 순회 | 8.125 / 8.375 / 8.208 | 8.625 / 8.208 / 8.500 |
| 1,000 | 격자 | 1.583 / 1.542 / 1.583 | 1.708 / 1.708 / 1.625 |
| 10,000 | 전체 순회 | 72.375 / 74.375 / 73.917 | 74.542 / 74.584 / 75.417 |
| 10,000 | 격자 | 1.750 / 1.750 / 1.667 | 1.875 / 1.792 / 1.750 |

격자 질의의 p95는 프로파일러를 켜면 평균 약 0.1 µs 증가했다. 이 Mac의 타이머 눈금(약 0.042 µs) 두세 개에 해당한다.
- `./monoon ci`로 InventoryLab을 Release·Native AOT 배포용으로 다시 빌드했다. 입력 재생 저장 파일의 SHA-256은 기존 기록과 같은 `EED41DEF…E70F795`였다.

## 회귀 검사

`./monoon test` 전체가 통과했다(`All checks passed.`). [`FrameProfilerChecks`](../../Engine.Checks/FrameProfilerChecks.cs)의 [19개 항목](checks.txt)은 다음을 확인한다.

- 반복 호출을 부모별 한 노드로 합쳐 호출 수를 센다. Self는 바로 아래 샘플의 Total을 뺀 값이고, 같은 작업의 재귀 호출은 바깥 호출만 시간에 합산한다.
- 스크립트 한 틱의 레이캐스트 37회와 겹침 11회가 그 스크립트의 `FixedUpdate` 아래에 기록된다. 고정 틱 3회를 실행한 프레임에는 3배가 기록된다.
- 링 버퍼는 최신 프레임을 순서대로 보관하고, 번호로 고른 프레임과 가장 느린 프레임이 일치한다. 깊이 제한을 넘은 호출, 열린 샘플 안의 프레임 종료, 다른 스레드의 호출을 처리한다.
- 정상 상태에서 샘플 20,000개와 프레임 20개, CSV 50프레임 기록의 관리 할당이 0바이트다.
- 빌드 영수증이 없거나 배포용이면 프로파일러를 설치하지 않고 파일을 만들지 않는다. 개발용 CSV의 행 순서, 부모 번호, 기록 시간 샘플을 확인한다.

## 원본

[Release 요약](release/summary.json) · [Release CSV](release/development.csv) · [`monoon profile` 출력](release/profile.txt) · [입력 기록](release/Field10000.inputreplay) · [Release 로그](release/development.log) / [배포용](release/distribution.log) · [Native AOT 요약](native-aot/summary.json) · [에디터 결과](scene-play/result.json) · [Spatial 검증 로그](overhead/) · [사용법](../../../../docs/manual/play-diagnostics.md#느린-프레임의-작업-분석)
