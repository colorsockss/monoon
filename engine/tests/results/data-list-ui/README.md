# 데이터 목록 UI 검증

2026-11-22. macOS 27.0.1 arm64(Apple Silicon), .NET SDK 10.0.101. 이 Mac에서 InventoryLab을 배포 프로필로 Release와 Native AOT로 빌드하고, 두 플레이어에서 같은 입력 기록을 헤드리스로 재생했다. 입력은 가방 화면에서 **C**, D-pad 아래 41번, 패드 위쪽 버튼(이름순 정렬), 아래쪽 버튼(사용), D-pad 아래 3번, 아래쪽 버튼이다. 232프레임, 96개 이벤트로 구성된다. [입력 생성과 검사 코드](../../InventoryLab.DataList/verify.py)

| 실행 대상 | 정렬 전 선택 | 이름순 정렬 후 위치 | 사용한 ID | 행 객체 / 아이템 |
| --- | --- | ---: | --- | --- |
| Release | `item-0042` (41번) | 651번 | `item-0042`, `item-0162` | 11 / 1,000 |
| Native AOT | `item-0042` (41번) | 651번 | `item-0042`, `item-0162` | 11 / 1,000 |

정렬로 선택한 아이템이 41번에서 651번으로 이동했지만, 선택과 첫 사용 대상은 `item-0042`로 유지됐다. 두 번째 사용 대상은 새 순서에서 세 칸 아래인 `item-0162`이며, 검사기가 별도로 계산한 정렬 결과와 같다. 1,000개 목록에서 생성된 행은 11개다. 두 플레이어가 저장한 슬롯은 바이트 단위로 같았고 SHA-256은 `8be2deb63a8a8bfdf2a49c2732b24e6765655bd9a8764a6655d5ded242697819`이다. [저장 슬롯](catalog.save) · [Release 결과](release.json) · [Native AOT 결과](native-aot.json)

## 기존 회귀

같은 빌드에서 기존 InventoryLab 검사를 실행했다.

| 명령 | 결과 |
| --- | --- |
| `./monoon test inventorylab <빌드> --profile distribution` | Release와 Native AOT의 3개 체크포인트가 바이트 단위로 같음 |
| `./monoon test replay <빌드>` | 기록 입력, 헤드리스 Release, 헤드리스 Native AOT의 저장이 같음. 171바이트, SHA-256 `EED41DEFCBD7987A527DEB677C60DA8151AD0DE1616C717C646E92B26E70F795` |
| `./monoon test` | `All checks passed.` 데이터 목록 검사 4개 포함 |

`./monoon test`의 [DataListChecks](../../Engine.Checks/DataListChecks.cs)는 200 높이 목록에서 1,000개 아이템이 행 8개만 만드는지 확인한다. 패드 이동과 자동 스크롤, 정렬 후 선택·포커스 유지, 사용 이벤트의 대상 ID, 휠로 멀리 스크롤한 뒤의 복귀, 행 클릭, 선택 항목 필터링, 목록 밖으로 나갔다 돌아오기, 씬 재구성 후 바인딩 유지도 검사한다.

## 확인하지 않은 항목

- 에디터의 Scene Play와 SDL 게임 창의 화면 캡처는 이번에 확인하지 않았다. 독립 플레이어는 헤드리스 입력 재생으로만 검사했다.
- 실제 게임패드 장치는 사용하지 않았다. 패드 입력은 입력 기록의 패드 이벤트로 재생했다.
- Windows·Linux는 실행하지 않았다.
