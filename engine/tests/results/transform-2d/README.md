# Transform 2D — macOS arm64, 2026-11-13

패키징한 MoNoon 에디터로 KeyRoom 프로젝트 복사본을 열었다. 원본 샘플의 씬 배치는 유지했다.

1. [초기 Chest](initial.png)를 선택하고 Inspector에서 [Rotation 30°, Scale (1.5, 0.75), Pivot (0.5, 0.5)](parent-edited.png)를 직접 입력한 뒤 Cmd+S로 저장했다.
2. [Play 중 Chest Lid](play-lid.png)를 선택했다. 로컬 위치 (0, −5), 로컬 회전 0°인 뚜껑이 상자와 함께 시계 방향으로 기울어졌다.
3. Play를 멈추고 Hierarchy에서 뚜껑을 Chest에서 Wall로 드래그한 뒤 Cmd+S로 저장했다. [다시 Play한 화면](reparent-play.png)에서 모양이 유지됐다.
4. 앱을 종료하고 같은 프로젝트를 다시 열었다. [Hierarchy와 Inspector](reopened-lid.png)에는 Wall → Chest Lid, 로컬 위치 (−147.27, −57.32), 회전 30°, 배율 (1.5, 0.75)가 표시됐다. Wall의 원점은 (480, 176)이므로 뚜껑의 월드 원점은 약 (332.73, 118.68)이다. [재열기 후 Play](reopened-play.png)와 [990×655 창의 필드 정렬](compact.png)도 확인했다.

세 Play 캡처(1220×768)의 동일 영역 x=[504,572), y=[284,343)에 있는 상자와 뚜껑 픽셀을 직접 비교했다. 부모 변경 전·후·재열기 후 세 영역의 SHA-256이 모두 같았다: `4a495faa1f3ca34e21fa8a9ea052aedd6f257582c6d46d74d1e591a724d9c487`.

자동 검사: `dotnet run --project engine/tests/Engine.Checks -c Release -- .` 전체 통과. 기본값, 좌표 왕복, 반전·회전·비균일 스케일 부모로 옮긴 뒤 네 모서리의 월드 좌표(허용 오차 0.001), 복제, YAML 재열기, 런타임 프리팹, 회전된 트리거를 검사했다.
