# InventoryLab 콘텐츠 번들 검증

2026-11-14, macOS 27.0.1 arm64. 에디터의 **Build → Build Game…**에서 샘플 복사본의 번들 폴더를 변경하고 **Save Settings**를 눌렀다. 에디터를 다시 열어 같은 값이 표시되는 것을 확인했다. 창 너비를 420에서 365로 줄였을 때 입력란과 Build 버튼의 오른쪽 끝이 두 크기 모두 일치했다. [축소한 빌드 창](build-settings.png)

원본 InventoryLab은 `Assets/Dialogue`와 `Assets/Quests`를 설정한다. Release와 Native AOT에서 각각 `Dialogue.bundle`에 `Herbalist.data`, `Quests.bundle`에 `Collection.table`이 들어갔다. 두 배포본 사이에서 각 번들의 바이트가 같았으며 두 파일의 풀린 사본은 배포된 `Game/`에 없었다.

| Mac에서 실행한 검사 | Release | Native AOT |
| --- | --- | --- |
| SDL 창에서 T로 약초상과 대화하고 의뢰 수락 → 약초 3개 수집 → 보상 수령 후 S로 저장 | 수정 1개, 의뢰 완료, 대화 노드 `paid` | 동일 |
| 새 프로필에서 기록된 475프레임·31이벤트를 헤드리스 재생 | Mac 기준 저장과 171바이트 전체 일치 | 동일 |
| 임시 배포본에서 대화 번들 손상 → 누락 → 원본 복원 | 앞의 두 경우 종료 코드 1, 슬롯 미생성. 복원 후 코드 0과 기준 저장 일치 | 동일 |

두 SDL 창에서 직접 만든 저장과 두 헤드리스 저장의 SHA-256은 모두 `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795`였다. 배포 프로필·저장 재개 검사와 전체 엔진 회귀 검사 372개도 통과했다. [AOT 보상 수령 화면](inventory-reward.png) · [기준 입력·저장](../../fixtures/InventoryLab/) · [번들 사용법](../../../../docs/manual/build.md#콘텐츠-번들)
