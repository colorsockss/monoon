# KeyRoom 스크립트 트윈 회귀 (16.3)

`verify.py prepare`는 KeyRoom을 별도 폴더에 복사하고 관찰 스크립트 [TraceTweens](TraceTweens.source)를 붙인다. 이 스크립트는 고정 틱마다 가방(Satchel)의 로컬 위치·스케일·그리는 색과 **Controls** 버튼의 배치 상자를 `R` 형식으로 기록한다.

입력 기록은 140프레임(각 1/60초)이다.

| 프레임 | 입력 | 의도 |
| --- | --- | --- |
| 5 → 6 | E | 상자를 열어 코인 생성 |
| 10 → 60 | → 누름 → 뗌 | 코인을 얻어 [SatchelPop](../../../samples/KeyRoom/Scripts/SatchelPop.cs)이 게임 시간 트윈 세 개(Scale·Move·Color)를 시작 |
| 100 → 101 | (742, 28) 마우스 클릭 | **Controls** 버튼의 UI 시간 `UiRect` 트윈 |

가방은 트윈 시작 기준 0·3·6·9·15틱, 버튼은 클릭 기준 0·1·3·5·8프레임의 값을 이징 공식과 비교한다(오차 0.0001·0.001). `compare`는 Scene Play·Release·Native AOT 기록 파일이 바이트 단위로 같은지 비교하고 다섯 시점 값을 `points.json`에 쓴다. 순서·지연·UI 시간·소유 오브젝트 종료·할당 0은 `./monoon test`의 [EngineFeatures1Checks](../Engine.Checks/EngineFeatures1Checks.cs)가 확인한다.

```sh
qa=engine/tests/KeyRoom.Tweens
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/KeyRoom "$work/KeyRoom"
./monoon build "$work/KeyRoom" release --profile distribution --output "$work/builds"
./monoon build "$work/KeyRoom" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer" "$work/aot"

engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/KeyRoom" \
  engine/.artifacts/bin/TweensProbe/release/TweensProbe.dll "$work/scene-play"
python3 "$qa/verify.py" compare "$work/scene-play/scene-play.trace" "$work/release/release.trace" "$work/aot/aot.trace"
```

[검증 결과](../results/engine-features-1/README.md)
