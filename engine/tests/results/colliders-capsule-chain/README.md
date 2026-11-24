# 캡슐·체인 충돌체 (16.4)

2026-11-24, macOS 27.0.1 arm64(Apple Silicon)에서 Scene 편집, Scene Play, Release, Native AOT(모두 Distribution 프로필)로 확인했다. 기준 커밋 `fdc1eb3`(wip/next1). 측정 중 `qemu-system`은 실행되지 않았다.

Capsule Collider 2D와 Edge Collider 2D를 더했다. BRUTAL 우선 원칙에 따라 판정과 월드는 `Brutal.Box2D`의 캡슐·체인을 쓴다. 질의(겹침·레이·스윕), 이동, 접촉·트리거 이벤트, 공간 격자(8.8), Box2D 월드(강체·조인트) 모두 새 도형을 처리한다.

## 배포본 영향

- 새 의존성·네이티브 라이브러리는 없다. 이미 배포하는 `libbox2d`의 캡슐·체인·세그먼트 함수를 부른다. 두 컴포넌트는 `Collider2DComponent`를 상속하고 `[EngineFeature(Physics)]`를 직접 달아 15.11 기능 판정에서 Box2D를 포함시킨다.
- 크기: [아래 표](#배포본-크기). 엔진 IL은 수 KB 늘었다. PlatformerLab은 Slopes 씬과 스크립트 하나가 더해졌다.
- 프레임 비용: 박스·원·다각형의 경로는 그대로다(분기 하나 추가). 체인은 선분마다 Box2D 판정을 한 번 한다. 할당: 같은 장면을 박스 대신 캡슐·Edge로 만들어도 고정 틱 120회의 할당 바이트가 같다(474,240 B, 기존 단계·접촉 경로의 할당이며 새 도형이 더하는 것은 0).
- Box2D는 내보내지 않는 인라인 함수 `b2MakeCapsule`을 C#으로 옮겨(두 꼭짓점 둥근 다각형) Box2D의 다각형 충돌·거리·캐스트 함수에 넘긴다. 레이는 Box2D의 다각형 레이가 두 꼭짓점 도형을 무한 직선으로 다루므로 `RayCastCapsule`·`RayCastSegment`를 쓴다. 월드에서는 `CreateCapsuleShape`와 `CreateChain`(열린 체인은 양 끝 고스트 점을 끝 선분 방향으로 연장)을 쓴다.

## 바꾼 것

| 파일 | 내용 |
| --- | --- |
| `engine/src/Engine.Core/SceneComponent.cs` | `CapsuleCollider2DComponent`(Radius, 0 = 짧은 변의 절반, 그보다 크면 잘림), `EdgeCollider2DComponent`(Points 2–1024개 크기 단위, Loop). YAML·JSON 등록 |
| `engine/src/Engine.Core/Physics2D.cs` | `Shape2D.Capsule(a, b, radius)` 공개 팩터리, 내부 `Chain`(선분 부분 도형 `Parts`) |
| `engine/src/Engine.Core/CollisionGeometry.cs` | 캡슐 생성·검증, 체인의 분리·접점·캐스트(가장 이른/깊은 선분), 두 꼭짓점 도형의 레이, 프록시 반지름 |
| `engine/src/Engine.Core/ScenePhysicsWorld.cs` | Box2D 캡슐(캐릭터는 발바닥 2px 위까지)과 체인 생성 |
| `engine/src/Engine.Core/TileCollision.cs` | 단방향 타일 면 간격에서 캡슐 반지름을 뺀다 |
| `engine/src/Engine.Core/PlayDiagnostics.cs` | Play 진단에서 캡슐 윤곽·체인 선분 그리기 |
| `engine/src/Engine.Core/SceneDocument.cs` | 새 충돌체가 있는 씬은 스키마 33(이전 판독기가 거부) |
| `engine/src/Engine.Editor/Scene/SceneViewport.Colliders.cs` (새) | 캡슐 윤곽과 반지름 핸들(끌기, Undo 1회) |
| `engine/src/Engine.Editor/Scene/SceneViewport.Paths.cs`, `.Pointer.cs`, `.Gizmos.cs` | Edge 점을 경로 점 편집기로 편집(접선 없음, Loop는 최소 3점), 반지름 끌기 연결 |
| `engine/src/Engine.Editor/Inspector/MainWindow.Presentation.cs` | 캡슐 Radius, Edge Loop·점 행(Smooth 없음) 몇 줄. Edge가 있는 오브젝트의 Line 2D 점 행은 숨긴다 |
| `samples/PlatformerLab/Scenes/Slopes.scene`, `Scripts/SlopeRunner.cs`, `GameSettings` | 캡슐 캐릭터, 8px 타일 바닥, Edge 경사·언덕, Box2D 캡슐 Boulder, 캡슐 트리거 Marker |
| `engine/tests/Engine.Checks/ColliderShapeChecks.cs` (새), `Program.cs` | `--only colliders`, `physics` 묶음과 전체 실행에 등록 |
| `engine/tests/Engine.Checks/InspectorChecks.cs`, `ComponentStateChecks.cs`, `KeyRoom.InspectorComponents/StartupHook.cs` | "col" 검색 충돌체 5종, 스키마 33 샘플 허용 |
| `engine/tests/PlatformerLab.Colliders/` (새) | Slopes 왕복 하네스(Release·AOT·Scene Play), 에디터 기즈모·Inspector 프로브 |

## 검증

| 검사 | Scene 편집 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- | --- |
| Slopes 왕복: 타일 이음새·Edge 경사 걸림 0회, x 514.86까지 올라갔다 39.86으로 복귀, Boulder (710, 204.0069) | – | 통과 | 통과 | 통과 |
| 세 경로 기록 바이트 비교(450행, 21,372 B, SHA-256 `635f9029…`) | – | 같음 | 같음 | 같음 |
| 캡슐 반지름 핸들 11 → 7, Edge 점 끌기·추가·삭제, Undo 4회로 원래 씬 | 통과 | – | – | – |
| Capsule·Edge Inspector 값 열·레이블 열 정렬(1280·1600·1920px) | 통과 | – | – | – |
| 금지 파일(SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring) | – | – | 없음 | 없음 |
| `Engine.Checks --only colliders` | 헤드리스 엔진 검사, 아래 표 ||||

`--only colliders`가 확인하는 것:

- 질의: 캡슐 옆면·끝·둥근 끝 레이, 명시 반지름, 양면 Edge(위·아래), 경사 법선, 끝 너머 빈 곳, Loop 안은 겹침이 아님, 캡슐 스윕이 Edge에 닿음, 박스 → 캡슐 스윕, 캡슐끼리 겹침, 트리거 필터, 회전·반전 캡슐.
- 접촉: 박스가 Edge 위에서 캡슐 트리거를 지나고 들려 올라갈 때 `CE, CS, TE, TS, TX, CX`.
- 문서: 저장·복원, 기본값, 잘못된 점(1개, 2점 Loop, 연속 중복, NaN)과 음수 반지름 거부.
- Slopes 헤드리스 왕복을 두 번 실행해 기록이 같고 걸림 0회, 트리거 진입 2회.
- 할당: 박스와 캡슐·Edge 장면의 고정 틱 120회 할당이 같다.
- 공간 격자(8.8 방식, 캡슐·Edge·박스를 1/3씩 섞은 고정 시드 장면, 경로 96개, 예열 1회 후 6회 번갈아 측정):

| 충돌체 | 질의 | 후보/질의, 전체 → 격자 | p95 (µs), 전체 → 격자 | 적중 |
| ---: | --- | ---: | ---: | ---: |
| 1,000 | 겹침(캡슐) | 1,000 → 8.39 | 50.5 → 3.2 | 57/96 |
| 1,000 | 레이캐스트 | 1,000 → 8.01 | 50.6 → 4.5 | 48/96 |
| 1,000 | 스윕(캡슐) | 1,000 → 11.72 | 56.5 → 9.1 | 75/96 |
| 10,000 | 겹침(캡슐) | 10,000 → 8.48 | 228.4 → 4.2 | 66/96 |
| 10,000 | 레이캐스트 | 10,000 → 9.33 | 96.0 → 4.1 | 44/96 |
| 10,000 | 스윕(캡슐) | 10,000 → 12.84 | 99.5 → 7.5 | 82/96 |

격자와 전체 순회의 답은 대상·거리·점·법선과 순서까지 같았다(답 SHA-256 앞 16자리 1천 `2BD6C0255E124F12`, 1만 `6C5627549756C4F1`). 시간은 Engine.Checks(Release JIT) 프로세스 안의 측정이라 참고값이다.

기존 물리 기준값(Release·Native AOT, 이번 엔진으로 다시 실행): PlatformerLab.Joints `2d523cd0…`, PlatformerLab.TileCollision `f30ed686…`, PlatformerLab.Paths `e37de6c0…`가 [x64-determinism 기준](../x64-determinism/README.md)과 같고, CollisionLab.Spatial 1천·1만 개 해시(`32FA7AB7…`·`B5C817DB…`) 검증기가 두 빌드에서 통과했다. Scene Play는 같은 엔진 코드라 다시 돌리지 않았다.

전체 회귀: `./monoon test` 통과(PASS 814줄, "All checks passed.", 빌드 크기 게이트 포함). 처음 실행에서 `ComponentStateChecks`가 "샘플 씬은 스키마 32 미만"을 가정해 Slopes(33)에서 실패했고, 검사를 "샘플에 비활성 표식(32)이 없다"로 고친 뒤 다시 통과했다.

[편집 기록](osx-arm64/editor.txt) · [Slopes 기록](osx-arm64/slopes-trace.tsv) · [판정 결과](osx-arm64/slopes-result.json) · [Edge 편집 화면](osx-arm64/edge-edit.png) · [Edge Inspector](osx-arm64/inspector-edge-1280.png) · [Capsule Inspector](osx-arm64/inspector-capsule-1280.png)

## 배포본 크기

`./monoon test`의 빌드 크기 게이트(osx-arm64 Distribution, 허용 +1%)가 통과해 기준값은 바꾸지 않았다. 아래 차이는 기준 파일 대비이며, 기준 파일 기록 뒤 병합된 다른 작업(14.8·14.11 등)의 증가분도 섞여 있어 16.4만의 몫으로 분리하지 않았다.

| 샘플 | 빌드 | 앱 (B) | 기준 대비 | 실행 파일 (B) | 기준 대비 |
| --- | --- | ---: | ---: | ---: | ---: |
| KeyRoom | Release | 32,899,292 | +74,752 (+0.23%) | 124,712 | 0 |
| KeyRoom | Native AOT | 15,708,592 | +33,184 (+0.21%) | 8,570,800 | +33,184 |
| PlatformerLab | Release | 30,161,533 | +87,512 (+0.29%) | 124,712 | 0 |
| PlatformerLab | Native AOT | 12,954,759 | +57,848 (+0.45%) | 8,271,952 | +49,696 |
| InventoryLab | Release | 83,589,909 | +75,264 (+0.09%) | 124,712 | 0 |
| InventoryLab | Native AOT | 66,364,591 | +49,680 (+0.07%) | 8,271,840 | +49,680 |

PlatformerLab에는 Slopes 씬(약 4 KB)과 SlopeRunner 스크립트가 더해졌다. 첫 프레임·session_ready 시간은 부하에 따라 움직여 게이트에서 판정하지 않는다. 이번 실행은 다른 에이전트의 빌드와 겹쳐 기준보다 느렸고(KeyRoom Release 첫 프레임 161 ms, 기준 134 ms), 기준·후보를 번갈아 재지 않았으므로 시작 시간 변화는 주장하지 않는다.

## 재현

[하네스 README](../../PlatformerLab.Colliders/README.md)의 명령을 그대로 실행한다. 엔진 검사는 다음과 같다.

```sh
dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only colliders
```

## 결정과 한계

- 질의·이동·접촉은 Edge 선분을 양면으로 보지만 Box2D 월드는 체인을 한쪽 면으로만 막는다(Box2D 체인의 성질). 점이 왼쪽 → 오른쪽이면 위에서 막는다. 반전된 오브젝트는 점 순서를 뒤집어 같은 면을 유지한다. 캐릭터(Character Motor)는 질의로 움직이므로 양면에 막히고, 동적 강체는 아래에서 통과한다.
- Edge는 면적이 없다. Loop 안에 완전히 들어간 도형은 겹침이 아니다.
- 캡슐은 오브젝트 사각형의 긴 변을 따른다. 방향·오프셋을 따로 두지 않았다(자식 오브젝트로 회전·이동한다).
- 0.01px보다 짧은 선분은 버린다(Box2D가 법선을 만들 수 없다).
- 같은 오브젝트에 Line 2D와 Edge Collider 2D가 있으면 Scene 점 편집은 Edge에 붙는다.

## 확인하지 않은 것

- x64(Windows·Linux): coordinator 몫. Box2D 결과는 아키텍처마다 다를 수 있어(11.3 선례) 차이가 나면 기록만 한다. Windows는 장비 준비 후.
- 실제 마우스 끌기: 프로브가 Scene 뷰의 BeginDrag·DragTo·ReleaseAt을 직접 부른다.
- 동적 강체에 붙인 Edge(질량 0, 대체 질량 사용)는 권장하지 않으며 따로 검사하지 않았다.
