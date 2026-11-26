# LedgeRun 전체 경로 리플레이

[route.tsv](route.tsv)는 60 Hz에서 Trailhead를 열고 슬롯 3을 선택한다. 초원에서 낙하와 체크포인트 복귀를 거쳐 동굴을 통과한다. 탑에서는 왼쪽 벽을 차고 올라 보석을 하나 주운 뒤 승강기를 탄다. 정상에서는 양쪽 도약대를 오가며 돌진·착지 충격파·3발 불씨를 피한다. 벽 기절 세 번에 한 번씩 밟고, 신호기가 올라온 뒤 깃발에 도달한다. 결과 패널을 거쳐 Trailhead로 돌아온다.

macOS에서 플레이어 템플릿과 에디터를 준비한 뒤 실행한다.

```sh
python3 engine/scripts/run-harness.py LedgeRun.Play --mode release,native-aot
python3 engine/scripts/run-harness.py LedgeRun.Play --mode scene-play
```

검사는 네 스테이지의 클리어 순서, 토큰·보석·틱 수와 슬롯 3의 저장을 검증한다. 네 구간의 합과 슬롯의 최단 원정 기록도 대조한다. 입력은 보석을 한 개 이상 모으고 정상 클리어에 15초 이상 걸려야 한다. macOS에서는 슬롯 카드와 로고, Courier와 HUD, 제목 배너, Warden 공격, 집계 중·완료 화면을 포함한 18프레임을 SDL 소프트웨어 렌더러로 찍는다. 기준 SHA-256을 확인하고 실행 경로 사이에 바이트 단위로 비교한다. [기준 캡처 해시](../results/ledgerun/macos-arm64-sha256.tsv)

Linux 게스트에서는 Release와 Native AOT를 직접 내보내고 같은 경로를 헤드리스로 실행한다.

```sh
qa=engine/tests/LedgeRun.Play
work=$(mktemp -d)
./monoon build samples/LedgeRun release --target linux-x64 --profile distribution --output "$work/builds"
./monoon build samples/LedgeRun native-aot --target linux-x64 --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/LedgeRun-linux-x64/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/LedgeRun-linux-x64/MoNoonPlayer" "$work/aot"
python3 "$qa/verify.py" compare "$work/release" "$work/aot"
```

[제작·검증 결과](../results/ledgerun/README.md)

### UI 정렬 오버레이

프로젝트 테스트는 100%와 200%에서 키캡의 글자 중심과 안쪽 여백, 조작 묶음 간격, 구간 설명 카드의 두 줄 정렬을 확인한다. 결산의 12px 들여쓰기와 시간·토큰·보석 열, 보스 바 아래 입장 카드 위치도 검사한다. 다음 환경 변수를 켜면 실제 `IGameUi.Bounds`를 로그에 기록한다. 오버레이는 같은 배율의 SDL PNG에 1px 경계를 그린 뒤 선택한 영역을 두 배로 확대한다.

```sh
UseSharedCompilation=false MONOON_LEDGERUN_UI_BOUNDS=1 ./monoon test project samples/LedgeRun > /tmp/ledgerun-ui.log
python3 engine/tests/LedgeRun.Play/ui-overlay.py /tmp/ledgerun-ui.log Summit 2 capture.png overlay.png --crop 60 0 325 213
```
