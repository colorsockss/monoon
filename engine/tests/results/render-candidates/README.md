# CollisionLab 카메라별 렌더링 검증

`Field10000.scene`에는 스프라이트가 있는 충돌체 10,000개와 카메라·HUD·정렬 확인용 마커를 포함한 보조 오브젝트 10개가 있다. 같은 씬을 로드한 Release 검사 실행 파일에서 엔진의 전체 순회와 카메라별 공간 인덱스를 번갈아 사용했다.

## 세 플랫폼 결과

2026-11-17 세 플랫폼 모두 카메라 2대 × 자세 5가지에서 전체 순회 대비 다른 픽셀 **0개**, 스프라이트 ID 순서 불일치 **0건**이었다. HUD를 포함한 합성 화면도 각 OS 안에서 두 경로가 일치했다. UI를 제외한 카메라별 RGBA와 그리기 순서 해시는 세 OS 사이에서도 같았다.

| 환경 | 카메라 | 후보 수, 전체 → 격자 | CPU 렌더링 p95 (ms), 전체 → 격자 | 시간 감소 |
| --- | --- | ---: | ---: | ---: |
| macOS arm64 | 기본 | 10,010 → 270 | 0.3322 → 0.2529 | 23.9% |
| macOS arm64 | 미니맵 | 10,010 → 520 | 0.2945 → 0.2088 | 29.1% |
| Windows x64 VM | 기본 | 10,010 → 270 | 14.2270 → 8.6730 | 39.0% |
| Windows x64 VM | 미니맵 | 10,010 → 520 | 11.7700 → 6.0100 | 48.9% |
| Linux x64 VM | 기본 | 10,010 → 270 | 11.5230 → 5.8505 | 49.2% |
| Linux x64 VM | 미니맵 | 10,010 → 520 | 10.4095 → 4.6437 | 55.4% |

- macOS arm64 — [판정과 OS 정보](osx-arm64/result.json) · [시간 표본](osx-arm64/timings.csv) · [전체 순회 화면](osx-arm64/composite-scan.png) · [최적화 화면](osx-arm64/composite-grid.png)
- Windows x64 VM — [판정과 OS 정보](win-x64/result.json) · [시간 표본](win-x64/timings.csv) · [전체 순회 화면](win-x64/composite-scan.png) · [최적화 화면](win-x64/composite-grid.png)
- Linux x64 VM — [판정과 OS 정보](linux-x64/result.json) · [시간 표본](linux-x64/timings.csv) · [전체 순회 화면](linux-x64/composite-scan.png) · [최적화 화면](linux-x64/composite-grid.png)

## 화면과 순서

기본 카메라(800×450)와 우상단 미니맵을 각각 단독으로 그려 뷰포트 전체의 RGBA 바이트와 화면에 들어온 스프라이트 ID의 **전체 순서**를 비교한다. 주황 마커가 파란 마커 뒤, 발 높이가 같은 위치, 앞, 미니맵에만 보이는 위치, 두 카메라 밖에 있는 위치의 다섯 가지를 검사한다. 겹친 위치에서는 화면 아래쪽에 발이 있는 마커의 색이 나왔는지 확인하고, 동률에서는 씬 순서상 나중에 있는 파란색을 기대한다. 마지막으로 두 카메라와 HUD를 함께 그린 전체 프레임도 픽셀 단위로 비교한다.

[기본 카메라에서 뒤](osx-arm64/primary-behind-grid.png) · [앞](osx-arm64/primary-front-grid.png) · [미니맵에서 뒤](osx-arm64/minimap-behind-grid.png) · [앞](osx-arm64/minimap-front-grid.png)

Mac의 Release 배포 앱에서도 숫자 **2**로 필드에 들어가 아래쪽 방향키로 주황 마커를 이동했다. 실제 창의 겹친 영역에서 파란색이 주황색으로 바뀐 픽셀은 기본 화면 5,976개, 미니맵 249개였다. [이동 전](osx-arm64/play-behind.png) · [이동 후](osx-arm64/play-front.png)

## 측정 방법

- 후보 수는 해당 카메라가 활성 상태, UI 여부, 정밀 시야 판정을 검사하기 위해 순회한 그리기 항목의 수다. 카메라·HUD도 기존 전체 순회에 포함된다. 기준 위치에서 실제로 그린 스프라이트는 기본 카메라 227개, 미니맵 450개다.
- 모든 OS에서 SDL dummy 비디오 드라이버와 BRUTAL SDL3 소프트웨어 렌더러를 사용했다. 스프라이트 배칭과 800×450 논리 해상도는 배포 플레이어와 같은 설정이다. 단조 시계로 카메라의 후보 조회부터 Y 정렬 순서 복원, 스프라이트 제출과 SDL 명령 플러시까지 렌더 스레드의 경과 시간을 쟀다. 화면 전송, 캡처, 프레임 제한, 물리 시뮬레이션은 측정 구간 밖에 있다.
- 정적 기준 자세에서 카메라·방식마다 24회 예열 후 160회 측정했다. 매 쌍마다 먼저 실행하는 방식을 바꿨다. p95는 오름차순 152번째 표본이다. 두 카메라가 프레임당 한 번 공유하는 Y 정렬 준비와 이동 오브젝트의 격자 갱신 시간은 CSV의 `shared_prepare_ms`에 따로 기록했다.
- Windows와 Linux는 Apple Silicon Mac에 보존한 QEMU x64 게스트에서 측정했다. 같은 OS 안에서 두 방식의 차이를 비교한다.

## 재현

저장소 루트에서 다음과 같이 실행하면 캡처, 판정 해시, 시간 원본이 지정한 폴더에 생성된다. Windows에서는 같은 환경 변수를 PowerShell에서 지정하고 해당 OS용 실행 파일을 사용한다.

```sh
dotnet build engine/tests/Engine.Checks -c Release
SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy DOTNET_TieredCompilation=0 \
  dotnet engine/.artifacts/bin/Engine.Checks/release/Engine.Checks.dll \
  --render-candidates samples/CollisionLab /tmp/monoon-render-results
```

[검사 코드](../../Engine.Checks/RenderCandidateChecks.cs) · [샘플 조작법](../../../../docs/manual/cameras.md#대형-씬에서-두-카메라-확인하기)
