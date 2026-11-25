# InventoryLab 스크립트 필드 타입 회귀 (13.8)

InventoryLab의 [SupplyTuning](../../../samples/InventoryLab/Scripts/SupplyTuning.cs)은 13.8에서 더한 필드 종류를 하나씩 가진다: `long`, `uint`, `Color`, `[ColorUsage(false)] string`, 플래그 enum, `int[]`·`float[]`·`string[]`·`bool[]`, `List<int>`, `Dictionary<string, int>`, `[Serializable]` 구조체.

[verify.py](verify.py) `prepare`는 InventoryLab을 복사해 시작 씬(약초 마당 `Scenes/Showcase.scene`)에 QA Supply 오브젝트(SupplyTuning + [FieldTrace](FieldTrace.source))를 넣는다. FieldTrace는 첫 틱에 SupplyTuning 값을 한 줄씩 `MONOON_FIELD_TRACE`에 쓴다.

## 에디터 (editor 단계)

1. `edit`: [프로브](StartupHook.cs)가 Inspector 행으로 모든 필드를 바꾼다. 숫자 입력, 색 입력(알파 포함·제외), 플래그 체크, 배열 원소 입력과 `+`·`×`, 목록 추가, 사전 행 추가·키 이름 변경(긴 키)·값 입력·삭제, 구조체 펼치기와 멤버 입력·선택이다. Seed·Unlocked·Prices·FirstWave는 Undo 한 번으로 이전 JSON, Redo로 다음 JSON이 되어야 한다. 1280·1600·1920px에서 섹션의 모든 행이 같은 값 열 x와 같은 `+`·`×` 오른쪽 끝을 가져야 하고, 긴 키 입력 상자가 값 상자와 같은 폭이어야 한다. 캡처를 남기고 저장한다.
2. `reopen`: 새 에디터에서 저장한 값이 Inspector에 그대로 나오는지 보고, Scene Play로 같은 기록을 재생해 trace를 쓴다.

## 실행

Mac 저장소 루트에서. 창을 여는 명령은 GUI 잠금으로 감싼다.

```sh
qa=engine/tests/InventoryLab.ScriptFields
work=$(mktemp -d)
engine/scripts/build-players.sh && engine/scripts/build-editor-mac.sh
dotnet build $qa -c Release -m:1 -p:UsedAvaloniaProducts=
dll=engine/.artifacts/bin/ScriptFieldsProbe/release/ScriptFieldsProbe.dll
python3 $qa/verify.py prepare samples/InventoryLab "$work/InventoryLab"
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon "$work/InventoryLab" $dll "$work/out" edit
sh engine/scripts/gui-lock.sh python3 $qa/verify.py editor editor/MoNoon.app/Contents/MacOS/MoNoon "$work/InventoryLab" $dll "$work/out" reopen
./monoon build "$work/InventoryLab" release --profile distribution --output "$work/builds"
./monoon build "$work/InventoryLab" native-aot --profile distribution --output "$work/builds"
python3 $qa/verify.py play "$work/builds/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 $qa/verify.py play "$work/builds/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot"
python3 $qa/verify.py compare "$work/out" "$work/release" "$work/aot"
python3 $qa/verify.py scan "$work/builds/InventoryLab.app" "$work/builds/NativeAOT/InventoryLab.app"
```

`play`와 `compare`는 창을 열지 않으므로 Linux x64 게스트에서 같은 프로젝트의 Release·Native AOT에도 쓸 수 있다(게스트 실행은 coordinator 몫). [결과](../results/inspector-fields/README.md)
