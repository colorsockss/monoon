# KeyRoom 씬 편집 검증

2026-11-14, macOS 27.0.1 arm64. 패키징한 MoNoon.app에서 KeyRoom 복사본을 열어 마우스, 키보드와 에디터 메뉴로 편집했다. 각 변환 뒤에 저장한 씬에서 오브젝트별 값을 읽었고, 앱을 종료한 뒤 같은 프로젝트를 다시 열었다.

| 실제 조작 | 확인 결과 |
| --- | --- |
| Scene에서 Player 클릭 후 Chest를 Shift+클릭 | [두 개의 윤곽선과 Inspector 선택 개수](selected.png)가 나타났다. |
| Snap을 켜고 Move의 X축 드래그 | Player X가 274→306, Chest X가 332→364가 됐다. 둘 다 Y는 유지됐다. Cmd+Z 한 번으로 양쪽이 원래 좌표로 돌아갔다. |
| E로 원을 잡고 회전 | [두 오브젝트가 그룹 중심 주위로 45° 회전](rotated.png)했다. Undo 한 번으로 위치와 회전이 복구됐다. |
| R로 중앙 핸들 드래그 | [두 오브젝트의 X·Y 배율이 1.6](scaled.png)이 됐다. Chest Lid의 로컬 위치 (0, −5)와 배율 (1, 1)이 유지됐다. Undo 후 같은 조작을 저장하고 [다시 열어](reopened-scale.png) 일치하는 값을 확인했다. |
| Chest Lid와 Chest를 차례로 프리팹으로 생성, 상자 복제 후 너비 52로 변형 생성 | [변형 에셋](variant.png)이 상자 원본을 참조하고 뚜껑은 자체 원본을 참조했다. Cmd+Z로 변형 파일과 메타데이터가 함께 없어지고 Redo로 복원됐다. |
| 변형 인스턴스의 높이를 36으로 수정하고 Revert | 변형 기본 높이 21이 됐다. Cmd+Z로 높이 36이 복원됐다. |
| 상자 원본 인스턴스의 회전을 15°로 바꿔 Apply | 디스크의 원본과 변형 인스턴스에 15°가 반영됐다. Undo로 원본 회전이 0°, Redo로 다시 15°가 됐다. |
| 최종 저장 후 앱 재실행 | [Inspector](reopened-variant.png)에서 너비 52, 높이 36, 회전 15°를 확인했다. 뚜껑 ID와 원본 연결도 유지됐다. [오버라이드 목록](overrides.png)에서 인스턴스 높이와 외부 Player 참조를 확인했고, [Scene Play](reopened-play.png)에서도 기울어진 상자와 뚜껑이 함께 그려졌다. |

[995×650 에디터](compact.png)와 [좁은 오버라이드 창](compact-overrides.png)에서도 레이블과 필드를 확인했다. 테스트 프로젝트를 macOS Release로 내보낸 뒤 패키지의 씬에서 (너비 52, 높이 36, 회전 15°), 변형 프리팹에서 (52, 21, 15°)를 읽어 비교했다.

[좌표·배율 측정값](measurements.json) · [검사 결과](checks.txt) · [속성 상속·런타임 생성·순환 방지 회귀 검사](../../Engine.Checks/PrefabEditingChecks.cs) · [사용 방법](../../../../docs/manual/editor.md#다중-선택과-기즈모)
