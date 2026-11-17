# KeyRoom 입력 모드 검증

2026-11-16, macOS 27.0.1 arm64에서 MoNoon의 Scene Play와 KeyRoom의 Release·Native AOT 배포 앱을 각각 열었다. 실행마다 새 저장 폴더를 사용하고 첫 방에 실제 키보드·마우스 이벤트를 보냈다. 아래 화면은 창 크기를 바꿔 같은 조작을 반복했을 때 캡처했다.

| 실행 | 확인창 | 장착 후 인벤토리 | 게임 복귀 |
| --- | --- | --- | --- |
| Scene Play | [나침반 확인](editor-confirm.png) | [Equipped](editor-equipped.png) | [출구 방향 HUD](editor-game-equipped.png) |
| Release | [나침반 확인](release-confirm.png) | [Equipped](release-equipped.png) | [출구 방향 HUD](release-game-equipped.png) |
| Native AOT | [나침반 확인](aot-confirm.png) | [Equipped](aot-equipped.png) | [출구 방향 HUD](aot-game-equipped.png) |

## 재현 입력과 관찰 결과

1. **I**로 인벤토리를 열고 **D**, **E**, **F5**를 눌렀다. 플레이어와 상자의 상대 위치가 유지됐고 저장 슬롯도 생성되지 않았다.
2. **Enter**로 확인창을 열어 뒤쪽 **Controls** 버튼을 클릭하고 **P**, **D**, **E**를 눌렀다. 안내창과 일시정지가 열리지 않았다. **Tab → Enter**로 취소하자 나침반은 **In bag**이었고 **Manage compass**에 포커스가 돌아왔다.
3. **Enter**로 확인창을 다시 열고 **Apply**를 클릭했다. 인벤토리에 **Equipped**가 표시됐다. **Backspace**로 닫자 HUD에 **Compass / Exit east**가 나타났다. 이때 처음 누른 **F5**가 슬롯을 만들었고 그 안의 상자 상태는 `chest: false`였다.
4. **Bag [I]** 버튼을 클릭해 다시 열었다. 확인창에 **Stow the pocket compass?**가 표시됐고, **Backspace**를 누르면 장착한 채 인벤토리로 돌아왔다. 다시 확인창에서 **Apply**를 누르자 **In bag**으로 바뀌었다.
5. **I**로 인벤토리를 닫고 **E → F5**를 누르자 저장 값이 `chest: true`로 바뀌었다. 오른쪽 방향키를 눌렀을 때 카메라를 따라 배경의 상자 위치도 이동했다.

세 창 모두 기본 크기와 변경한 크기에서 이 순서를 통과했다. 에디터 창은 1220×768에서 1090×724로, 배포 앱 창은 너비 800에서 706×518로 바꿨다. 확인창의 **Apply**와 게임의 **Bag [I]** 모두 표시된 위치를 클릭했다.

| macOS arm64 빌드 | 프로필 | 검증한 빌드 ID |
| --- | --- | --- |
| Release | distribution | `5a4793d3b7c34b53a22c7a4e11ec29bd` |
| Native AOT | distribution | `46544c04000548b9aa0142fa3ae815d1` |

자동 검사는 입력 라우터에 게임패드 이벤트를 보내 탐색·확정·뒤로 가기를 확인한다. 같은 키를 누른 채 모드를 바꾸는 경우, 대기 중인 클릭 두 개, 이전 포커스 복원, 열린 패널 숨김·제거·비활성화도 검사한다. [회귀 검사 코드](../../Engine.Checks/InputModeChecks.cs) · [제작 방법과 조작법](../../../../docs/manual/input-actions.md#입력-모드-전환과-복귀)
