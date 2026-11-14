# BarrageLab 탄막 배칭·풀링 실측

2026-11-14, macOS 27.0.1 (26A434), Apple Silicon arm64. 에디터의 Scene Play에서 시작하고 Release와 Native AOT는 각각 독립 앱으로 실행했다. 세 창에 실제 Space·B·R 입력을 보냈다. 창마다 초당 1,440발로 발사하고 화면을 벗어난 탄환을 회수했다.

## 프레임당 측정 결과

| 실행 | 제출 모드 | 측정 프레임 | SDL 제출 수 범위 | 제출 수 중앙값 | 관리 할당 중앙값 | 관리 할당 평균 |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Scene Play | 묶음 | 238 | 10 | 10 | 792 B | 802.7 B |
| Scene Play | 개별 | 240 | 2,691–2,768 | 2,719 | 792 B | 801.6 B |
| Release | 묶음 | 480 | 10 | 10 | 704 B | 709.3 B |
| Release | 개별 | 480 | 2,691–2,768 | 2,719 | 704 B | 708.8 B |
| Native AOT | 묶음 | 479 | 10 | 10 | 752 B | 756.8 B |
| Native AOT | 개별 | 480 | 2,691–2,768 | 2,719 | 752 B | 756.8 B |

이 구간에는 탄환 2,683–2,760개가 동시에 활성 상태였다. HUD, 배경, 중앙 발사체 표시까지 포함해 측정했다. 같은 텍스처의 연속된 사각형을 최대 1,024개씩 한 번에 제출한다. 화면 가운데 코어도 탄환과 같은 PNG라 마지막 묶음에 들어갈 수 있다.

4,096개 슬롯의 같은 풀로 두 모드를 비교했다. 각 실행에서 먼저 505틱 이상 발사해 스크립트·텍스처·HUD 숫자 캐시를 예열했다. R로 처음부터 발사한 뒤 B와 R로 개별 제출에서도 동일한 경로를 재생했다. 두 기록에서 발사 시작 후 240–479틱에 해당하는 실제 렌더 프레임을 집계했다. 세 환경의 양쪽 모드에 모두 찍힌 238개의 같은 시뮬레이션 틱에서 활성·회수·재사용 수가 전부 일치했다.

관리 할당은 메인 스레드의 게임 업데이트와 렌더 구간에서 `GC.GetAllocatedBytesForCurrentThread()`의 차이를 더했다. 프레임 수는 실제 렌더 주기에 따라 달라진다. 각 프레임에서 실행된 고정 업데이트 횟수와 HUD 갱신이 평균 할당에 영향을 준다. CSV 기록과 에디터 Inspector 갱신은 측정 구간 밖에서 수행했다. 탄환의 발사·이동·회수 및 렌더 목록 준비만 떼어낸 회귀 검사에서는 예열 후 10,000회 합계가 **0 B**였다. 같은 위치·속도·색상 데이터를 담은 탄환 객체를 매번 새로 생성한 대조군은 같은 10,000회에 640,000 B를 할당했다. 두 경우 모두 렌더 목록 버퍼를 재사용했다. GPU 메모리·백그라운드 스레드·GC 일시정지 시간은 측정 대상에서 제외했다.

[Scene Play 원본 CSV](scene-play.csv) · [Release 원본 CSV](release.csv) · [Native AOT 원본 CSV](native-aot.csv)

## 화면 밖 회수 및 재사용

| 실행 | 발사 중지 후 누적 발사 = 누적 회수 | 활성 탄환 | 재개 뒤 확인한 재사용 발사 | 화면 |
| --- | ---: | ---: | ---: | --- |
| Scene Play | 13,248 | 0 | 648 / 648 | [묶음](scene-play-batched.png) · [개별](scene-play-individual.png) · [회수 완료](scene-play-drained.png) · [재개](scene-play-resumed-compact.png) |
| Release | 12,936 | 0 | 312 / 312 | [묶음](release-batched.png) · [개별](release-individual.png) · [회수 완료](release-drained.png) · [재개](release-resumed.png) |
| Native AOT | 12,864 | 0 | 336 / 336 | [묶음](native-aot-batched.png) · [개별](native-aot-individual.png) · [회수 완료](native-aot-drained.png) · [재개](native-aot-resumed.png) |

세 실행에서 모두 풀 포화로 거절된 발사는 0건이었다. 에디터는 재개 후 880×660으로 줄여 조작 안내와 카운터가 같은 기준선에 놓이는지도 확인했다. 전체 회귀 검사 332개가 통과했다. [검사 로그](checks.txt) · [풀 경계·렌더·할당 검사](../../Engine.Checks/SpritePoolChecks.cs)

별도의 Metal 픽셀 검사에서 아틀라스의 반투명 스프라이트 2,500개를 같은 순서로 그렸다. 묶음 제출 3회와 개별 제출 2,500회의 RGBA 결과가 픽셀 단위로 일치했다. [Metal 검사 로그](metal-pixels.txt)

[샘플 실행 및 CSV 재현 방법](../../../../docs/manual/sprite-pools.md)
