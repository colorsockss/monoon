# IsoBand

**File → Open Project…**에서 [IsoBand](../../samples/IsoBand/) 프로젝트를 열고 **Haven**에서 Play를 누른다. 대장에게 두 동료가 따라온다. 청록색 문 근처에서 **Enter**를 눌러 첫 필드로 들어간다. 구역의 적을 모두 쓰러뜨리고 다음 문으로 가면 습지 → 유적 → 지하 묘지로 진행한다.

| 동작 | 입력 |
| --- | --- |
| 대장 이동 | WASD·방향키·왼쪽 스틱 또는 지면 클릭 |
| 용병단 기술 Pulse | Space 또는 패드 South |
| 문 이용 | Enter 또는 패드 Start |
| 완료 후 새 원정 시작 | Haven에서 N 또는 패드 Back |
| 안전한 야영지에서 회복 | R 또는 패드 East |
| 마을의 불가에서 고용 | H 또는 패드 West, 6 크라운 |
| 마을의 불가에서 장비 강화 | U 또는 패드 North, 8 크라운 |

사거리 안 적은 자동으로 공격한다. 경험치가 쌓여 레벨이 올라가면 체력이 회복되고, 레벨 3부터 대장을 포함해 여섯 명까지 고용할 수 있다. 야영 중에는 주위에 적이 없어야 회복된다. 스테이지를 끝낼 때와 마을에서 고용·강화할 때 슬롯 1에 진행을 저장한다. 지하 묘지를 끝내고 문으로 돌아오면 Haven에 완료 안내가 뜬다. 새 원정을 선택하면 경험치, 크라운, 동료, 장비를 가진 채 습지에서 다시 출발한다.

## 에디터에서 찾아보기

- **Diamond terrain**은 64 × 32 Isometric 레이아웃이다. 물 타일은 충돌과 통행 비용 0을 지정했다. 타일 정보를 바탕으로 클릭한 칸 중심을 찾고 자유 이동 길찾기로 간다.
- **Captain**, **Companion**, 나무·기둥은 발 위치 기준 Y 정렬을 쓴다. [Expedition](../../samples/IsoBand/Scripts/Expedition.cs)이 정적 지형을 굽고 동료를 `NavCrowd2D`로 대장 주변에 배치한다.
- [Marsh.scene](../../samples/IsoBand/Scenes/Marsh.scene)에서 물과 마른 땅의 경계를 확인한다. [프로젝트 검사](../../samples/IsoBand/Tests/ExpeditionTests.cs)는 등각 물 타일의 굽기와 클릭·자동 전투를 확인한다.

[제작 계획](../plans/samples/2-iso-rpg.md) · [구현 및 검증 기록](../../engine/tests/results/isoband/README.md)
