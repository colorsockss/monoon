# SeedLab 길찾기 회귀

`verify.py prepare`는 별도 프로젝트에 관찰 스크립트를 붙인다. 이 스크립트는 배달원의 로컬 Transform과 설정 변경 입력만 기록한다. 프레임마다 입력을 재생하고 실제 방문 좌표를 타일 좌표로 변환한다. 검사는 독립적인 다익스트라 탐색으로 최소 비용을 구해 방문 기록과 대조한다. 실시간 문 열기의 경우 문을 연 칸부터 남은 비용을 비교한다.

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

Windows와 Linux에서는 같은 명령에 해당 OS의 패키지 에디터와 배포 플레이어 경로를 지정한다. 에디터 검증은 창이 있는 데스크톱 세션에서 수행한다. Debian VM에서는 Xvfb를 사용한다. 독립 플레이어는 `--replay-input`을 사용한다. 각 실행의 `result.json`에는 테스트별 경로와 비용이 들어간다.
