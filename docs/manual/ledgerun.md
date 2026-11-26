# LedgeRun

**File → Open Project…**에서 [LedgeRun](../../samples/LedgeRun/) 폴더를 열고 **Trailhead** 씬에서 Play를 누른다. **Enter**로 초원에 출발한다. 깃발에 닿아 스테이지를 끝내고 **Enter**를 누르면 다음 스테이지가 열린다. 정상에서는 수호자를 위에서 세 번 밟으면 마지막 깃발이 나온다.

| 동작 | 키보드 | 게임패드 |
| --- | --- | --- |
| 이동, 스테이지 선택 | 좌우 방향키 또는 A·D | D-pad 또는 왼쪽 스틱 |
| 점프, 벽 점프 | Space | South |
| 대시 | X | West |
| 출발, 다음 스테이지 | Enter | Start |
| 일시정지 | P | Back |
| 출발 메뉴로 돌아가기 | Backspace | East |
| 메뉴에서 저장 슬롯 바꾸기 | Tab | North |

점프를 길게 누르면 더 높이 오른다. 벽 쪽으로 이동키를 누르고 있으면 내려오는 속도가 줄고, 그 상태에서 점프하면 반대쪽으로 튄다. 대시로 발판을 벗어나면 착지 후 다시 대시할 수 있다. 낙하하거나 하트가 모두 줄면 마지막 체크포인트에서 돌아온다. 스테이지를 끝낼 때 선택한 슬롯에 해금 범위, 수집 최고 기록, 최단 시간이 저장된다. 정상에서는 체력과 최단 시간을 표시한다. 슬롯이 손상되어 백업에서도 복구할 수 없으면 Trailhead에서 다른 슬롯을 선택해 플레이할 수 있다.

## 에디터에서 찾아보기

- **Meadow → Trail terrain**에서 16px 직교 타일과 통과할 수 있는 발판을 편집한다. 스프라이트의 PNG를 가리키는 [Trail.tileset](../../samples/LedgeRun/Assets/Trail.tileset)에서 충돌 모양을 확인할 수 있다.
- **Courier**는 Capsule Collider와 Character Motor를 쓴다. [TrailRunner](../../samples/LedgeRun/Scripts/TrailRunner.cs)의 스크립트가 점프 버퍼, 벽 판정과 대시를 처리한다. 하위 **Courier sprite**의 자세와 방향을 바꿔도 캡슐의 크기는 같게 유지된다.
- **Cave**의 Global Light·Point Light는 주변광과 횃불 그림자를 만든다. **Tower → Trail terrain**은 Courier 기준 반경 1청크를 스트리밍한다.
- [Trailhead](../../samples/LedgeRun/Scenes/Trailhead.scene)가 빌드의 시작 씬이다. [TrailProfile](../../samples/LedgeRun/Scripts/TrailProfile.cs)은 세 슬롯의 스테이지 기록을 저장한다.

[제작 계획](../plans/samples/1-platformer.md) · [구현 및 검증 기록](../../engine/tests/results/ledgerun/README.md)
