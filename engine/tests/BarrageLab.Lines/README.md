# BarrageLab 레이저·궤적 회귀

[레이저 씬](../../../samples/BarrageLab/Scenes/Laser.scene)에서 [LaserSweep](../../../samples/BarrageLab/Scripts/LaserSweep.cs)이 매 틱 반사점을 계산해 Line 2D(두께 6, 연노랑 → 분홍)에 넣고, 24틱마다 Trail 2D가 붙은 혜성 프리팹을 풀에서 빌려 쏜다. 입력은 [verify.py](verify.py)가 만든다. 480틱 동안 300틱에 Space(회전 정지), 360틱에 Space(재개), 420틱에 R(혜성 회수, 처음부터)을 누른다.

복사본에만 붙이는 [관찰 스크립트](LineTrace.source)가 매 틱 그려지는 레이저 점과 각 혜성의 위치·궤적 점(소수 3자리)을 기록하고, 60·200·330·425·470프레임을 저장한다. 검사 기준:

- 레이저 전체 길이 1,400px(세 번째 반사에서 끝나는 경우 제외), 최대 3회 반사, 정지 구간(303–359틱)에는 점이 바뀌지 않음.
- 궤적 점 수 ≤ 40(Max points), 동시에 5개 이상의 혜성, R 직후 혜성 1개 이하.
- 세 실행 경로의 기록과 캡처가 바이트 단위로 같다.

```sh
qa=engine/tests/BarrageLab.Lines
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/BarrageLab "$work/BarrageLab"
./monoon build "$work/BarrageLab" release --profile distribution --output "$work/builds"
./monoon build "$work/BarrageLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/BarrageLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/BarrageLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
dotnet build engine/tests/PlatformerLab.Paths -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/BarrageLab" \
  engine/.artifacts/bin/PathsProbe/release/PathsProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play" "$work/release" "$work/aot"
```

[결과](../results/lines-paths/README.md)
