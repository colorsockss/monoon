# SeedLab 8방향 오토타일·무작위 타일·움직이는 타일 회귀 (16.9)

대상은 SeedLab 쇼케이스 Route의 **Hedges**·**Decor** 레이어다(2026-11-26부터, 이전에는 `meadow.py`가 만든 `Meadow.scene`). 그림과 타일셋은 [showcase-src/seed/hedges.py](../../../showcase-src/seed/hedges.py)가 만든다: 47칸 blob 생울타리 브러시(8 neighbours, Random 4, Seed 7), 꽃 무더기 무작위 브러시(Random 4, Seed 2026), 횃불(네 장, 8 FPS), 강물 물결(세 장, 4 FPS). 엔진과 별개인 판정 모델(47칸 blob 마스크, 좌표·시드 해시, 게임 시간으로 고르는 애니메이션 장)은 [garden.py](garden.py)에 있다. 쇼케이스 울타리에 없는 연결 모양은 `./monoon test`의 AutotileChecks가 모양마다 표본을 그려 확인한다.

`verify.py prepare`는 샘플 복사본의 시작 씬을 Route로 바꾸고 관찰 스크립트 [TraceAutotile](TraceAutotile.source)를 붙인다. 이 스크립트는 10틱에 안쪽 덤불 (5, 3)을 지우고 (6, 4)에 새로 심고 모서리 (0, 0)을 지우며, 꽃 (4, 2)와 물결 (8, 0)도 지운다. 30틱에 덤불과 꽃, 물결을 되돌린다. 매 틱 물결 (8, 2)와 횃불 (16, 4)의 장을, 1·20·40틱에는 두 레이어 18×9칸 전체의 그림을 기록한다.

- `play`/`editor.py`: 기록의 모든 칸이 판정 모델과 같은지 확인한다. 30틱에 다시 칠한 꽃은 처음과 같은 무작위 그림, 다시 칠한 물결 칸은 이웃과 같은 장이어야 한다. 장은 `floor(게임 초 × FPS) mod 장 수`이고, 게임 초는 float 틱 간격을 더한 값이다.
- `compare`: Scene Play·Release·Native AOT 기록이 (가장 짧은 기록의 줄 수까지) 바이트 단위로 같은지 비교한다.
- `snapshots`: Release와 Native AOT가 창 없는 재생에서 5·25·45프레임에 저장한 PNG가 같은지 비교한다.
- 에디터 프로브는 Tileset 패널(`Hedges.tileset`)에서 생울타리 브러시(8 neighbours, Random 4)의 Seed를 7→8→7로, 물결 타일의 FPS를 4→6→4로 저장·재열기하고, 700·380px에서 Random·Seed 행과 Frames·FPS 행이 Brush·Terrain·Cost 열에 맞는지(입력 칸 너비 포함) 확인하고 캡처한다.

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
