# KeyRoom Inspector 다중 편집 검증

2026-11-17에 플랫폼별 배포 에디터에서 같은 KeyRoom 샘플 ZIP을 풀어 검사했다. Project에서 `Scenes/WallWorkshop.scene`을 열고 Hierarchy의 **Wall 01**을 클릭한 뒤 **Wall 10**을 Shift+클릭했다. 아래 화면은 실제 Inspector를 선택 직후, **Box Collider 2D → Mask → None**을 누른 뒤, Undo 단축키를 한 번 누른 뒤의 순서로 캡처한 것이다.

| 배포 에디터 | Undo 입력 | 화면 | 벽별 값과 오버라이드 |
| --- | --- | --- | --- |
| macOS 27.0.1 arm64 · MoNoon.app | Cmd+Z | [Inspector](osx-arm64.png) | [검사 결과](osx-arm64.json) |
| Windows 11 Enterprise LTSC x64 · QEMU | Ctrl+Z | [Inspector](win-x64.png) | [검사 결과](win-x64.json) |
| Debian 12.15 x64 · QEMU, X11/Xvfb | Ctrl+Z | [Inspector](linux-x64.png) | [검사 결과](linux-x64.json) |

- 10개 벽은 같은 `WallSegment.prefab`의 인스턴스다. 가로 76, 세로 28은 공통값으로 표시됐고, 위치·레이어·트리거·마스크·색상은 `Mixed`로 표시됐다. 01·04·08 벽의 마스크는 프리팹 값 3을 상속하고 나머지 일곱 벽에는 마스크 오버라이드가 있다.
- 마스크를 모두 0으로 바꾼 직후, 편집 전 씬의 사본에 선택한 충돌체의 마스크만 0으로 설정한 예상 데이터와 실제 씬의 전체 직렬화 결과가 일치했다. 05번 벽은 원래 0이었다.
- Undo 한 번으로 10개 벽이 선택된 채 마스크가 혼합 상태로 돌아왔다. 편집 전과 Undo 후의 전체 씬 바이트, 벽별 프리팹 오버라이드 목록·원본값·로컬값, 원본 프리팹 파일 해시가 일치했고 Undo 스택은 비었다.
- Mac에서는 에디터 창을 1000 × 720으로 줄인 경우에도 SDL Scene과 Inspector가 분리되고 마스크 버튼과 메뉴가 클릭됐다. 개별 레이어 메뉴에서 혼합된 31번 비트만 켰을 때도 각 벽의 다른 31개 비트가 유지되고 Cmd+Z로 복원됐다. [31번 레이어 입력 기록](osx-arm64-layer31.json)

세 OS에서 편집 전과 Undo 후의 씬 SHA-256은 모두 `A9CA080881EB2729B55BA9CAC6C6B9C8D06B07285F0B1CB8F3F6203EB557B0EA`이었다. 마스크를 모두 0으로 바꾼 직후의 SHA-256도 세 OS에서 일치했다.

자동 회귀는 저장소 루트의 `./monoon test`로 다시 실행한다. 검사에는 상속·오버라이드가 섞인 10개 마스크, 상위 비트 설정과 해제, 같은 값 재적용, Undo, Redo와 프리팹 원본 보존이 포함된다. [검사 코드](../../Engine.Checks/MultiInspectorChecks.cs)
