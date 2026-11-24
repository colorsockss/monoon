# SeedLab 8방향 오토타일·무작위 타일·움직이는 타일 회귀 (16.9)

[meadow.py](meadow.py)가 SeedLab의 `Meadow.png`·`Meadow.tileset`·`Meadow.scene`을 만들고, 엔진과 별개인 판정 모델(47칸 blob 마스크, 좌표·시드 해시, 게임 시간으로 고르는 애니메이션 장)을 담는다. 생울타리 도형은 47개 연결 모양을 모두 포함하고, 화면 가운데의 청크 경계 x = −1|0, y = −1|0을 가로지른다. Water 레이어의 연못(#57, 세 장, 4 FPS)은 생울타리 안쪽 6×4칸을 채워 같은 청크 경계를 지나고, 횃불 두 개(#60, 네 장, 8 FPS)가 생울타리 위에 있다.

`verify.py prepare`는 샘플 복사본의 시작 씬을 Meadow로 바꾸고 관찰 스크립트 [TraceAutotile](TraceAutotile.source)를 붙인다. 이 스크립트는 10틱과 30틱에 경계 칸을 `SetTile`로 칠하고 지우며(생울타리 (−1,−1)·(7,0)·(0,−6), 잔디 (0,0)·(−1,−1), 연못 (0,0)), 1·20·40틱에 세 레이어의 화면 안 1,400칸이 실제로 그리는 타일 번호(`TileInfo.Display`)를, 매 틱 연못 한 칸과 횃불 한 칸이 그 틱에 보여 주는 장을 기록한다.

- `play`/`editor.py`: 기록의 모든 칸이 판정 모델과 같은지 확인한다. 30틱에 다시 칠한 잔디는 처음과 같은 무작위 그림, 다시 칠한 연못 칸은 이웃과 같은 장이어야 한다. 장은 `floor(게임 초 × FPS) mod 장 수`이고, 게임 초는 float 틱 간격을 double로 더한 값이다(판정 모델도 같은 방식으로 더한다).
- `compare`: Scene Play·Release·Native AOT 기록이 (가장 짧은 기록의 줄 수까지) 바이트 단위로 같은지 비교한다.
- `snapshots`: Release와 Native AOT가 창 없는 재생에서 5·25·45프레임에 저장한 PNG가 같은지 비교한다.
- 에디터 프로브는 Tileset 패널에서 생울타리 브러시(8 neighbours, Random 4)의 Seed를 7→8→7로, 연못 타일의 FPS를 4→6→4로 저장·재열기하고, 700·380px에서 Random·Seed 행과 Frames·FPS 행이 Brush·Terrain·Cost 열에 맞는지(입력 칸 너비 포함) 확인하고 캡처한다.

편집기 획(칠하기·지우기·취소·직사각형)과 청크 경계, 시드 재현, 실행 중 해석본, 스트리밍 페이지는 `./monoon test`의 [AutotileChecks](../Engine.Checks/AutotileChecks.cs)(`--only autotile`)가, 공유 시계·한 시트 그리기·할당 0·잘못된 입력 거부는 [TileAnimationChecks](../Engine.Checks/TileAnimationChecks.cs)(`--only tile-animation`)가 확인한다.

```sh
qa=engine/tests/SeedLab.Autotile
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/SeedLab "$work/SeedLab"
./monoon build "$work/SeedLab" release --profile distribution --output "$work/builds"
./monoon build "$work/SeedLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
python3 "$qa/verify.py" snapshots "$work/release" "$work/native-aot"
python3 "$qa/verify.py" prepare samples/SeedLab "$work/editor-copy"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/editor-copy" \
  engine/.artifacts/bin/AutotileProbe/release/AutotileProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play/scene-play.trace" "$work/release/release.trace" "$work/native-aot/native-aot.trace"
```

[검증 결과](../results/engine-features-3/README.md#169-8방향-오토타일과-무작위-타일)
