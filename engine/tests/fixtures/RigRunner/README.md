# RigRunner 검사 자료

`Assets/Runner.rig`는 PlatformerLab 쇼케이스 시작 표지판 옆에서 제자리로 달리던 인물의 뼈대다. 2026-11-26 사용자 결정으로 쇼케이스에서 인물을 빼면서 샘플에서 이 폴더로 옮겼다(에셋 ID는 그대로).

- 쓰는 곳: [RigChecks](../../Engine.Checks/RigChecks.cs)가 폴더를 임시 프로젝트로 복사하고 Skeleton Animator 2D(Time 시계, 0.7초) 인물 하나를 둔 씬을 돌려 뒷다리 관절 연결·뼈 길이·흔들림과 잘못된 부모 순서 거부를 확인한다. [사용 매뉴얼](../../../../docs/manual/rig-animation.md)의 예제 뼈대이기도 하다.
