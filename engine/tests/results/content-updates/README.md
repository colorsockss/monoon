# InventoryLab 수확제 콘텐츠 업데이트 검증

2026-11-14, macOS 27.0.1 arm64. 업데이트를 읽는 콘텐츠 버전 34의 배포용 Release와 Native AOT를 각각 한 번 내보냈다. 이후 Release의 기본 매니페스트와 교체할 YAML 두 개만으로 1,433바이트의 `Harvest.bundle`을 생성했다. 두 플레이어에 같은 파일을 적용했다. [수확제 원본](../../../../samples/ContentUpdates/InventoryLab/Harvest/Assets) · [번들 제작·적용 명령](../../../../docs/manual/build.md#콘텐츠-업데이트)

| 같은 앱으로 순서대로 실행한 조건 | Release SDL 창 | Native AOT SDL 창 |
| --- | --- | --- |
| 최초 설치에서 변조된 번들을 발행 해시로 적용 | 기본 인사, 수정 1개 | 기본 인사, 수정 1개 |
| 수확제 정상 적용 | 수확제 인사, 열쇠 1개 | 수확제 인사, 열쇠 1개 |
| 후속 번들의 보상 수량이 0이거나 문서 해시 불일치 | 수확제 인사, 열쇠 1개 유지 | 수확제 인사, 열쇠 1개 유지 |
| 프로필의 활성 번들을 잘라낸 뒤 재실행 | 기본 인사, 수정 1개로 복귀 | 기본 인사, 수정 1개로 복귀 |

각 경우 새 SDL 창에서 T로 대화한 뒤 의뢰 수락 → 약초 세 포기 수집 → 보상 수령 → S로 저장을 진행했다. 화면에서 인사와 수령 대사를 확인하고 같은 상태에서 475프레임·31이벤트의 헤드리스 입력도 재생했다. 여덟 번 모두 창에서 저장한 슬롯과 리플레이 결과가 바이트 단위로 일치했다. `herbalist.collected`는 3, `claimed`는 true, 대화 노드는 `paid`였다. 각 적용 명령은 실행 전에 있던 슬롯의 바이트를 보존했다. [Release 수확제 인사](release-harvest.png) · [Native AOT 후속 업데이트 거부 뒤 열쇠 지급](native-aot-last-good.png) · [Native AOT 손상 복구 뒤 수정 지급](native-aot-fallback.png)

보상 수량 0은 문서 형식과 전송 해시가 정상이었지만 두 앱 모두 시작 씬을 여는 중 `QuestLog.OnCreate`에서 거부했다. 대사와 보상이 함께 유지됐다. 손상된 다운로드와 문서 해시 오류도 각각 종료 코드 1이었다. 활성 번들이 잘린 경우 두 앱 모두 `using shipped content`를 기록하고 게임을 정상 종료했다. 엔진 검사에서는 게임 ID·기준 매니페스트 불일치, 모드 우선순위, 실행 중 게임의 콘텐츠 유지 및 원본 복원도 검증했다. 엔진 전체 회귀 검사 374개가 통과했다. [회귀 검사](../../Engine.Checks/ContentUpdateChecks.cs)

| SHA-256 검증 대상 | 값 |
| --- | --- |
| 배포한 수확제 번들 | `8288d051cc082460f3d4f20b8a4b53224ce5f77e8df00db4346cd8059409c15d` |
| [기본 저장](../../fixtures/InventoryLab/Herbalist.save) | `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795` |
| [수확제 저장](Harvest.save) | `5b86c868a44991febaa06aa961844c17cd18cced30c6fb189fd21132f537dddd` |

번들 제작 직전과 모든 플레이 완료 후 Release 앱 내부 266개, Native AOT 앱 내부 22개 파일의 경로와 SHA-256이 전부 일치했다. 실행 검증용 프로필과 별도로 이 Mac의 기본 InventoryLab 프로필에 정상 번들을 적용했고, 두 앱이 이를 읽어 임시 슬롯에 수확제 저장을 만드는 것도 확인했다.
