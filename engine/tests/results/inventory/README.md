# InventoryLab macOS 실행 검증

2026-11-14, macOS 27.0.1 (26A434), Apple Silicon arm64. MoNoon 에디터에서 샘플을 열고 Play를 눌렀다. Release와 Native AOT는 에디터의 두 빌드 명령으로 내보낸 뒤 각각의 앱 실행 파일을 시작했다. 각 모드는 별도의 임시 사용자 프로필을 사용했다.

| 입력 순서 | 세 실행에서 관찰한 결과 |
| --- | --- |
| **1** 네 번, **2** 두 번, **3** 한 번 | 약초가 두 묶음으로 나뉘었다. 수정으로 마지막 칸을 채운 다음 열쇠를 담으려 하자 가방이 가득 찼다는 메시지가 떴다. **S**로 저장한 세 칸은 약초 3 · 약초 1 · 수정 2였다. [저장 파일](full.save) |
| **E → U → 3 → S** | 가운데 칸의 약초를 사용한 자리에 열쇠를 담았다. 약초 3 · 열쇠 1 · 수정 2가 저장됐다. [저장 파일](reused.save) |
| **U**, 종료, 다시 실행, **L → S** | 사용 직후 가운데 칸이 비었다. 새 실행은 세 칸이 모두 비어 있었고 L로 불러오자 열쇠와 양옆의 묶음이 복원됐다. [재저장 파일](restored.save) |
| 마우스로 열쇠 담기 → 첫 칸 선택 → 사용 → 수정 담기 → 저장 | 세 창 모두 열쇠 사용 메시지를 표시했고 첫 칸에 수정 하나만 저장했다. Scene Play는 에디터 창을 880×660으로 줄인 상태에서 버튼을 클릭했다. [저장 파일](clicked.save) · [축소한 에디터](scene-play-compact.png) |

각 단계에서 UI를 캡처하고 플레이어 저장 파일을 읽어 슬롯 키·수량을 검사했다. 위 네 단계 모두 각 모드가 만든 YAML이 바이트 단위로 일치했다. [파일별 SHA-256](saves.csv) · [화면 인식 텍스트와 실제 저장값](observations.txt)

| 모드 | 가득 찬 가방 | 재실행 후 복원 |
| --- | --- | --- |
| Scene Play | [화면](scene-play-full.png) | [화면](scene-play-restored.png) |
| Release | [화면](release-full.png) | [화면](release-restored.png) |
| Native AOT | [화면](native-aot-full.png) | [화면](native-aot-restored.png) |

에디터의 패키지 설치 코드로 빈 프로젝트에 배포 패키지를 가져온 뒤 가방 프리팹을 배치하고 저장·재실행했다. 재가져오기 중복 방지, 원본과 배포본 일치, 여러 개를 한꺼번에 담을 때의 처리, 잘못된 저장 데이터로부터 기존 상태 보존 검사를 포함해 전체 회귀 검사 340개가 통과했다. [검사 로그](checks.txt) · [검사 코드](../../Engine.Checks/InventoryChecks.cs) · [재현 및 패키지 사용법](../../../../docs/manual/inventory.md)
