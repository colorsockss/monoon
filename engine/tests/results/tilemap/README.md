# KeyRoomTiles 타일맵 검증

2026-11-13. KeyRoom의 첫 방을 복사해 Floor tiles에는 통과 가능한 타일을, Wall에는 충돌 타일을 배치했다. 원본 벽의 폭 24와 높이 274를 재현하기 위해 샘플의 Wall만 월드 셀 크기 24×2를 사용한다. 새 레이어의 기본 셀 크기는 32×32다. [샘플](../../../../samples/KeyRoomTiles/)

| 실행 대상 | 확인한 결과 |
| --- | --- |
| Mac arm64 에디터 | Project 메뉴로 타일셋을 만들고 기존 타일셋을 더블클릭해 Solid를 변경했다. 좁은 창에서 다시 열어 설정을 확인했다. Hierarchy 우클릭으로 만든 자식 레이어에 Project의 타일셋을 드래그하고 브러시로 칠했다. [팔레트](palette.png) · [Scene 편집](layer-painted.png) |
| Mac 편집 기록 | 기존 Wall에 청크 경계를 가로지르는 획을 저장했다. Cmd+Z 후 저장한 씬은 변경 전 파일과 바이트가 같았고, Cmd+Shift+Z 후 저장한 씬은 칠한 파일과 같았다. 앱 종료 후 다시 열어 레이어를 확인했다. |
| Mac Scene Play | 아래 방향키로 짧은 타일 벽에 막히고, 왼쪽으로 열쇠를 얻은 뒤 오른쪽으로 둘째 방에 진입했다. [벽 접촉](scene-wall.png) · [둘째 방](scene-room.png) |
| Mac Release / Native AOT | 독립 실행한 두 플레이어에서 같은 경로를 진행했다. 논리 좌표 (260,154)에서 벽을, 그 위아래에서 바닥을 확인했다. [Release 벽](release-wall.png) · [둘째 방](release-room.png) · [Native AOT 벽](aot-wall.png) · [둘째 방](aot-room.png) |
| Windows x64 Release | 기존 Windows VM에서 방향키와 WASD 각각으로 타일 벽에 닿아 계속 눌러도 막혔다. 열쇠와 둘째 방 HUD를 확인했다. [벽](windows-tile-blocked.png) · [둘째 방](windows-03-room-two.png) · [게스트 측정값](windows.json) |
| Linux x64 Release | 기존 Debian VM의 X11에서 같은 두 입력 경로를 통과했다. 두 게스트 모두 배포 파일 해시와 16:9 뷰포트 검사를 통과했다. [벽](linux-tile-blocked.png) · [둘째 방](linux-03-room-two.png) · [게스트 측정값](linux.json) |

엔진 통합 검사는 음수 셀과 청크 경계, 건너뛴 포인터 좌표, 직사각형 채우기, 취소한 지우개 획, 오브젝트 이동과 획을 섞은 Undo/Redo, 저장 기준점, 프리팹·복제본의 독립성, 잘못된 셀과 타일 ID, 화면 밖 청크 300개, 고속 충돌과 대각선 미끄러짐을 확인한다. [TileChecks.cs](../../Engine.Checks/TileChecks.cs)

에디터 조작은 Mac에서, Windows·Linux의 OS별 실행은 Release 게임에서 검증했다.
