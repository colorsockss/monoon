# InventoryLab 가방 그리드·드래그 앤 드롭 회귀 (16.13)

InventoryLab 재고 쇼케이스의 4×5 가방(`Scenes/ShowcaseStock.scene`의 **Bag grid**: UI Layout **Grid**, Columns 4, 끌어 놓기 칸 20개, [BagSlots](../../../samples/InventoryLab/Scripts/Showcase/BagSlots.cs))에서 마우스 끌기, 클릭으로 집고 놓기, 키보드, 게임패드로 칸을 바꾸는 같은 입력을 Scene Play, Release, Native AOT에서 재생한다. `prepare`는 복사본의 시작 씬을 재고 씬으로 바꾸고 관찰 오브젝트를 넣는다. 관찰 스크립트 `TraceBag`이 가방이 바뀐 UI 프레임마다 포커스 칸, 집은 칸, 20칸의 글자를 `bag.trace`에 남기고, 정해 둔 9개 프레임을 PNG로 저장한다.

저장소 루트에서 실행한다. 엔진 코드가 바뀌었으면 먼저 `engine/scripts/build-players.sh`와 `engine/scripts/build-editor-mac.sh`를 다시 실행한다.

```sh
qa=engine/tests/InventoryLab.BagGrid
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/InventoryLab "$work/InventoryLab"
./monoon build "$work/InventoryLab" release --profile distribution --output "$work/builds"
./monoon build "$work/InventoryLab" native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/runs/native-aot"
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/InventoryLab" \
    engine/.artifacts/bin/BagGridProbe/release/BagGridProbe.dll "$work/runs/scene-play"
python3 "$qa/verify.py" compare "$work/runs"
```

- 입력(60 Hz, 330프레임): 10–20 약초(0번 칸)를 5번 빈칸으로 끌기 → 40 열쇠(2번) 클릭으로 집기, 60 3번 빈칸 클릭으로 놓기 → 90–140 키보드 ←, ↓(빵), Enter, →, ↓(지도), Enter → 190–270 패드 D-pad ←, South(빈 칸 집기), East(취소), ↓, →(방패), South, ↑, ↑(밧줄), South(놓기). 집은 아이템은 칸에서 빠져 떠 있는 사본으로 옮겨지고(포인터를 따라가거나, 키·패드면 포커스 칸 위), 두 사본이 내려앉은 뒤 칸 글자가 바뀐다.
- 캡처 프레임 15(끌기 중 따라오는 칸), 30, 50(집은 칸 테두리), 80, 115, 175, 205, 245, 320.
- `check`는 각 단계 뒤 칸의 아이콘 태그(`<icon=…>`)와 포커스·집은 칸을 확인하고, `compare`는 세 실행의 `bag.trace`와 캡처가 바이트 단위로 같은지 확인해 `comparison.tsv`를 쓴다.
- [검사기](StartupHook.cs)는 Bag grid를 선택해 Inspector를 1280·1600·1920 px로 캡처하고 UI Layout 행(Axis, Columns, Gap, Padding)의 레이블 x·입력 x·폭이 같은지 확인한다(`inspector.txt`). [검증 기록](../results/engine-features-4/README.md)
