# AnimatorBeacon 검사 재료

[AnimatorChecks](../../Engine.Checks/AnimatorChecks.cs)가 쓰는 작은 컨트롤러다. Idle(`Rest.clip`, 1초, 이벤트 Rested)과 Pulse(`Pulse.clip`, 0.6초 Rise → 0.4초 Lit, 반복 안 함)를 Bool `Running`으로 오간다. 한 컨트롤러를 두 인스턴스가 따로 쓰는지, 프레임 이벤트의 정확한 경계와 건너뛴 프레임, 전이 순서를 본다. [AnimatorConditionChecks](../../Engine.Checks/AnimatorConditionChecks.cs)·[AnimatorDirectionChecks](../../Engine.Checks/AnimatorDirectionChecks.cs)는 조건·방향 키가 없는 옛 형식이 그대로 읽고 쓰이는지 본다.

원래 AnimatorLab의 `Animator.scene` 재료였고, 샘플을 쇼케이스로 바꾸면서(2026-11-26) 검사 재료로 옮겼다.
