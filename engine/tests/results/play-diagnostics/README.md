# KeyRoom 실행 진단 검증

2026-11-14 · macOS 27.0.1 arm64 · 패키징한 MoNoon.app의 SDL Scene Play. KeyRoom 임시 복사본에서 실제 창에 키보드와 마우스를 입력했다.

| 조작 | 관찰 결과 |
| --- | --- |
| Play → Debug에서 Colliders, Queries, Profiler 활성화 | Player와 벽은 초록, Key·Coin 트리거는 분홍으로 그려졌다. 선택한 도형은 더 두꺼웠다. |
| 시작 위치에서 Player 선택 → Pause → Cast ray… → 닫힌 문 너머 클릭 | [레이가 Door에 186유닛 거리에서 적중](ray-door.png)했다. 접점과 법선이 남았고 Pause 중에도 렌더 시간과 드로우콜이 갱신됐다. |
| E로 상자 열기 → Hierarchy에서 Coin 선택 | [Runtime Inspector](live-coin.png)에 `Active · Coin.prefab`, 월드 위치 (384, 126), Trigger / Layer 0, 그리고 CollectCoin의 `available: True`, `collected: False`가 표시됐다. |
| Player에서 같은 방향으로 다시 레이 발사 | 이번에는 [앞에 생긴 Coin 트리거가 90유닛 거리에서 먼저 적중](ray-paused.png)했다. |
| 콘솔 검색어를 `Coin collected`로 둔 채 오른쪽 방향키로 코인 획득 | [일치하는 로그](console-filter.png)가 0개에서 1개로 늘었다. 필터를 지우면 [Play 시작·레이 두 번·획득 로그](console.png)가 보였고, Clear 후에는 보관된 기록도 0개였다. |
| 오른쪽 방향키로 닫힌 문까지 이동 | [Movement · 1 hit · Door](movement-hit.png)가 표시됐다. 이때 Render 0.15 ms, SDL draws 9가 측정됐다. 표시된 수치는 이 실행에서 관찰한 값이다. |
| Stop → 프레임 기록과 씬 확인 | [마지막 180프레임 기록](post-stop.png)이 남았다. 해당 구간의 평균 프레임 간격은 17.0 ms였고, Runtime Inspector는 닫혔다. 실행한 복사본의 씬 파일 SHA-256이 원본과 일치했다. |
| 860 × 620으로 축소해 다시 Play | [작은 창 화면](compact.png)에서 진단 버튼·그래프·Inspector의 열이 겹치지 않았다. 프레임당 할당량과 세대별 GC 횟수도 계속 갱신됐다. |

엔진 회귀 검사 208개를 모두 통과했다. 아래 검사 결과에는 이번 진단 기능에 해당하는 항목을 발췌했다.

[코어 회귀 검사](../../Engine.Checks/DiagnosticsChecks.cs) · [검사 결과](checks.txt) · [사용 설명서](../../../../docs/manual/play-diagnostics.md)
