# 시간 제어 검증

2026-11-13, macOS arm64. 패키징한 MoNoon 에디터에서 KeyRoom 복사본을 열었다. 게임 빌드는 샘플 원본에서 내보낸 macOS Release와 Native AOT를 각각 실행했다.

| 동작 | 확인한 결과 |
| --- | --- |
| 에디터에서 P로 정지하고 오른쪽 방향키를 누른 뒤 H로 안내 열기 | Player X는 274에 머물렀고 정지 시간과 조작 안내가 표시됐다. [정지](paused.png) · [정지 중 안내](paused-help.png) |
| 정지 상태에서 4×를 선택하고 오른쪽 방향키를 누른 채 Step 한 번 클릭 | X가 278이 됐다. 이후에도 정지 상태와 X가 유지됐고 Controls를 클릭해 안내를 닫았다. [한 틱 후](step-at-speed.png) · [같은 위치에서 안내 닫기](paused-click.png) |
| Resume로 재개하고 창을 900×525로 축소 | 4×로 이동했고 Pause·Step·배속·Stop 버튼과 Inspector가 한 줄에 맞게 표시됐다. [재개](resumed.png) · [작은 창](compact.png) |
| Release 독립 창 | P로 멈춘 뒤 방향키와 E를 입력해도 플레이어와 상자는 그대로였고, Controls를 클릭하면 안내가 열렸다. 재개 후 E로 상자를 열고 이동해 코인을 획득했다. [정지](release-pause.png) · [UI 입력](release-help.png) · [재개 후 획득](release-coin.png) |
| Native AOT 독립 창 | 같은 입력 순서로 정지 표시 0초→3초, 안내 열기, 재개 후 COIN 1을 확인했다. [정지](aot-pause.png) · [UI 입력](aot-help.png) · [재개 후 획득](aot-coin.png) |

엔진 검사에서는 0.5×와 4×의 틱 수, 짧은 입력과 정지 중 입력 처리, UI 타이머 반복·취소, 정지 중 씬 전환을 확인했다. 열쇠를 획득한 직후 정지하면 문 애니메이션과 카메라가 멈췄고, 여섯 틱을 진행하면 문 프레임이 바뀌었다. 2×로 재개한 뒤 열쇠 획득 후 누적 60번째 게임 틱에서 플레이어 카메라가 복귀했다. [검사 코드](../../Engine.Checks/TimeChecks.cs) · [실행 로그](checks.txt)

저장소 루트에서 `dotnet run --project engine/tests/Engine.Checks -c Release -- .`로 엔진 검사를 재현할 수 있다. 독립 창의 실행 검증 범위는 macOS다.
