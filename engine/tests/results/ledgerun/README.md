# LedgeRun 제작 기록

2026-11-25 · macOS 27.0.1 (26A434) · Apple Silicon arm64. [샘플 계획](../../../../docs/plans/samples/1-platformer.md) · [조작 및 에디터 매뉴얼](../../../../docs/manual/ledgerun.md)

## 구현

- 384 × 216 논리 해상도와 16 × 16 직교 타일을 사용한다. Trailhead에서 시작해 Meadow, Cave, Tower, Summit으로 넘어간다. 초원은 발판과 코인, 동굴은 횃불과 세 종류의 적, 탑은 세로 스크롤과 청크 스트리밍으로 구분한다. 정상의 수호자는 순찰·도약·투사체 패턴을 돈다.
- 이동과 충돌은 기존 Character Motor와 타일 충돌을 사용한다. 벽 점프, 대시, 체크포인트, 적 상태, 슬롯 진행은 프로젝트의 C# 스크립트에서 처리한다.
- 맵은 반복 구간을 규칙으로 채운 뒤 씬 YAML로 구웠다. 에디터에서 타일맵이나 개별 오브젝트를 열어 수정할 수 있다. 배포에 맵 생성 코드가 들어가지 않는다.
- 시작 씬은 Trailhead로 고정한다. 배포본마다 같은 녹화 시작 씬을 쓰고 게임의 진행·해금 경로로 각 스테이지에 진입한다.

## 그림

Codex 내장 이미지 생성기로 두 원본을 생성했다. 투명 시트의 8개 스프라이트는 셀별로 잘라 알파를 유지하고 픽셀 크기로 내렸다. 지형 4칸은 64 × 16 시트로 정리했다. 원경은 384 × 216 PNG로 줄였다. 배포에는 잘라낸 PNG만 참조한다. [전체 프롬프트](../../../../docs/plans/samples/asset-prompts.md)

- 스프라이트 시트 프롬프트. “Generate one orthographic pixel art spritesheet set on genuine transparent alpha, landscape 4:3 with exactly four equal columns by three equal rows.” 세 가지 자세의 적갈색 머리 배달원, 금색 토큰, 청록색 깃발, 보석, 보라색 딱정벌레, 불씨, 초원·흙·동굴·탑 타일을 주문했다. 각 소재는 고유 실루엣과 단순한 16비트 색 군집을 사용한다.
- 원경 프롬프트. “Draw a serene golden-hour highland meadow vista, as a distant backdrop viewed horizontally from the side, extremely wide 16:9 composition.” 연한 푸른 하늘, 라벤더 빛 능선, 아래쪽 이끼색 언덕과 청록색 침엽수에 대비를 낮춰 플레이 요소가 앞에서 잘 보이도록 주문했다.

## 검증

### macOS arm64 Scene Play와 배포 플레이어

Scene Play, Release, Native AOT(Distribution)에서 [전체 경로 하네스](../../LedgeRun.Play/README.md)를 실행했다. 4,050프레임, 입력 302개로 두 번 낙하한 뒤 초원에서 다시 시작해 세 스테이지와 정상 전투를 끝냈다. [클리어 기록](macos-arm64-route.txt)과 SDL 소프트웨어 렌더러의 [캡처 10장 해시](macos-arm64-sha256.tsv)가 세 모드에서 같다. [초원](meadow.png) · [동굴](cave.png) · [정상](summit.png)

| 모드 | 앱 바이트 | 플레이어 실행 파일 바이트 | 첫 프레임 ms | 세션 시작 ms |
| --- | ---: | ---: | ---: | ---: |
| Release | 30,130,077 | 124,712 | 127 | 40.1 |
| Native AOT | 12,520,640 | 7,276,432 | 40 | 2.4 |

시작 시간은 첫 실행을 버리고 나머지 3회에서 한 프레임을 SDL로 그려 PNG를 저장하고 종료할 때까지 잰 중앙값이다. QEMU 게스트 부팅·빌드와 같은 Mac에서 측정했다. 샘플 콘텐츠는 29개 파일, 306.6 KiB이며 짧은 합성 효과음 3개가 30.6 KiB를 차지한다. 두 출력의 SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring·Roslyn 파일은 0개였다. [배포 크기 기준](../build-size/baseline.tsv)

