# InventoryLab 오브젝트 참조 목록

2026-11-16, macOS 27.0.1 arm64. 배포 에디터 MoNoon.app과 같은 씬에서 내보낸 Distribution 프로필의 Release 및 Native AOT 앱을 사용했다.

1. InventoryLab 사본을 열고 **Bag → InventoryWorkbench → Choices**에서 마지막 참조를 제거했다. Hierarchy의 **Answer 4**를 목록 헤더에 드래그해 네 번째 참조로 복원했다.
2. 목록에서 **Answer 4**를 두 번째 행에 놓고 저장했다. 씬 YAML에 기록된 오브젝트 ID를 이름으로 확인한 순서는 **1, 4, 2, 3**이었다. **Cmd+Z** 한 번 후 저장한 순서는 **1, 2, 3, 4**였다.
3. 다시 같은 드래그를 하고 저장했다. 에디터 프로세스를 종료한 뒤 프로젝트를 다시 열었다. Inspector 두 번째 행은 **Answer 4**였다. 별도 사본에서 창을 880 × 630으로 줄이고 긴 이름을 지정했을 때 이름은 말줄임으로 표시되고 제거 버튼은 같은 열에 정렬됐다. [작은 창의 Inspector](compact.png)
4. 각 실행에서 **T**로 약초상 대화를 열고, 화면 오른쪽 끝에 있는 원래 네 번째 버튼 **[2] 보상을 문의한다**를 마우스로 클릭했다. 세 화면에 **의뢰를 마쳤다면 수정 한 개를 드릴게요.**가 표시됐다. [선택 전 Scene Play](scene-choices.png)

| 실행 | 대화 노드 | 화면 |
| --- | --- | --- |
| Scene Play | reward | [재열기 후 보상 대사](scene-reward.png) |
| Release | reward | [보상 대사](release-reward.png) |
| Native AOT | reward | [보상 대사](aot-reward.png) |

각 실행에서 **S**로 만든 슬롯은 모두 67바이트였고 SHA-256은 `0f9b314314c78e33cf983dc954eb5f0a5beced20a96af14fbd5a4a549b5ee3ad`였다. 기존 약초 의뢰 입력 리플레이도 두 배포 앱에서 각각 475프레임·31개 이벤트를 재생해 기존 171바이트 기준 저장과 일치했다. 엔진 전체 회귀는 오브젝트 복제와 런타임 프리팹 생성 시 목록 내 참조가 새 자식 ID로 연결되는 경우까지 통과했다. [클릭 후 실제 저장](reward.save)
