# BarrageLab 프리팹 풀 실측

2026-11-22, macOS 27.0.1 (26A434), Apple M4 Max, .NET SDK 10.0.101. 같은 소스에서 BarrageLab을 `--profile distribution`으로 Release와 Native AOT로 빌드했다. 두 배포본 모두 SkiaSharp 어셈블리나 네이티브 라이브러리를 포함하지 않았다. [재생 실행기](../../BarrageLab.PrefabPools/README.md)로 각 배포본을 헤드리스로 실행하고 같은 입력을 풀 방식과 생성·제거 방식으로 한 번씩 재생했다. 탄환은 Sprite Renderer, Circle Collider 2D 트리거, PooledBullet 스크립트를 가진 [프리팹](../../../../samples/BarrageLab/Assets/Bullet.prefab)이다. 포탑은 틱마다 4발을 쏘고, 위아래로 움직이는 트리거 표적 4개에 닿거나 화면을 벗어난 탄환을 반환하거나 제거한다.

## 명중 수

| 배포본 | 방식 | 틱 | 발사 | 명중 | 표적의 Exit | 끝난 시점의 활성 탄환 | 생성 | 재사용 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Release | `Rent`·`Return` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 587 | 6,721 |
| Release | `Instantiate`·`Destroy` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 7,308 | 0 |
| Native AOT | `Rent`·`Return` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 587 | 6,721 |
| Native AOT | `Instantiate`·`Destroy` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 7,308 | 0 |

네 실행에서 명중, Exit, 활성 수가 같았다. 표적이 받은 Enter와 Exit 수가 같으므로 반환된 탄환과의 접촉이 남지 않았다. 씬을 연 뒤 다시 시작하기 전 27틱 동안 두 기록 모두 풀 방식으로 108발을 쐈다. 이 수는 생성 수에 포함된다. 생성·제거 기록에서는 이 108발이 P를 누를 때 풀로 반환되어 비활성 상태로 남아 있었다.

## 프레임당 업데이트 시간과 관리 할당

다시 시작한 뒤 601–1,799번째 틱(1,199프레임)을 집계했다. 재생 프레임마다 고정 업데이트가 정확히 한 번 실행됐다. 시간은 입력 전달과 고정 업데이트 구간의 벽시계 시간이고 렌더링은 포함하지 않는다.

| 배포본 | 방식 | 평균 | p95 | 최대 | 프레임당 할당 | 1,199프레임 할당 합계 | Gen0 수집 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Release | `Rent`·`Return` | 294.3 µs | 404.5 µs | 2,262.3 µs | 57,628 B | 69,096,552 B | 8 |
| Release | `Instantiate`·`Destroy` | 1,241.0 µs | 2,574.0 µs | 12,030.2 µs | 841,179 B | 1,008,573,424 B | 127 |
| Native AOT | `Rent`·`Return` | 122.8 µs | 130.1 µs | 1,235.1 µs | 57,604 B | 69,067,136 B | 8 |
| Native AOT | `Instantiate`·`Destroy` | 874.4 µs | 1,543.8 µs | 4,174.5 µs | 842,934 B | 1,010,678,360 B | 127 |

풀 방식의 프레임당 관리 할당은 생성·제거 방식의 약 6.8%였다. 같은 배포본을 다시 실행했을 때 명중 수와 할당은 같았다. 시간은 Release 풀 310.8 / 362.9 / 2,478.0 µs, 생성·제거 1,207.4 / 2,342.9 / 10,951.4 µs, Native AOT 풀 119.3 / 131.8 / 1,087.9 µs, 생성·제거 849.0 / 1,470.6 / 3,921.0 µs(평균 / p95 / 최대)였다. 생성·제거 방식은 매 생성마다 프리팹 파일을 읽고, 생성·제거가 있는 틱마다 월드 색인을 다시 만든다. 풀 방식에 남은 할당이 어디서 생기는지는 이번에 분석하지 않았다. GPU 메모리, 렌더링 시간, GC 일시정지 시간은 측정하지 않았다.

[Release 원본](release/summary.json) · [Release 반복 실행](release/summary-repeat.json) · [Native AOT 원본](native-aot/summary.json) · [Native AOT 반복 실행](native-aot/summary-repeat.json). 각 폴더에 입력 기록, 플레이어 로그, 프레임별 CSV, 빌드 영수증이 있다.

## 회귀 검사

[`PrefabPoolChecks`](../../Engine.Checks/PrefabPoolChecks.cs)는 임시 프로젝트의 프리팹(태그·그룹, 트리거, 자식, 프리팹 내부 이벤트 연결)으로 다음을 확인한다.

