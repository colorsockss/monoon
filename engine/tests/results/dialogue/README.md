# InventoryLab 약초상 대화 검증

2026-11-14, macOS 27.0.1 (26A434), Apple Silicon arm64. MoNoon 에디터의 Scene Play, 독립 Release, Native AOT 앱을 각각 실행했다. 각 모드는 독립된 임시 프로필을 사용했다. 저장과 복원 사이에는 앱 프로세스를 종료했고, 에디터에서도 프로젝트를 다시 열어 새 Play 세션에서 확인했다.

| 같은 입력으로 확인한 분기 | 세 실행의 결과 |
| --- | --- |
| 약초상 → 의뢰 문의 → 사양 | 작별 대사로 이동했다. 의뢰와 가방은 비어 있었다. [저장값](declined.save) |
| 의뢰 수락 전에 보상 요청 | 보상 문의 대사에 머물렀고 보상은 지급되지 않았다. |
| 의뢰 문의 도중 S → 종료·재실행 → L | 약초 세 포기를 부탁하는 대사와 수락·사양 선택지가 복원됐다. 수락을 고르니 수집량 0/3으로 시작했다. [복원한 노드](offer.save) · [수락 후](accepted.save) |
| 약초 3개 수집 → 보상 문의 → 보상 받기 | 지급 대사로 이동했고 가방에는 약초 3개와 수정 1개, 일지에는 수령 완료가 기록됐다. [저장값](paid.save) |
| 지급 대사에서 S → 종료·재실행 → L → 대화를 끝내고 보상 재요청 | 지급 대사가 복원됐다. 다시 보상을 청하자 “이미 받은 보상입니다.”가 표시되었고 수정은 1개였다. 첫 대사로 돌아가 **나중에 온다**를 고르면 작별로 이동했다. [재요청 후](repeat.save) |

| 실행 경로 | 의뢰를 제안하는 대사 복원 | 보상 지급 대사 복원 |
| --- | --- | --- |
| Scene Play | [화면](scene-play-offer-reopened.png) | [화면](scene-play-paid-reopened.png) |
| Release | [화면](release-offer-reopened.png) | [화면](release-paid-reopened.png) |
| Native AOT | [화면](native-aot-offer-reopened.png) | [화면](native-aot-paid-reopened.png) |

첫 선택지는 마우스로 클릭했고 다른 선택지는 키보드로 골랐다. 에디터 창을 880×660으로 줄여서도 같은 선택지를 클릭했다. 모드별로 열 단계의 YAML을 파싱해 현재 노드, 퀘스트 수락·수집량·수령 여부, 가방 수량을 검사했다. 같은 단계에서 얻은 세 파일은 바이트 단위로 일치했다. 의뢰 문의와 보상 지급 두 지점 모두 재실행 직후 다시 저장한 파일이 종료 전과 같았다. [파일별 SHA-256](checkpoints.csv) · [축소한 Scene Play](scene-play-greeting.png) · [화면 인식과 저장 관측값](observations.txt)

## 편집한 선택지가 게임에 반영되는지

임시 프로젝트의 Project 패널에서 **Herbalist.data**를 열고 첫 대사를 **Which herbs do you need?**, 첫 선택지를 **Say farewell**로 수정했다. 목적지를 **offer**에서 **farewell**로 바꾸고 Save를 눌렀다. 에디터를 종료하고 다시 열어 수정값을 확인했다. 편집 창을 470×350으로 줄여 레이블·입력 시작점과 하단 버튼 정렬을 확인했다. Play에서 바뀐 문구가 표시됐고 첫 선택지를 누른 뒤 저장한 노드가 **farewell**, 퀘스트 목록이 빈 상태였다. [기본 편집 창](editor.png) · [재실행 후 축소 창](editor-compact.png) · [수정한 대사의 Play 화면](scene-edited.png)

전체 엔진 검사 370개가 통과했다. 독립 프로젝트로 패키지 가져오기와 프리팹 실행, 대사·선택지 직렬화, 잘못된 목적지 거부, 중첩 행동 방지, 이전 v1·v2 저장 복원도 검사했다. [대화 검사](../../Engine.Checks/DialogueChecks.cs) · [저장 호환성 검사](../../Engine.Checks/QuestChecks.cs) · [전체 검사 로그](checks.txt) · [사용법](../../../../docs/manual/dialogue.md)
