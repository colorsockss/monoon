# SeedLab 지형 비용과 셀 편집 회귀

대상은 SeedLab 쇼케이스의 Route 지도와 `Assets/Showcase/Field.tileset`이다. `verify.py prepare`는 [SeedLab.GridNavigation](../SeedLab.GridNavigation/README.md)과 같은 시험용 드라이버 DriveRoute를 붙이고, 관찰 스크립트 [TraceTerrain](TraceTerrain.source)을 Field에 붙인다. 이 스크립트는 길찾기 스크립트의 상태를 읽지 않는다. 배달원의 로컬 Transform과 입력을 기록하고, 입력 다음 틱마다 엔진의 타일 레이어에서 18×9칸 전체의 `Display`·`Terrain`·`MoveCost`를 읽는다. 남쪽 여울 (8, 5), 울타리 (5, 3), 북쪽 길 (3, 1), 풀 (3, 2)의 중심은 `Physics.Overlap`으로 검사한다.

검사기는 타일셋과 별개로 지도와 비용(길 1, 풀 3, 진흙 8, 다리 1)을 [route_model.py](../SeedLab.GridNavigation/route_model.py)에 정의한다. 일곱 경우마다 다음을 대조한다.

- 방문한 칸의 비용 합계와 독립 다익스트라 최소 비용. 걷는 중에 바꾼 경우(비, 다리)는 바꾼 칸부터 남은 경로를 비교한다.
- 그려진 타일 번호, 지형 이름, 비용. 출발할 때와 걷는 중에 바꾼 직후에 18×9칸 전체를 본다.
- 여울과 충돌. 물은 막히고 충돌이 있으며, 다리를 놓으면 비용 1·충돌 없음이 된다. 진흙과 풀에는 충돌이 없고 울타리는 막힌다.
- 복사본 `Field.tileset`의 `terrain`·`moveCost`와 검사기의 기준값.

Mac에서 저장소 루트 기준으로 실행한다. 먼저 [플레이어 템플릿과 에디터](../../../docs/manual/build.md)를 최신 소스로 빌드한다.

```sh
qa=engine/tests/SeedLab.TileTerrain
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/SeedLab "$work/SeedLab"
./monoon build "$work/SeedLab" release --profile distribution --output "$work/builds"
./monoon build "$work/SeedLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/release" "$work/SeedLab/Assets/Showcase/Field.tileset"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot" "$work/SeedLab/Assets/Showcase/Field.tileset"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/SeedLab" \
  engine/.artifacts/bin/TerrainProbe/release/TerrainProbe.dll "$work/scene-play"
```

에디터 실행은 [StartupHook](StartupHook.cs)으로 복사본의 Field 타일셋을 연다. 진흙 타일의 **Terrain / Cost**를 `bog` / 9로 바꿔 저장하고 다시 연 뒤 `mud` / 8로 되돌린다. 700px과 380px 너비에서 입력 필드 정렬을 확인하고 창을 캡처한 다음, 같은 입력으로 Scene Play를 실행한다. 각 실행의 `result.json`에는 경우별 걸음 수, 비용, 바꾼 칸과 방문 순서가 들어간다. 기존 여섯 경로는 [SeedLab.GridNavigation](../SeedLab.GridNavigation/README.md)으로 따로 확인한다.
