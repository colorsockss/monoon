# SeedLab 길찾기 회귀

대상은 SeedLab 쇼케이스의 Route 지도다(`Scenes/ShowcaseRoute.scene`, `Assets/Showcase/Field.tileset`: 길 1, 풀 3, 진흙 8, 다리 1, 물·울타리는 막힘). 쇼케이스의 RouteCourier는 비와 다리가 저절로 흘러가는 연출이라, `verify.py prepare`는 복사본에서 그 스크립트를 시험용 키 드라이버 [DriveRoute](DriveRoute.source)로 바꾸고(사용자 결정 2026-11-26) 시작 씬을 Route로 바꾼다. 드라이버 키는 R 시작점으로, Space 출발(12틱에 한 칸), V 4/8방향, O 남쪽 여울에 다리 놓기/걷기, B 북쪽 길에 비(길 ↔ 진흙)다.

관찰 스크립트 [TraceCourier](TraceCourier.source)는 배달원의 로컬 Transform과 설정 변경 입력만 기록한다. 검사는 [route_model.py](route_model.py)의 독립 다익스트라로 최소 비용을 구해 여섯 경우(4방향·8방향 × 다리 유무, 비, 걷는 중에 다리 놓기)의 방문 기록과 대조한다. 걷는 중에 바꾼 경우는 바꾼 칸부터 남은 비용을 비교한다.

Mac에서 저장소 루트 기준으로 실행한다. 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 최신 소스로 빌드한다.

```sh
qa=engine/tests/SeedLab.GridNavigation
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/SeedLab "$work/SeedLab"
./monoon build "$work/SeedLab" release --profile distribution --output "$work/builds"
./monoon build "$work/SeedLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/SeedLab" \
  engine/.artifacts/bin/RouteProbe/release/RouteProbe.dll "$work/scene-play"
```

Windows에서는 같은 명령에 패키지 에디터와 배포 플레이어 경로를 지정한다. 에디터 검증은 창이 있는 데스크톱 세션에서 수행한다. Linux에서는 플레이어만 검사한다(Linux 에디터는 2026-11-23부터 지원하지 않는다). 독립 플레이어는 `--replay-input`을 사용한다. 각 실행의 `result.json`에는 테스트별 경로와 비용이 들어간다.
