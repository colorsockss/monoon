# KeyRoom 에셋 이동과 이름 변경

2026-11-15, macOS 27.0.1 arm64의 배포 에디터 `editor/MoNoon.app`에서 KeyRoom 복사본을 열었다. 검증용 목적지로 `Assets/Doors`를 만들고 새 저장 프로필을 사용했다.

| Project 패널 조작 | 변경 직후 | Cmd+Z 한 번 |
| --- | --- | --- |
| `Assets/Door.png`를 F2로 `Gate.png`로 변경 | PNG·메타가 새 이름으로 바뀌고 씬의 Door가 새 경로를 참조했다. | `Assets/Door.png`와 원래 씬 참조 복원 |
| `Gate.png`를 `Doors` 폴더 행에 드롭 | `Assets/Doors/Gate.png`로 이동했다. 메타는 원래 파일과 바이트 단위로 같았다. | `Assets/Gate.png`와 해당 씬 참조 복원 |
| 프로젝트 루트의 `Scripts`를 F2로 `Gameplay`로 변경 | 하위 공용 파일을 포함한 C# 소스마다 기존 스크립트 ID를 확인했다. | `Scripts`와 생성 전 메타 상태 복원 |
| `Gameplay`를 `Assets` 폴더 행에 드롭 | `Assets/Gameplay/KeySequence.cs`의 식별자는 `KeySequence`로 유지됐다. | `Gameplay`를 원래 루트 위치로 복원 |

우클릭 **Move to Folder…**도 열어 315 × 150pt로 창을 줄였다. 목적지와 버튼이 모두 보였고, `Assets`를 선택해 PNG를 옮긴 뒤 Cmd+Z 한 번으로 복원했다. [축소한 이동 창](move-dialog.png)

각 Undo를 확인한 뒤 같은 변경을 다시 적용했다. PNG의 ID는 매번 `5bc5fe18-c056-5658-b492-61ee1a157a67`였다. [PNG 이름 변경](rename-sprite.png) · [스크립트 폴더 이동](moved-folder.png)

**Cmd+S → 에디터 종료 → 같은 프로젝트 다시 열기 → Scene Play**로 확인했다. 첫 방에서 오른쪽 방향키를 누르면 닫힌 문에 막혔고, Door를 선택한 Inspector에는 `Assets/Doors/Gate.png`와 `Solid`가 표시됐다. 왼쪽으로 이동해 열쇠를 획득하면 같은 Door가 활성 상태로 `Assets/DoorOpen3.png`를 표시하고 충돌체가 꺼졌다. 오른쪽으로 문을 통과한 뒤 F5로 기록한 슬롯에서 두 번째 방 `97ae2562-6e93-5e7a-be3b-45ec603896c6`과 `key: true`를 확인했다. [재실행 후 닫힌 문](reopened-locked.png) · [열쇠 획득 후 Inspector](reopened-open.png) · [두 번째 방](second-room.png)

엔진 회귀에서는 대소문자만 바뀐 이름, 목적지 충돌, Undo 후 원래 씬 바이트 복원, 이동 후 외부에서 편집한 다른 필드 보존도 검사했다. 스크립트가 옮겨진 프로젝트의 씬과 Coin 프리팹에 누락 참조가 없었고, 프로젝트 템플릿으로 내보내고 다시 설치한 뒤에도 Play가 시작됐다. [회귀 검사 코드](../../Engine.Checks/ProjectMoveChecks.cs)
