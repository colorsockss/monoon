# InventoryLab 데이터 목록 회귀

`verify.py play`는 배포한 InventoryLab 플레이어를 헤드리스 입력 재생으로 실행한다. 약초 마당(시작 씬)에서 **C**로 재고 씬(`Scenes/ShowcaseStock.scene`)을 열면 장부 목록이 포커스를 받는다. 패드 D-pad 아래를 41번 눌러 42번 아이템을 고른다. 패드 위쪽 버튼으로 이름순 정렬을 한 뒤 아래쪽 버튼으로 사용한다. 이어서 아래로 세 칸 이동해 한 번 더 사용한다. 장부 스크립트([LedgerList](../../../samples/InventoryLab/Scripts/Showcase/LedgerList.cs))는 사용한 번호, 정렬 이름, 만들어진 행 수를 저장 슬롯 2에 기록한다.

검사기는 샘플과 별도로 아이템 1,000개를 만들고 이름순(UTF-16 코드 단위, 같은 이름은 번호순)으로 정렬한다. 정렬 전 선택한 번호가 첫 사용 대상인지, 두 번째 사용 대상이 새 순서에서 세 칸 아래 번호인지 확인한다. 행 수는 270 높이 목록을 채우는 행 수에 한 행을 더한 9개 이하여야 한다. 결과는 `result.json`에 남는다.

Mac에서 저장소 루트 기준으로 실행한다. 엔진 코드가 바뀌었으면 먼저 [플레이어 템플릿](../../../docs/manual/build.md)을 다시 빌드한다.

```sh
qa=engine/tests/InventoryLab.DataList
work=$(mktemp -d)
engine/scripts/build-players.sh osx-arm64
./monoon build samples/InventoryLab release --profile distribution --output "$work/builds"
./monoon build samples/InventoryLab native-aot --profile distribution --output "$work/builds"
python3 "$qa/verify.py" play "$work/builds/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/release"
python3 "$qa/verify.py" play "$work/builds/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer" "$work/native-aot"
```

재고 씬은 시작 씬이 아니고 약초 마당의 저장 슬롯 1도 건드리지 않는다. 기존 약초상 입력 재생과 체크포인트 검사는 같은 빌드에서 `./monoon test inventorylab "$work/builds" --profile distribution`과 `./monoon test replay "$work/builds"`로 확인한다. 재활용, 패드 이동, 정렬, 필터, 휠, 클릭 검사는 `./monoon test`의 [DataListChecks](../Engine.Checks/DataListChecks.cs)에 있다.
