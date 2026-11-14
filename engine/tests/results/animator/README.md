# AnimatorLab · macOS arm64

[AnimatorLab](../../../../samples/AnimatorLab)에서 Running을 켜고 끄면 `Idle → Pulse → Idle`로 전환된다. 시작 시 청록색 마름모, Pulse 진입 시 주황색 화살표, 0.6초 후 노란 원, Idle로 돌아오면 다시 마름모가 나온다. 미리보기에서는 Running을 체크·해제했고 Scene Play와 독립 게임에서는 오른쪽·왼쪽 방향키를 눌렀다.

| 실제 실행 창 | ① Rested | ② Rise | ③ Lit | ④ Rested |
| --- | --- | --- | --- | --- |
| 컨트롤러 미리보기 | [Idle](preview-idle.png) | [Pulse 1](preview-rise.png) | [Pulse 2](preview-lit.png) | [이벤트 기록](preview-return.png) |
| Scene Play | [Idle](editor-idle.png) | [Pulse 1](editor-rise.png) | [Pulse 2](editor-lit.png) | [복귀](editor-return.png) |
| macOS Release | [Idle](release-idle.png) | [Pulse 1](release-rise.png) | [Pulse 2](release-lit.png) | [복귀](release-return.png) |
| macOS Native AOT | [Idle](aot-idle.png) | [Pulse 1](aot-rise.png) | [Pulse 2](aot-lit.png) | [복귀](aot-return.png) |

게임의 HUD는 스크립트가 실제로 받은 이벤트 이름·프레임·누적 횟수를 표시한다. 컨트롤러 미리보기와 세 게임 창의 네 단계에서 스프라이트 중심의 sRGB 값이 각각 일치했다. 두 배포본에서 컨트롤러·클립과 참조 PNG도 원본 파일과 일치했다. [측정 결과](pixels.txt)

샘플 복사본에서 Pulse.clip 두 번째 프레임의 이벤트를 Lit에서 Shine으로 수정해 저장했다. 클립을 닫았다가 다시 열어 같은 이벤트와 프레임을 확인했고, 다시 실행한 게임도 세 번째 이벤트로 Shine을 표시했다. [재열기](clip-reopened.png) · [수정 후 Play](edited-event-play.png) · [396×376 컨트롤러 창](preview-narrow.png)

엔진 검사는 오브젝트별 파라미터 분리, 하나의 틱에서 여러 조건이 맞을 때의 순서, Trigger 소비, Float 경계값, 여러 루프를 지난 경우와 반복하지 않는 마지막 프레임의 이벤트, 콜백 안에서 재시작하는 경우를 다룬다. 기존 KeyRoom의 단일 클립 재생과 일반 회귀 검사도 통과했다. [검사 코드](../../Engine.Checks/AnimatorChecks.cs) · [실행 로그](checks.txt)
