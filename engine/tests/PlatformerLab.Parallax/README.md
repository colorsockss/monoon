# PlatformerLab 패럴랙스 회귀 (16.2)

`verify.py prepare`는 PlatformerLab을 별도 폴더에 복사해 시작 씬을 [Parallax.scene](../../../samples/PlatformerLab/Scenes/Parallax.scene)으로 바꾸고 관찰 스크립트 [TraceParallax](TraceParallax.source)를 붙인다. 이 스크립트는 고정 틱마다 카메라 중심 X와 하늘(Scroll 0)·언덕(0.25)·나무(0.6)의 `TryGetParallaxOffset` 값을 `R` 형식으로 기록한다.

입력 기록은 입력 없는 120프레임(각 1/60초)이다. 카메라는 Position Tween 2D(640, 4초, 왕복)로 오른쪽으로 320 이동한다.

- 모든 틱에서 레이어 오프셋이 `(1 − Scroll) × 카메라 이동량`과 같은지(오차 0.001) 확인한다.
- `compare`는 Scene Play·Release·Native AOT 기록 파일이 바이트 단위로 같은지 비교한다.
- `snapshots`는 Release와 Native AOT가 창 없는 재생에서 0·30·60·119프레임에 저장한 PNG가 같은지 비교한다(`MONOON_REPLAY_SNAPSHOTS`, SDL 소프트웨어 렌더러).

레이어당 그리기 한 번과 할당 0은 `./monoon test`의 [EngineFeatures1Checks](../Engine.Checks/EngineFeatures1Checks.cs)가 확인한다.

```sh
qa=engine/tests/PlatformerLab.Parallax
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/PlatformerLab "$work/PlatformerLab"
./monoon build "$work/PlatformerLab" release --profile distribution --output "$work/builds"
./monoon build "$work/PlatformerLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/PlatformerLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
python3 "$qa/verify.py" snapshots "$work/release" "$work/aot"

engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/PlatformerLab" \
  engine/.artifacts/bin/ParallaxProbe/release/ParallaxProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play/scene-play.trace" "$work/release/release.trace" "$work/aot/aot.trace"
```

Scene Play는 시작 훅이 패키지 에디터에서 Play를 누르고 같은 기록을 `InputReplay.Play`로 재생한 뒤 Stop한다. [검증 결과](../results/engine-features-1/README.md)
