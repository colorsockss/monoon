# InventoryLab 월드 공간 텍스트 회귀 (16.6)

InventoryLab의 시장 거리(`Scenes/Labels.scene`)를 입력 없이 240프레임 재생해 Scene Play, Release, Native AOT의 같은 프레임 캡처가 바이트 단위로 같은지 본다. 장면에는 번역 키로 넣은 이름표(`label.herbalist` → 약초상), 서식 태그 간판, 한글 제목이 있고 `DamageBurst`가 20·110·200틱에 풀에서 데미지 숫자 100개를 빌려 `SetWorldText`로 숫자를 넣는다. 숫자는 60틱 동안 30 px 올라간 뒤 풀로 돌아간다.

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

- `prepare`는 복사본의 시작 씬만 시장 거리로 바꾼다(에디터가 이 씬을 연다). 플레이어는 기록의 `startScene`에서 시작한다.
- 캡처 프레임 10, 30, 79, 100, 120, 230. `check`는 10 ≠ 30 ≠ 79 ≠ 230, 10 = 100(숫자가 모두 풀로 돌아감), 30 = 120(다시 빌린 풀이 같은 그림)을 확인한다.
- `editor.py`의 [검사기](StartupHook.cs)는 간판을 선택해 Inspector를 1280·1600·1920 px로 캡처하고 Text 2D 행(Text key, Text, Size, Color, Align, Layer, Order)의 레이블 x·입력 x·폭이 모두 같은지 확인한 뒤(`inspector.txt`), Play에서 같은 기록을 재생하며 SDL 캔버스·SDL_ttf로 같은 프레임을 오프스크린 렌더링한다.
- 프레임 시간·할당은 `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only world-text`가 잰다. [검증 기록](../results/engine-features-4/README.md)
