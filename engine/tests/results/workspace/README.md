# KeyRoom 작업 보존·확장 검증

2026-11-14, macOS 27.0.1 arm64. 패키징한 MoNoon.app에서 KeyRoom 복사본과 그 템플릿으로 만든 새 프로젝트를 사용했다.

| 작업 | 관찰 결과 |
| --- | --- |
| **Project → Extensions → Place Coin** 선택 후 Undo | 씬에 배치한 Coin이 Undo 한 번에 사라졌다. |
| 미저장 코인의 이름을 Recovery Coin으로 변경하고 테스트 에디터 프로세스를 강제 종료 | 디스크의 씬에는 이름이 없었다. 재시작 시 [복구 창](recover.png)이 나타났고 Recover를 누르자 Hierarchy에 이름이 돌아왔다. |
| 복구 상태에서 디스크의 Wall Top 이름도 수정하고 Save Scene | 저장된 씬에 Recovery Coin과 외부 변경 External Wall Top이 함께 남았다. |
| 양쪽에서 Player 이름을 다르게 변경 | [충돌 창](merge.png)에서 Cancel을 누르자 디스크의 이름이 유지됐다. 다시 저장해 Use My Edits를 선택하자 Local Hero와 이전 외부 편집을 함께 보존했다. [Scene Play](play.png)에서 게임 스크립트가 실행됐다. |
| 데이터 에셋과 테이블 편집 | KeyRoomRules.data의 chestReach를 84로, Loot.table의 코인 표시명을 Golden Coin으로 저장했다. 각 파일에서 Undo 후 이전 값, Redo 후 새 값을 직접 읽었다. |
| **Project → Export Package…**에서 KeyRoom.tools 선택 | [365 × 280 선택 창](package.png)에서 프로젝트 검색·선택·내보내기 버튼을 사용했다. ZIP 안에 명령 정의, Coin.prefab, PNG, Loot.table, 메타데이터와 C# 소스가 들어갔다. |
| **File → Export Project Template…**와 **New from Template…** | 만들어진 프로젝트의 ID가 원본과 달랐고 시작 씬과 에셋 참조를 유지했다. |
| 새 프로젝트에서 확장 파일을 제거한 뒤 패키지 가져오기 | Project 패널에서 패키지를 더블클릭하자 파일 1개가 복원됐다. 되살아난 Place Coin 메뉴로 배치하고 저장했다. [Scene Play](template-play.png)의 CollectCoin에서 available이 True, collected가 False였다. |
| 새 프로젝트를 macOS Release로 빌드 | 독립 [Release 게임 창](release.png)에서 E와 오른쪽 방향키로 상자를 열고 코인을 획득해 `Coin collected.` 로그를 확인했다. 빌드한 게임 콘텐츠에서 에디터 명령과 복구 파일이 제외됐다. |

추가 검사에서는 같은 필드를 두 가지 방향으로 병합하고, 다른 에디터가 열고 있는 복구 사본을 제외하며, 외부에서 수정한 프로젝트 설정에 Undo를 시도해 원본을 보존하는 경우를 확인했다. 이전 Door.clip.yaml을 변환하기 전 원본과 메타데이터가 백업 ZIP에 들어가는 것도 검사했다. 전체 회귀 검사 220개가 통과했다.

[검사 결과](checks.txt) · [검증 코드](../../Engine.Checks/WorkspaceChecks.cs) · [사용 설명서](../../../../docs/manual/workspace.md)
