# SeedLab 지형 비용과 셀 편집 회귀

`verify.py prepare`는 샘플 복사본의 배달원에 관찰 스크립트 [TraceTerrain](TraceTerrain.source)를 붙인다. 이 스크립트는 길찾기 스크립트의 상태를 읽지 않는다. 배달원의 로컬 Transform과 입력을 기록하고, 입력 다음 틱마다 엔진의 타일 레이어에서 13×7칸 전체의 `Display`·`Terrain`·`MoveCost`를 읽는다. 문 (6, 4), 벽 (6, 3), 칠한 진흙 (6, 1), 남쪽 진흙 (3, 5)의 중심은 `Physics.Overlap`으로 검사한다.

검사기는 타일셋과 별개로 바닥·문·진흙의 배치와 비용(진흙 12, 마른 진흙 1)을 정의한다. 일곱 경우마다 다음을 대조한다.

- 방문한 칸의 비용 합계와 독립 다익스트라 최소 비용. 걷는 중에 바꾼 경우는 바꾼 칸부터 남은 경로를 비교한다.
- 그려진 타일 번호. 점이 찍힌 칸만 지나서 최소 비용으로 도착할 수 있는지, 바꾸지 않은 경우 점과 실제 방문 칸이 같은지 확인한다.
- 문 타일과 충돌. 닫힌 문은 4번 타일·비용 0·충돌 있음, 열린 문은 5번 타일·비용 1·충돌 없음이어야 한다. 진흙에는 충돌이 없다.
- 복사본 `Route.tileset`의 `terrain`·`moveCost`와 검사기의 기준값.

Mac에서 저장소 루트 기준으로 실행한다. 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 최신 소스로 빌드한다.

```sh
qa=engine/tests/SeedLab.TileTerrain
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/SeedLab "$work/SeedLab"
./monoon build "$work/SeedLab" release --profile distribution --output "$work/builds"
./monoon build "$work/SeedLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/release" "$work/SeedLab/Assets/Route.tileset"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot" "$work/SeedLab/Assets/Route.tileset"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/SeedLab" \
  engine/.artifacts/bin/TerrainProbe/release/TerrainProbe.dll "$work/scene-play"
```

에디터 실행은 [StartupHook](StartupHook.cs)으로 복사본의 Route 타일셋을 연다. 진흙 타일의 **Terrain / Cost**를 `bog` / 9로 바꿔 저장하고 다시 연 뒤 `mud` / 12로 되돌린다. 700px과 380px 너비에서 입력 필드 정렬을 확인하고 창을 캡처한 다음, 같은 입력으로 Scene Play를 실행한다. 각 실행의 `result.json`에는 경우별 걸음 수, 비용, 바꾼 칸과 방문 순서가 들어간다. 기존 여섯 경로는 [SeedLab.GridNavigation](../SeedLab.GridNavigation/README.md)으로 따로 확인한다.