헤드리스 전체 입력을 `MONOON_REPLAY_CAPTURE=<csv>`로 재생했다. 아래 표는 씬 전환을 뺀 구간의 업데이트만 측정한 값이다. 화면 그리기와 GPU 비용은 포함되지 않는다.

| 구간 | Release 업데이트 p50 / p95 µs | AOT 업데이트 p50 / p95 µs | Release 관리 할당 p50 / p95 B | AOT 관리 할당 p50 / p95 B |
| --- | ---: | ---: | ---: | ---: |
| 초원 | 27.4 / 38.3 | 10.3 / 14.1 | 96 / 912 | 96 / 1,000 |
| 동굴 | 36.3 / 50.8 | 13.2 / 18.9 | 384 / 2,936 | 448 / 3,032 |
| 탑 | 24.0 / 39.2 | 7.8 / 16.1 | 176 / 4,176 | 176 / 4,208 |
| 정상 | 11.9 / 24.0 | 5.2 / 11.6 | 240 / 3,952 | 240 / 3,984 |

1차 구현에는 조작 프롬프트, 기본 자세 교체, 착지 먼지, 수집·피격·클리어 효과음, 세 슬롯 진행 저장을 넣었다. 장르 확장 항목 중 경사·움직이는 발판, 음악·볼륨과 조작키 편집, 배경 레이어 추가, 0 B 할당 목표가 남아 있다.

### Linux x64

Debian 12 x86_64 QEMU 게스트에서 Release와 Native AOT를 직접 만들어 같은 경로를 헤드리스로 재생했다. [클리어 기록](linux-x64-route.txt)은 두 Linux 빌드와 macOS 세 경로에서 같다. 첫 Release 빌드는 루트 디스크 여유 공간이 0 B인 상태에서 `Bus error`로 종료됐다. 임시 디렉터리와 .NET·XDG 설정 캐시를 RAM으로 옮긴 다음 두 빌드가 통과했다. Windows는 장비 준비 후 확인한다.

[프로젝트 검사](../../../../samples/LedgeRun/Tests/TrailRunnerTests.cs)는 점프 입력을 유지하면 더 높이 올라가고 같은 점프 중 대시가 반복되지 않는 것을 확인한다.

## 2026-11-26 독립 검수

macOS arm64의 기존 Release·Native AOT에서 완주 기록과 기준 캡처 10장이 일치했다. 합성 패드 입력으로 Trailhead에서 슬롯 3을 선택해 전 구간을 클리어했으며, 슬롯 3만 기록되고 슬롯 1의 기존 완주 기록과 내용이 같았다.

