# KeyRoom 런타임 프리팹 — macOS arm64, 2026-11-13

같은 Mac에서 패키징한 MoNoon 에디터, 독립 Release 게임, Native AOT 게임을 각각 새로 띄워 게임 창에 실제 키 입력을 보냈다. 시작 위치에서 E를 한 번 누르고 →를 약 0.52초 눌렀다. 아래는 각 창의 최종 빌드 캡처다.

| 실행 대상 | 열기 전 | E: 뚜껑이 사라지고 코인 생성 | →: 코인 제거, HUD에 COIN: 1 |
| --- | --- | --- | --- |
| 에디터 Play | [초기 상태](editor-before.png) | [Hierarchy에서 Coin 선택, 22×22, Is Trigger 체크](editor-coin.png) | [Hierarchy에서 제거, Inspector 선택 해제](editor-collected.png) |
| Release | [초기 상태](release-before.png) | [열린 상자와 코인](release-coin.png) | [획득 후](release-collected.png) |
| Native AOT | [초기 상태](aot-before.png) | [열린 상자와 코인](aot-coin.png) | [획득 후](aot-collected.png) |

에디터 창을 1220×768에서 [990×655로 축소](editor-compact.png)해도 이름, 크기, 스프라이트와 트리거 항목이 정렬된 상태로 표시됐다. [Play를 중지](editor-stopped.png)하자 편집 씬에는 생성된 코인이 없고, 뚜껑과 이전 Player 선택이 복원됐다.

회귀 검사: 저장소 루트에서 `dotnet run --project engine/tests/Engine.Checks -c Release -- .`. 중복 상호작용, 접촉 전후 코인 개수, 원본 씬 불변, 인스턴스별 GUID와 내부 참조, 이동한 부모 아래 월드 좌표, 비활성 자식의 위치, 하위 트리 제거와 다른 인스턴스 보존을 검사한다. 전체 검사 통과. 이번 실제 창 검증 대상은 macOS arm64다.
