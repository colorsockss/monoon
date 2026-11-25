# InventoryLab 월드 공간 텍스트 회귀 (16.6)

InventoryLab의 약초 마당(`Scenes/Showcase.scene`)을 240프레임 재생해 Scene Play, Release, Native AOT의 같은 프레임 캡처와 숫자 풀 기록이 바이트 단위로 같은지 본다. 장면에는 번역 키로 넣은 이름표(`label.herbalist` → Herbalist)와 서식 태그 간판(**Stall sign**)이 판 스프라이트 위에 있다. 입력은 → 80프레임(두 번째 약초), ↑ 20프레임, ← 10프레임(첫 번째 약초)이다. 약초를 주울 때마다 [HerbPickup](../../../samples/InventoryLab/Scripts/Showcase/HerbPickup.cs)이 풀에서 `+1` 숫자를 빌려 `SetWorldText`로 문구를 넣고, 숫자는 60틱 동안 30 단위 올라간 뒤 풀로 돌아간다.

저장소 루트에서 실행한다. 엔진 코드가 바뀌었으면 먼저 `engine/scripts/build-players.sh`와 `engine/scripts/build-editor-mac.sh`를 다시 실행한다.

```sh
qa=engine/tests/InventoryLab.WorldText
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/InventoryLab "$work/InventoryLab"
./monoon build "$work/InventoryLab" release --profile distribution --output "$work/builds"
./monoon build "$work/InventoryLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/native-aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/InventoryLab" \
    engine/.artifacts/bin/WorldTextProbe/release/WorldTextProbe.dll "$work/runs/scene-play"
python3 "$qa/verify.py" compare "$work/runs"
```

- `prepare`는 복사본의 시작 씬을 약초 마당으로 두고(에디터가 이 씬을 연다) 관찰 스크립트 [TracePool](TracePool.source)을 넣는다. TracePool은 `+1` 프리팹 풀의 Active·Available·Created·Reused·Returned가 바뀐 틱을 `pool.tsv`에 쓴다.
- 캡처 프레임 5, 85, 120, 150, 175, 235. `check`는 여섯 캡처가 모두 다르고, 첫 숫자를 60–100틱, 둘째 숫자를 140–200틱에 빌렸으며, 끝에 풀이 (0, 1, 1, 1, 2)인지 확인한다. 두 번째 숫자는 새로 만들지 않고 돌아온 첫 숫자를 다시 쓴다. `compare`는 세 실행의 캡처와 `pool.tsv`가 같은지 본다.
- `editor.py`의 [검사기](StartupHook.cs)는 간판(**Stall sign**)을 선택해 Inspector를 1280·1600·1920 px로 캡처하고 Text 2D 행(Text key, Text, Size, Color, Align, Layer, Order)의 레이블 x·입력 x·폭이 모두 같은지 확인한 뒤(`inspector.txt`), Play에서 같은 기록을 재생하며 SDL 캔버스·SDL_ttf로 같은 프레임을 오프스크린 렌더링한다.
- 프레임 시간·할당은 `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only world-text`가 잰다. [검증 기록](../results/engine-features-4/README.md)