- **P2. 발판 끝에서 공중 대시를 한 번 더 쓸 수 있다.** 초원 첫 발판의 X≈201 지점에서 지상 대시를 누르고, 8틱 뒤 발판을 벗어난 직후 착지 전 다시 대시한다. 헤드리스 테스트에서 두 번째 입력으로 X 속도가 260px/s까지 올라갔다. 지상에 닿아 있는 첫 대시의 진행 틱에도 공중 대시 사용 상태가 풀린다. 기존 테스트는 공중에서 시작한 대시만 다룬다. [대시 상태 갱신](../../../../samples/LedgeRun/Scripts/TrailRunner.cs#L51)
- **P3. 정상에서 일시정지를 풀면 보스 목표 안내가 사라진다.** 정상 진입 시 보이는 세 번 밟기 안내가 일시정지 문구로 덮인 뒤 빈 문자열로 바뀌었다. 합성 패드의 Back 버튼으로 멈춰도 키보드 P 안내가 표시됐다. [일시정지 처리](../../../../samples/LedgeRun/Scripts/TrailRunner.cs#L111)
- **P3. 정상 HUD와 기록 화면에 수집할 수 없는 보석이 표시된다.** 정상에는 보석이 배치되어 있지 않아도 HUD와 기록 화면이 모두 보석 목표를 3개로 표시한다. [HUD 계산](../../../../samples/LedgeRun/Scripts/TrailRunner.cs#L177) · [기록 표시](../../../../samples/LedgeRun/Scripts/Trailhead.cs#L55)
- **P3. 복구할 백업이 없는 슬롯에 접근하면 메뉴가 종료된다.** Native AOT의 임시 프로필에서 유효한 슬롯 2와 손상된 슬롯 1이 함께 있으면 시작 시 `Trailhead.OnCreate: Invalid slot data`로 종료 코드 1이 나왔다. 슬롯 1로 시작하고 손상된 슬롯 2로 바꿔도 종료됐다. 유효한 백업을 둔 대조군은 정상 복구됐다. [프로필 읽기](../../../../samples/LedgeRun/Scripts/TrailProfile.cs#L22) · [메뉴 진입과 슬롯 전환](../../../../samples/LedgeRun/Scripts/Trailhead.cs#L12)

고정 리플레이의 합격 조건은 스테이지 통과 순서와 화면이다. 틱별 위치·체력, 슬롯별 저장 내용, 보석 수집은 후속 회귀 항목으로 남겼다. 패드 검수에는 합성 입력을 사용했다.

## 2026-11-26 검수 수정과 재검증

기준 커밋 `5b9db95`의 샘플 워크트리에서 macOS 27.0.1 arm64 프로젝트 검사, Scene Play, Release·Native AOT Distribution으로 확인했다.

| 우선순위 | 재현 입력과 확인값 |
| --- | --- |
| P2 발판 밖 이중 대시 | 초원 첫 발판에서 오른쪽으로 X≥200까지 달려 `X → 8틱 → X`를 입력했다. 첫 대시로 공중에 나간 뒤 두 번째 입력에서는 수평 속도가 120px/s 미만으로 돌아왔다. 공중에서 시작한 연속 대시 차단 검사도 통과했다. |
| P3 일시정지 후 목표 | 정상에 들어가 패드 Back으로 멈추고 P로 재개했다. 일시정지 문구에 패드 Back이 보이고 재개 후 세 번 밟기 목표가 복원됐다. 두 Mac 배포본에서 [정상 재개 화면](review-summit.png)을 확인했다. |
| P3 정상의 보석 표시 | 정상 HUD에는 `HEARTS 3`이 표시됐다. 정상 클리어 기록이 있는 슬롯의 Trailhead에는 `Best   2.0 s`만 표시됐다. |
| P3 복구할 사본이 없는 슬롯 | 슬롯 1의 저장 파일을 손상시키고 정상 클리어 기록이 든 슬롯 2를 준비했다. 메뉴에서 슬롯 1 진입을 막고 `Tab → Enter`로 슬롯 2의 정상을 열었다. 두 Mac 배포본에서 슬롯 1과 슬롯 2 각각을 손상시켜 메뉴가 살아 있고 손상 파일이 보존되는 것을 확인했다. 유효한 백업이 있으면 원본과 같은 바이트로 복구됐다. |

프로젝트 검사 5개가 통과했다. 세 Mac 실행 경로의 [완주 로그](macos-arm64-route.txt)는 이전 기록과 같았으며 수정 후 [캡처 10장](macos-arm64-review-sha256.tsv)도 서로 일치했다. 이전 패키지 대비 Release 앱은 2,015 B 늘었고 Native AOT 앱은 17 B 줄었다. 새 런타임 의존성은 없다.

Debian 12 x64 QEMU 헤드리스 Release(교차 빌드)에서도 네 구간 완주 로그가 Mac과 같았다.

QEMU가 동작하는 동안 이전·수정 패키지를 교대로 예열하고 각각 5번 더 실행했다. 헤드리스 `session_ready`와 업데이트 중앙값·p95 차이는 두 측정 범위의 합 이내였다. 두 배포본의 관리 할당 중앙값은 틱당 240 B였다. 할당 p95는 Release 3,608 → 3,688 B, AOT 3,624 → 3,720 B였다. `MONOON_STARTUP_TRACE`와 `MONOON_REPLAY_CAPTURE`로 측정했다.

```sh
./monoon test project samples/LedgeRun
python3 engine/scripts/run-harness.py LedgeRun.Play
```
