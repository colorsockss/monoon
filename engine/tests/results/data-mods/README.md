# InventoryLab 달맞이 장터 모드

2026-11-14, macOS 27.0.1 arm64. Release와 Native AOT 앱 각각에서 기본값이 `off`임을 확인하고 `--mods on moon-market`으로 활성화했다. SDL 창에서 **T**로 약초상에게 말을 걸고, 의뢰를 수락한 뒤 약초 세 포기를 모아 보상을 받고 저장했다. 같은 빌드·프로필에서 `--mods off moon-market`으로 끄고 게임을 다시 시작한 뒤 같은 순서로 새 의뢰를 완료했다.

| 모드 | 약초상의 첫 대사 | 새 의뢰 보상 | 실제 SDL 창 |
| --- | --- | --- | --- |
| 켬 | 달맞이 장터에 오신 걸 환영해요! | 수정 2개 | [Release](release-on.png) · [Native AOT](native-aot-on.png) |
| 끔 | 어서 오세요. 무슨 일로 오셨나요? | 수정 1개 | [Release](release-off.png) · [Native AOT](native-aot-off.png) |

각 상태에서 SDL 창으로 만든 저장과 475프레임·31이벤트의 헤드리스 재생으로 만든 저장이 바이트 단위로 일치했다. 두 빌드 모두 약초 수집량 3, 보상 수령 완료, 대화 노드 `paid`였으며 수정 수량만 달랐다. 켰을 때 저장의 SHA-256은 `d2495f4e1cfc780b6b870517e568043b362bdf3b51ff3693c84dfd085829531a`, 껐을 때는 기존 Mac 기준인 `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795`였다. [모드 저장](moon-market.save) · [기본 저장](../../fixtures/InventoryLab/Herbalist.save) · [공통 입력](../../fixtures/InventoryLab/Herbalist.inputreplay)

모드를 끈 직후 기존 슬롯의 수정 2개는 유지됐다. 전환 전후에 실행 파일·번들·모드 파일·배포 매니페스트의 해시가 같았다. 엔진 전체 회귀 검사 373개가 통과했다. [모드 사용법](../../../../docs/manual/build.md#데이터-모드) · [충돌과 원본 복원 회귀 검사](../../Engine.Checks/GameModChecks.cs)
