# KeyRoom 프로젝트 에셋 검증

2026-11-14 · macOS 27.0.1 arm64 · 패키징한 MoNoon.app. KeyRoom을 임시 폴더에 복사하고 에디터의 키보드·마우스와 macOS 열기 대화상자를 사용했다. 아래 변경값은 검증 복사본에만 저장했다.

| 조작 | 확인 결과 |
| --- | --- |
| 프로젝트 루트에서 `t:table` 검색 후 Enter | [폴더 위치와 함께 Loot.table 한 개가 표시](project-search.png)되고 테이블 편집기가 열렸다. |
| KeyRoomRules.data의 Number 필드 변경 | 잘못된 문자열은 저장이 거부됐고 기존 72가 보존됐다. 84를 저장하고 앱을 종료한 뒤 다시 열어 [Number / 84](data-reopened.png)를 읽었다. [400px 폭](data-narrow.png)에서도 타입·값이 잘리지 않았다. |
| Loot.table의 coin 행 변경, 행 한 개 추가 | `coin / label = Gold Coin`, `row1 / label = Silver Coin`, `row1 / flag = silver`를 저장했다. 새 앱 프로세스에서 [두 행과 열 이름·타입](table-reopened.png)이 그대로였다. [좁은 창](table-narrow.png)에서도 표와 저장 버튼은 별도 영역에 남는다. |
| 선택 파일의 양방향 참조 조회 | [Coin.prefab의 직접 의존성](coin-dependencies.png)에 Coin.png, CollectCoin.cs, Loot.table이 표시됐다. [KeyRoomRules.data를 쓰는 곳](rules-used-by.png)은 Chest / OpenChest.Rules였다. |
| Loot.table만 프로젝트 밖으로 옮기고 누락 검사 | .meta와 매니페스트에 ID가 남아 있어도 [Coin.prefab / CollectCoin.LootTable의 Missing file](missing-table.png)이 나왔다. |
| macOS 파일 선택기로 Loot.table 임포트 | 파일과 함께 원래 ID를 복구했고 [프로젝트 전체 검사에서 오류가 사라졌다](restored.png). 이후 앱을 재실행해 위의 편집값을 읽었다. |

Mac용 Native AOT로 원본 KeyRoom을 내보내고 독립 앱에서 E를 눌렀다. [상자 옆에 생성된 코인](native-coin.png)에 닿으면 코인이 없어지고 [HUD에 COIN: 1](native-collected.png)이 표시됐다. 엔진 회귀 검사 199건도 통과했다. 세 Release 대상의 내보낸 파일에 테이블이 포함되는지 검사했으며, 이번에 Windows·Linux 게스트를 실행하지는 않았다.

[자동 검사 결과](checks.txt) · [검증 코드](../../Engine.Checks/ProjectCatalogChecks.cs) · [사용 설명서](../../../../docs/manual/project-assets.md)
