# 로드맵 1.4 스크립팅 — macOS arm64, 2026-11-13

패키징한 MoNoon 에디터에서는 KeyRoom 복사본을 열었고, 독립 실행은 원본 KeyRoom에서 막 내보낸 Release와 Native AOT를 각각 실행했다.

| 검사 | 관찰한 결과 |
| --- | --- |
| 공용 파일 생성 | Project → + → [New Shared C# File…](shared-dialog.png)에서 기본 이름으로 생성했다. [Project에서 선택된 GameRules.cs](shared-created.png)와 디스크의 정적 클래스 선언을 확인했다. 검증용 파일은 샘플 원본에 추가하지 않았다. |
| 타입 참조 Inspector | Chest의 OpenChest → Player를 열면 [None과 Player만 표시](typed-picker.png)됐다. 일반 오브젝트 참조인 Lid, 프리팹 참조인 Loot는 기존 값을 유지했다. [942×587로 줄인 창](compact-inspector.png)에서도 입력 열과 레이블 간격을 확인했다. |
| KeyRoom 에디터 Play | E로 [상자를 열면 코인이 생성](play-spawn.png)됐다. 오른쪽으로 이동해 닿으면 코인이 사라지고 [COIN: 1](play-collected.png)이 표시됐다. |
| macOS Release | 독립 앱에서 E로 [생성](release-spawn.png), 오른쪽 방향키로 [획득](release-collected.png). 배포 데이터에 프로젝트 DLL 하나가 있고 Scripts 소스 폴더는 없다. |
| macOS Native AOT | 별도로 빌드한 네이티브 실행 파일에서 같은 순서로 [생성](aot-spawn.png)과 [획득](aot-collected.png)을 확인했다. 게임 데이터에서 스크립트 DLL을 동적으로 로드하지 않는다. |

[회귀 검사 로그](checks.txt)와 [스크립팅 검사 코드](../../Engine.Checks/ScriptingChecks.cs): 컴포넌트와 일반 파일 분류, 공용 상수 변경 후 Inspector 값 갱신, 공용 파일 오류 위치, 내장 컴포넌트 설정 조회의 격리, 타입 참조의 복제·프리팹 재연결, 비활성 부모 아래 생성, 활성 상태 중복 지정, 생성 콜백에서 자기 제거, 자식부터 종료, 새 Play 상태 초기화를 검사했다. 같은 검사 실행에서 세 플랫폼용 Release를 내보내 각각의 패키지에 담긴 DLL로 KeyRoom 씬·프리팹을 macOS 호스트에서 로드했다. 이번 단계에서 Windows·Linux 게스트 창을 직접 실행하지는 않았다.

저장소 루트에서 재현:

```sh
dotnet run --project engine/tests/Engine.Checks -c Release -- .
editor/MoNoon.app/Contents/MacOS/MoNoon --build-release samples/KeyRoom game-builds/KeyRoom
editor/MoNoon.app/Contents/MacOS/MoNoon --build-native-aot samples/KeyRoom game-builds/KeyRoom
```
