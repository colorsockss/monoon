# InventoryLab 빌드 CLI와 CI 검증

2026-11-14, Apple Silicon arm64, macOS 27.0.1 (26A434), .NET SDK 10.0.101, Xcode 27.0. 저장소 루트의 터미널에서 에디터를 열지 않고 실행했다.

| 명령 | 관측 결과 |
| --- | --- |
| `./monoon ci` | 소스에서 macOS Release 템플릿과 Native AOT 키트를 준비하고 두 앱을 빌드했다. 대화·퀘스트·가방 패키지 검사 27개를 통과했고, 내보낸 플레이어 두 개가 다음 세 단계의 저장을 생성했다. 종료 코드 0. [전체 로그](inventorylab.txt) |
| `./monoon test` | 전체 엔진 검사에서 PASS 370개와 **All checks passed.**를 확인했다. KeyRoom의 세 플랫폼용 Release 내보내기도 포함한다. 종료 코드 0. [전체 로그](engine.txt) |

| 플레이어 입력 | Release와 Native AOT에서 확인한 저장 |
| --- | --- |
| T → 1 → S | 약초 의뢰 제안 **offer**. 빈 가방과 미수락 상태. |
| 새 프로세스에서 L → 1로 수락 → 1로 대화 종료 → 1 세 번 → T → 2 → 1 → S | 지급 대사 **paid**. 약초 3개, 수정 1개, 수집량 3, 보상 수령 완료. |
| 새 프로세스에서 L → 1로 대화 종료 → T → 2 → 1 → S | 보상을 다시 요청해 **reward**에 머물렀다. 약초 3개·수정 1개와 수령 완료 상태를 유지했다. |

각 빌드의 실제 `MoNoonPlayer`를 세 번씩 시작해 컴파일된 게임 스크립트와 YAML을 읽었다. 입력은 플레이어의 창 없는 검사 모드에서 게임 입력 바인딩으로 전달했다. 각 저장의 필드를 예상값과 대조했고, 세 단계 모두 Release와 Native AOT의 저장 파일이 바이트 단위로 같았다. CI의 tar 명령으로 두 앱을 묶은 뒤 실행 권한과 숨김 매니페스트도 확인했다. [검사 코드](../../Engine.Checks/InventoryBuildChecks.cs)

공백이 있는 프로젝트·출력 경로와 저장소 밖에서 호출하는 경우를 확인했다. 잘못된 인자에 2, 스크립트 컴파일 오류와 손상된 빌드에 1이 반환됐다. 임시 프로젝트의 컴파일 오류에는 파일명과 줄이 포함됐고 직전 Release의 모든 파일 내용이 보존됐다. [실패 경로 로그](failures.txt)

이 기록은 로컬 Mac의 실행 결과다. GitHub Actions 워크플로의 YAML과 두 작업의 정의를 확인했다. 원격 러너에서의 실행 이력은 아직 없다. [빌드와 CI 사용법](../../../../docs/manual/build.md#터미널에서-빌드와-회귀-검사) · [당시 워크플로](https://github.com/colorsockss/monoon/blob/6232209b5ce4a2c7516db0c8fd989ba63a8e77c5/.github/workflows/build-regression.yml)
