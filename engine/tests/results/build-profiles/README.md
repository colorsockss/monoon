# InventoryLab 빌드 프로필과 심볼 검증

2026-11-14에 macOS 27.0.1 (26A434), Apple Silicon arm64, .NET SDK 10.0.101에서 실행했다. CLI로 두 프로필의 Release와 Native AOT를 각각 빌드했다.

| 프로필 | 형식 | 빌드 ID | 심볼 검사 |
| --- | --- | --- | --- |
| 배포용 | Release | `0dbc391b3233480480fff5f1cc3fb652` | 앱 안에 심볼 0개. 보관한 PDB 4개의 ID와 스탬프가 각각 플레이어, Engine.Core, Engine.Desktop, 게임 스크립트 DLL과 일치했다. |
| 배포용 | Native AOT | `bbb7b599b6a54423bd4413e75b7687ef` | 앱 안에 심볼 0개. 별도로 보관한 dSYM과 실행 파일의 UUID가 `3D625E3A-E2BD-3005-AC01-DFA1E38D74C6`로 일치했다. 컴파일 과정에서 나온 관리 코드 PDB 3개도 보관했다. |
| 개발용 | Release | `cca0f1aae97044db89cffbe0e4651bc6` | 앱 내부의 PDB 4개와 대응 DLL이 일치했다. |
| 개발용 | Native AOT | `74ac9f85ae404f9588ac9ad4c2295e79` | 앱 내부에 PDB 3개와 dSYM이 있었다. 실행 파일과 dSYM의 UUID는 `74777FF4-9B5F-36D5-B141-84EF94D959AA`로 일치했다. |

두 배포용 앱을 일반 실행 모드로 열었다. 각 SDL 게임 창에 **T → 1 → S**를 입력하자 약초상이 약초 세 포기를 구해 달라는 대사를 표시했다. 양쪽 모두 화면에 저장 완료가 표시됐고, 각각의 독립 저장 슬롯에 대화 노드 `offer`가 기록됐다. [Release 화면](release.png) · [Native AOT 화면](native-aot.png) · [창 입력과 저장 기록](windows.txt)

이 Mac에서 CI와 같은 tar 명령으로 배포 앱과 심볼을 따로 묶어 두 압축 파일을 열어 확인했다. 앱 묶음에는 두 플레이어의 실행 권한이 보존됐고 심볼은 0개였다. 심볼 묶음에서는 두 빌드 기록과 해당 PDB·dSYM을 찾았다. [워크플로](../../../../.github/workflows/build-regression.yml)

배포용 Native AOT 실행 파일에서 `InventoryWorkbench.FixedUpdate`의 명령어 주소를 구해 보관된 dSYM으로 `atos`를 실행했다. `0x1008cc0ec`가 `InventoryWorkbench.cs:40`으로 해석됐다. 개발용에서도 같은 소스 줄을 얻었다. Release PDB에는 해당 스크립트의 소스 줄 매핑이 있었다. 임시 복사본에서 다른 DLL의 PDB로 바꾸거나, 배포 앱 안에 PDB를 넣거나, 빌드 기록을 지우면 각각 검사가 종료 코드 1로 실패했다. [심볼 및 실패 경로 기록](symbols.txt)

`./monoon test inventorylab`의 프로필 지정 검사에서 개발용과 배포용이 각각 43개 항목을 통과했다. 의뢰 제안, 보상 수령, 재실행 후 보상 중복 방지의 세 단계에서 각 프로필의 Release와 Native AOT가 바이트 단위로 같은 저장 파일을 만들었다. 엔진 전체 회귀 검사 `./monoon test`에서도 370개 항목을 통과했다. [배포용 검사](distribution.txt) · [개발용 검사](development.txt) · [재실행 명령과 출력 위치](../../../../docs/manual/build.md#개발용배포용-프로필과-디버그-심볼)
