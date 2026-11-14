# 일시정지, 한 틱 진행과 타이머

**Play → Pause**를 누르면 현재 게임 상태를 Inspector에서 살펴볼 수 있다. **Step**은 게임을 정확히 1/60초 진행하고 다시 멈춘다. **Resume**은 선택한 배속으로 계속 진행한다. 배속 메뉴에는 0.25×, 0.5×, 1×, 2×, 4×, 8×가 있다. 배속은 실제 1초 동안 실행할 고정 틱의 수를 바꾼다.

## KeyRoom에서 따라 하기

1. Hierarchy에서 Player를 선택하고 Play를 시작한다. **P**를 눌러 정지한다. 게임 오른쪽 위에 정지 시간이 표시된다. **H** 또는 **Controls** 버튼으로 안내를 열고 닫을 수 있다.
2. 에디터에서 4×를 고른다. 시작 위치에서 오른쪽 방향키를 누른 채 **Step**을 한 번 클릭한다. 기본 속도 240인 Player의 X가 274에서 278로 바뀐다. 키를 계속 누르고 있어도 다음 틱은 다음 Step 또는 Resume를 기다린다.
3. **Resume** 또는 **P**로 재개한다. 독립 게임에서도 P로 정지·재개할 수 있다. 이 키는 프로젝트의 **PauseGame** 액션에서 변경한다. 패드의 기본 바인딩은 Back이다.

## 스크립트에서 시간 선택하기

| 할 일 | API와 동작 |
| --- | --- |
| 이동·충돌·애니메이션 진행 | `FixedUpdate(ScriptTick tick)`에서 `tick.DeltaTime`으로 이동량을 계산한다. 게임 초당 60틱이며, 정지 중에는 Step을 누른 만큼 실행된다. |
| 메뉴·정지 화면 갱신 | `UpdateUi(UiTick tick)`는 활성 창의 프레임마다 호출된다. `tick.DeltaTime`은 해당 프레임의 실제 경과 시간이다. UI 입력은 게임 정지·배속 중에도 동작한다. |
| 경과 시간 확인 | `tick.Session.Time`에서 `GameSeconds`, `UiSeconds`, `TickCount`를 읽는다. 이 값은 씬 전환 동안 이어지고 새 Play에서는 0부터 시작한다. |
| 시간 제어 | `time.IsPaused`를 설정하고, 정지 상태에서 `time.StepOnce()`를 호출한다. `time.Scale`에는 0보다 크고 8 이하인 유한한 값을 지정한다. 기본값은 1이다. |

월드와 UI는 입력을 각각 읽는다. 느린 배속에서 틱 사이에 키를 짧게 눌렀다 놓아도 다음 게임 틱이 입력을 받는다. 정지 중 누르고 놓은 게임 액션은 재개 시 실행되지 않는다. Step에서는 현재 누르고 있는 방향키가 적용된다.

## 타이머 사용하기

`StartTimer`로 타이머를 한 번 만들고 업데이트에서 `Consume()`으로 만료를 확인한다. 일회용 타이머는 만료됐을 때 한 번만 참을 반환한다. `Remaining`은 남은 초, `IsRunning`은 소비 또는 취소 전 상태이며 `Cancel()`로 취소할 수 있다.

```csharp
// 열쇠 획득 시 게임 시간 1초 뒤에 카메라를 되돌린다.
returnToPlayer = contact.Session.Time.StartTimer(1);

// FixedUpdate에서 확인한다.
if (returnToPlayer?.Consume() == true)
    tick.Cameras.TrySetActive(PlayerCamera);
```

`tick.Time.StartTimer(1, TimeDomain.Ui, repeat: true)`는 UI 시간 1초마다 만료된다. 여러 주기가 지나서 확인하면 `Consume()`은 한 번만 참을 반환한 뒤 다음 주기를 기다린다. KeyRoom은 게임 타이머로 문 카메라를 복귀시키고, UI 타이머로 정지 시간을 표시한다. [문 카메라](../../samples/KeyRoom/Scripts/KeySequence.cs) · [정지 화면](../../samples/KeyRoom/Scripts/PauseGame.cs)

플레이 창이 비활성화된 동안에는 시간을 진행하지 않는다. 활성 창에서 긴 프레임이 발생하면 UI에는 경과 시간을 모두 반영하고, 월드는 프레임당 최대 0.1초에 해당하는 시간에 배속을 곱해 따라간다. 재생 중인 효과음은 자체 재생 시간을 따른다.