- 빈 풀은 한 번 생성하고 `OnCreate → OnEnable → OnRent` 순서로 호출한다. 반환은 `OnReturn → OnDisable`, 재사용은 `OnEnable → OnRent`이며 `OnCreate`는 다시 호출하지 않는다.
- 반환하면 태그·그룹 목록에서 빠지고, 이벤트를 보내지 않고, 물리 조회에 나타나지 않고, 월드 저장 대상에서 빠진다. 닿아 있던 트리거는 다음 틱에 Exit를 받는다.
- 다른 부모 아래로 다시 대여하면 부모, 위치, 자식의 로컬 변환과 활성 상태, 충돌체, 태그·그룹이 초기화된다. 첫 접촉은 Stay가 아니라 Enter이고, 스크립트 상태는 `OnRent`에서 초기화된다.
- 같은 틱에 반환하고 다시 대여하면 이전 접촉은 Exit로 끝나고 다음 틱에 새 Enter가 시작된다. 새로 대여한 인스턴스는 `Instantiate`처럼 다음 틱부터 업데이트와 접촉에 참여한다.
- 제거된 인스턴스는 다시 대여하지 않는다. 풀에서 빌리지 않은 오브젝트와 이미 반환한 오브젝트의 `Return`은 false다.
- 에디터와 같은 동적 스크립트로 BarrageLab의 프리팹 탄환 씬을 900틱씩 실행했다. 두 방식 모두 3,600발 중 1,091발이 명중했고 Exit와 활성 수도 같았다. 마지막 300틱의 관리 할당은 풀 14,789,904 B, 생성·제거 205,637,832 B였다.

기존 스프라이트 풀 검사와 BarrageLab 스프라이트 탄막 검사도 그대로 통과했다.

에디터의 Scene Play는 실행하지 않았다. Windows·Linux 게스트 결과는 아래에 있다.

## Windows x64와 Linux x64 게스트

2026-11-23, 이 Mac의 QEMU(TCG) 게스트에서 샘플 BarrageLab을 각 게스트에서 Release와 Native AOT(distribution)로 빌드하고 같은 [재생 실행기](../../BarrageLab.PrefabPools/README.md)로 실행했다. 네 배포본 모두 `PASS same 2338 hits from 7200 bullets; reused 6721 of 7200`이었다. [게스트 환경과 실행 방법](../x64-guest-10.2-10.8/README.md)

| 게스트 | 배포본 | 방식 | 틱 | 발사 | 명중 | Exit | 활성 | 생성 | 재사용 | 프레임당 할당 | Gen0 |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Windows 11 x64 | Release | `Rent`·`Return` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 587 | 6,721 | 57,655 B | 8 |
| Windows 11 x64 | Release | `Instantiate`·`Destroy` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 7,308 | 0 | 839,508 B | 129 |
| Windows 11 x64 | Native AOT | `Rent`·`Return` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 587 | 6,721 | 57,625 B | 8 |
| Windows 11 x64 | Native AOT | `Instantiate`·`Destroy` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 7,308 | 0 | 841,664 B | 128 |
| Debian 12 x64 | Release | `Rent`·`Return` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 587 | 6,721 | 57,623 B | 8 |
| Debian 12 x64 | Release | `Instantiate`·`Destroy` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 7,308 | 0 | 839,120 B | 129 |
| Debian 12 x64 | Native AOT | `Rent`·`Return` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 587 | 6,721 | 57,623 B | 8 |
| Debian 12 x64 | Native AOT | `Instantiate`·`Destroy` | 1,800 | 7,200 | 2,338 | 2,338 | 503 | 7,308 | 0 | 841,221 B | 128 |

- 틱·발사·명중·Exit·활성·생성·재사용·반환 수는 macOS의 네 실행과 모두 같았다.
- 관리 할당은 macOS와 수십~수천 바이트 다르고 생성·제거 방식의 Gen0 수는 128–129회(macOS 127회)였다. 런타임과 OS가 다르므로 바이트 단위 일치를 기대하는 값이 아니다. 풀 방식의 프레임당 할당은 생성·제거 방식의 약 6.8–6.9%로 macOS와 같은 비율이었다.
- 업데이트 시간은 에뮬레이션 값이다(예: Linux Release 풀 평균 2,415 µs, 생성·제거 11,845 µs). Mac 시간과 비교하지 않는다. [실행별 값](x64/runs.tsv) · 원본 요약: [Windows Release](x64/win-x64-release.json) · [Windows Native AOT](x64/win-x64-native-aot.json) · [Linux Release](x64/linux-x64-release.json) · [Linux Native AOT](x64/linux-x64-native-aot.json)
