# KeyRoom 런타임 컴포넌트 검증

2026-11-15, macOS 27.0.1 arm64. 같은 KeyRoom 프로젝트를 MoNoon 에디터의 Scene Play와 두 배포용 앱에서 실행했다. 실행마다 별도 저장 폴더를 사용했다.

| 실행 대상 | 열쇠 획득 전 | 열쇠 획득 후 | 문 통과 |
| --- | --- | --- | --- |
| Scene Play | 오른쪽으로 걸으면 닫힌 문에 멈췄다. Inspector에 `Door.png`와 `Solid`가 표시됐다. [닫힌 문](editor-locked.png) | 같은 `Door`가 `Active`를 유지했고 Inspector의 Runtime에 `DoorOpen3.png`와 `Collider disabled`가 표시됐다. [열린 문](editor-open.png) | 오른쪽으로 걸어 두 번째 방에 진입하고 F5로 저장했다. [두 번째 방](editor-second.png) |
| Release | 첫 방에서 문에 막혔고 저장 슬롯에서 열쇠 값이 `false`였다. | 왼쪽의 열쇠를 얻자 문이 열리고 열쇠 값이 `true`가 됐다. | 두 번째 방에 도착했다. [Release 화면](release-second.png) |
| Native AOT | 첫 방에서 문에 막혔고 저장 슬롯에서 열쇠 값이 `false`였다. | 같은 방향키 입력으로 열쇠를 얻고 문이 열렸다. | 두 번째 방에 도착했다. [Native AOT 화면](aot-second.png) |

Scene Play를 Stop하면 첫 방의 편집 씬에 닫힌 문이 다시 보인다. 다시 Play해 오른쪽으로 걸었을 때도 닫힌 문에 막혔다. 재시작 전후의 닫힌 문 캡처가 바이트 단위로 일치했고, `Scenes/KeyRoom.scene`은 실행 전후 모두 SHA-256 `cb5cbb5a7ff10cb83566722292e1ca46129a5f873039cc342e542f6771ea07c2`였다. [Stop 직후 편집 씬](editor-stopped.png)

두 배포본은 `osx-arm64`의 `distribution` 프로필로 빌드했다. Release 빌드 ID는 `5b53f61bfcb142b39b5e4ff4d59c94b7`, Native AOT 빌드 ID는 `32f073c27402491c92f5251bc0b2ab03`이다. 두 앱에서 마지막으로 F5를 눌러 만든 슬롯은 모두 두 번째 방 `97ae2562-6e93-5e7a-be3b-45ec603896c6`, `key: true`였고, 파일의 SHA-256도 `00d5309db260658bb54a80d78c4e2b4e7d05e473550e69d70a84e6acc9833805`로 일치했다.

`Engine.Checks` 전체 회귀도 통과했다. KeyRoom 검사에서는 닫힌 문이 플레이어를 X=440에서 멈추는지, 열린 문이 겹침·레이캐스트·스윕 결과에서 사라지는지, 충돌체를 다시 켜면 레이캐스트가 즉시 문을 감지하는지 확인했다. 문 열림 클립이 끝난 뒤 그려진 PNG, 런타임 색상 변경, 체크포인트 복원도 검사했다. [이동·렌더링 검사](../../Engine.Checks/Program.cs) · [저장 복원 검사](../../Engine.Checks/PlayerDataChecks.cs)
