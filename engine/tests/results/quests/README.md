# InventoryLab 수집 퀘스트 검증

2026-11-14, macOS 27.0.1 (26A434), Apple Silicon arm64. 에디터에서 InventoryLab을 열고 Scene Play를 실행했다. macOS Release와 Native AOT는 각각 빌드한 앱의 실행 파일을 사용했다. 실행 경로별로 임시 사용자 프로필을 분리했다. 각 모드에서 세 번 앱을 닫고 새 프로세스로 다시 열었으며, 에디터도 매번 Play를 새로 시작했다.

| 단계 | 입력과 확인한 결과 |
| --- | --- |
| 진행 중 | 의뢰 버튼을 클릭하고 **1** 두 번, **S**를 눌렀다. 종료·재실행 후 **L**로 약초 2/3과 가방 속 약초 2개가 복원됐다. **J**로 조기 수령을 시도하고 재저장해도 수정은 생기지 않았다. [저장 원본](partial.save) |
| 목표 달성 | **1**을 한 번 더 누르고 **S**로 저장한 뒤 다시 열었다. **L**로 복원하자 약초 3/3과 **보상 받기** 버튼이 표시됐다. 가방에는 아직 수정이 없었다. [저장 원본](ready.save) |
| 수령 및 반복 입력 | 보상 버튼을 클릭하고 **J**를 두 번 더 누른 뒤 저장했다. 가방에는 약초 3개와 수정 1개가 있었다. 다시 열고 **L**을 누르자 **보상 수령 완료**가 복원됐다. 같은 버튼 위치를 클릭하고 **J → S**를 눌러도 수정은 1개였고, 저장 파일은 직전 수령본과 바이트 단위로 일치했다. [저장 원본](claimed.save) |

| 실행 | 2/3 복원 | 보상 대기 복원 | 보상 수령 완료 복원 |
| --- | --- | --- | --- |
| Scene Play | [화면](scene-play-active-reopened.png) | [화면](scene-play-ready-reopened.png) | [화면](scene-play-claimed-reopened.png) |
| Release | [화면](release-active-reopened.png) | [화면](release-ready-reopened.png) | [화면](release-claimed-reopened.png) |
| Native AOT | [화면](native-aot-active-reopened.png) | [화면](native-aot-ready-reopened.png) | [화면](native-aot-claimed-reopened.png) |

세 실행에서 각각 다섯 단계의 YAML을 파싱해 슬롯별 아이템 ID·수량, 의뢰별 진행도·수령 여부를 검사했다. 같은 단계의 파일은 세 실행 모두 동일했다. 에디터를 880×660으로 줄인 뒤에도 의뢰 버튼을 클릭할 수 있었고 목표·보상·버튼이 겹치지 않았다. [저장 파일 해시](checkpoints.csv) · [화면 인식 텍스트와 실제 저장값](observations.txt) · [축소한 Scene Play](scene-play-compact.png)

회귀 검사에서는 독립 프로젝트에 퀘스트 패키지만 가져와 프리팹을 실행하고, 가방이 가득 찬 경우의 재수령, 중첩 호출, 기존 v1 가방 데이터 이전, 손상된 수령 기록을 불러올 때 가방과 일지 보존을 검사했다. 전체 350개 검사가 통과했다. [검사 코드](../../Engine.Checks/QuestChecks.cs) · [전체 로그](checks.txt) · [사용법](../../../../docs/manual/quests.md)
