# InventoryLab 크래시 진단 검증

2026-11-15 KST, macOS 27.0.1 arm64, .NET 10.0.101. InventoryLab의 배포용 Release와 Native AOT에 같은 의뢰 테이블을 읽혔다. 약초 수집 의뢰의 `rewardCount`를 0으로 둔 재현용 모드를 각각의 임시 프로필에서 켜고, 두 앱을 일반 SDL 플레이 경로로 실행했다. 첫 프레임의 `QuestLog.OnCreate`에서 두 앱 모두 종료 코드 1과 함께 로컬 크래시 보고서 한 개를 남겼다. [재현 데이터](../../fixtures/InventoryLab/invalid-reward.mod) · [재실행 스크립트](../../InventoryLab.Crashes/check.py)

| 배포본 | 빌드 ID · 일치한 심볼 | 최초 예외 프레임 | 해석 결과 |
| --- | --- | --- | --- |
| Release | `7c99a497b83b48b1ba346fbb3142a2f5` · 게임 스크립트 PDB `d0a6f107-8576-4a1f-8f6e-3244442a4241` | 메서드 토큰 `0x06000020` · IL `0x86` | `Scripts/Quests/QuestJournal.cs:35` · [원본](release.crash) · [해석](release.txt) |
| Native AOT | `959313df7a7340f69cf84d922d4c4ce7` · arm64 실행 파일과 dSYM UUID `0EE8FE2D-5BC1-3421-AE40-14FDE4058C2F` | 이미지 상대 반환 주소 `0x908ea0` · 호출 명령 `0x908e9c` | `Scripts/Quests/QuestJournal.cs:35` · [원본](native-aot.crash) · [해석](native-aot.txt) |

원인 예외는 두 보고서 모두 `System.IO.InvalidDataException`이고 메시지는 `Quest 'herbalist' needs a positive integer rewardCount.`다. 심볼을 대조한 결과 35번째 줄에서 예외를 던졌고, 27번째 줄에서 보상 수량을 읽으며, 호출은 `QuestLog.cs` 10번째 줄의 `OnCreate`에서 시작했다. [수량 검사](../../../../samples/InventoryLab/Scripts/Quests/QuestJournal.cs) · [생성 콜백](../../../../samples/InventoryLab/Scripts/Quests/QuestLog.cs)

이전 빌드의 심볼 폴더와 서로 다른 형식의 심볼 폴더는 빌드 ID 불일치로 거부됐다. 같은 빌드 기록을 유지한 임시 폴더에 다른 PDB 또는 dSYM을 넣어도 심볼 ID 불일치로 종료 코드 1을 반환했다. 재현 모드를 끈 뒤에는 두 앱에서 약초 의뢰 입력 475프레임을 재생해 기존 기준 저장과 바이트 단위로 일치했고 추가 보고서는 생기지 않았다. 엔진 전체 회귀 374개와 InventoryLab 배포 프로필 검사 43개가 통과했다. [기준 입력과 저장](../../fixtures/InventoryLab/) · [크래시 보고서 사용법](../../../../docs/manual/build.md#크래시-진단)
