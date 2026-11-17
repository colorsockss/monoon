# KeyRoom 프리팹 원본 편집과 속성별 오버라이드

2026-11-16, macOS 27.0.1 arm64. KeyRoom 사본을 배포 에디터에서 수정하고, 저장한 사본 그대로 Distribution 프로필의 Release와 Native AOT로 빌드했다. 원본 샘플은 아래 첫 행에서 시작한다.

| 편집 단계 | Oak.prefab | Oak tree | Courtyard oak |
| --- | --- | --- | --- |
| 시작 | 102 × 98 | 102 × 98 | 76 × 70 |
| 원본 창에서 높이 112로 저장 | 102 × 112 | 102 × 112 | 76 × 70 |
| Courtyard oak의 width만 Apply to Source | 76 × 112 | 76 × 112 | 76 × 70 |
| 씬에서 Cmd+Z 한 번 | 102 × 112 | 102 × 112 | 76 × 70 |
| width를 다시 적용하고 height만 Revert Property | 76 × 112 | 76 × 112 | 76 × 112 |

Project에서 검색한 **Oak.prefab**을 더블클릭했다. 별도 창의 Hierarchy에는 원본 나무 한 개가 표시됐다. 원본을 저장한 뒤 씬 Inspector의 **Overrides…**에서 너비와 높이를 각각 선택했다. 단계마다 저장한 프리팹과 씬의 수치를 대조했다. 에디터를 종료하고 다시 열었을 때 **Courtyard oak**에는 이름 오버라이드만 남았고, 두 나무의 배치 좌표는 각각 (243, 172), (375, 203)이었다. [원본 편집](source.png) · [속성 선택](overrides.png) · [씬 재열기](reopened.png)

긴 오브젝트 이름을 지정한 뒤 Overrides 창을 350 × 263으로 줄였다. name·width·height가 각 행 맨 앞에 보였고 세 버튼은 같은 높이에서 창 안에 배치됐다. [작은 창](compact.png)

| 실행 | 나무 두 그루 | 플레이 확인 |
| --- | --- | --- |
| [Scene Play](scene-play.png) | 두 위치에 76 × 112. 런타임 Inspector에서도 크기와 월드 위치 확인 | 열쇠 획득 → 문 통과 → 두 번째 방 F5 저장 |
| [macOS arm64 Release](release.png) | 같은 위치와 실루엣 | 같은 입력과 저장 결과 |
| [macOS arm64 Native AOT](native-aot.png) | 같은 위치와 실루엣 | 같은 입력과 저장 결과 |

캡처에서 플레이어와 고정 벽을 기준으로 월드 좌표를 화면에 대응시켰다. 두 나무 각각에 대해 스프라이트 주변과 내부를 샘플링한 뒤, 배율에 따라 달라지는 1픽셀 경계를 제외했다. 나머지 9,678개 지점의 나무·배경 판정이 세 실행 모두 Oak.png의 알파 마스크와 일치했다. 세 F5 파일은 모두 107바이트였고 SHA-256은 `00d5309db260658bb54a80d78c4e2b4e7d05e473550e69d70a84e6acc9833805`였다. [기준 저장](../script-asset-fields/second-room.save)

엔진 전체 회귀도 통과했다. 프리팹 검사는 중첩·변형 안의 자식 ID, 다른 인스턴스로의 전파, 스크립트 오브젝트 참조의 ID 변환, 씬 전용 참조의 적용 거부, 열어둔 원본보다 나중에 저장한 변경의 보호를 포함한다. [회귀 검사](../../Engine.Checks/PrefabEditingChecks.cs)
