# 오브젝트 복사·붙여넣기와 씬 간 이동 (로드맵 11.11)

2026-11-23, macOS 27.0.1 arm64. KeyRoom 사본을 이 브랜치의 배포 에디터(`editor/MoNoon.app`)에서 [검사기](../../KeyRoom.CopyPaste/README.md)로 편집했다. 저장한 사본으로 Distribution 프로필의 Release와 Native AOT를 빌드했다.

## 구현

- 복사 경로는 기존 Duplicate와 같다. `SceneDocument.DuplicateObject`의 본문을 `CopyObjects`로 분리했고, Duplicate와 Paste가 모두 이 함수로 새 ID를 만들고 참조·프리팹 연결(`RemapLink`)·스크립트 필드(`ScriptComponent.Duplicate`)를 다시 연결한다. [SceneDuplication.cs](../../../src/Engine.Core/SceneDuplication.cs)
- Copy는 선택한 하위 트리만 담은 씬 YAML에 `# MoNoon objects` 첫 줄을 붙여 OS 클립보드에 쓴다. 다른 프로세스에서도 읽힌다(검사 뒤 `pbpaste` 첫 줄 `# MoNoon objects`, 3099바이트). 두 에디터 인스턴스 사이의 붙여넣기는 실행하지 않았다.
- Paste는 클립보드 텍스트를 씬 파일과 같은 검증(`Validate`)에 통과시킨 뒤 사본을 만든다. 표식이 없거나 읽을 수 없는 텍스트는 씬을 바꾸지 않고 거부한다.
- Undo는 기존 씬 스냅숏 기록(`FinishObjectChange`)을 그대로 쓴다. 붙여넣기 한 번, 잘라내기 한 번이 각각 기록 하나다.
- Edit 메뉴에 Cut·Copy·Paste(macOS ⌘X/⌘C/⌘V, Windows·Linux Ctrl+X/C/V)를 추가했다. Hierarchy 우클릭 메뉴에도 같은 명령을 넣었다. 메뉴가 이 키를 가져가므로 텍스트 필드에 포커스가 있으면 해당 TextBox의 Cut/Copy/Paste를 실행한다.
- 끊긴 참조 표시는 기존 Inspector 표시(`Missing object or component`, 목록은 `Missing object`, 카메라 Follow는 `Missing target`)를 그대로 쓴다. 게임 빌드의 기존 참조 검사는 끊긴 참조가 있는 씬을 거부한다.

## 에디터 (macOS arm64, 배포 에디터)

| 단계 | 결과 |
| --- | --- |
| Edit 메뉴 | `Undo ⌘Z · Redo ⇧⌘Z · Cut ⌘X · Copy ⌘C · Paste ⌘V` |
| 이름 필드에 포커스를 두고 Copy | 클립보드 `Floor`, 씬 변화 없음 |
| Rig(+Rig Arm)와 Chest 1(프리팹)을 Copy | OS 클립보드에 3099자, 첫 줄 `# MoNoon objects` |
| 같은 씬에 Paste | `Rig (1)`, `Rig Arm`, `Chest 1 (1)`, `Chest Lid`. 새 ID 4개, 씬 전체 ID 중복 없음. Copied → `Chest 1 (1)`, Outside → 기존 Floor. 프리팹 원본 ID와 인스턴스 맵 유지 |
| Undo / Redo | Undo 한 번에 붙여 넣기 전 스냅숏과 같음. Redo에 같은 ID로 복원 |
| `Rig (1)` Cut / Undo | 부모와 자식 삭제, 클립보드 갱신. Undo 한 번에 같은 ID로 복원 |
| 저장 후 SecondRoom을 열고 Paste | `Rig`, `Rig Arm`, `Chest 1`, `Chest Lid`(이름 충돌 없음). Copied → 붙여 넣은 Chest 1, Outside → PrefabWorkshop의 Floor(이 씬에 없음) |
| Inspector, 1280px | Outside `Missing object or component`. 필드 x=1130, 너비 142, 레이블 x=1070으로 Copied 행과 같음. Sprite Renderer 등 다른 행의 값 열도 x=1130 |
| Undo / Redo, 저장 | 붙여넣기 전후 스냅숏 일치. 다시 연 파일에 끊긴 Outside와 프리팹 연결 유지 |
| Scene Play(끊긴 참조) | `copied 1 chest 1 outside 0`, 예외 없음 |
| Outside를 North Wall로 다시 연결, 저장, Scene Play | `copied 1 chest 1 outside 1` |

[단계별 기록](editor.txt) · [Inspector 1280px](inspector-missing-1280.png) · [에디터 전체 1280px](editor-1280.png)

검사기는 Edit 메뉴 항목을 네이티브 메뉴의 클릭 진입점으로 실행했다. 실제 키 입력과 마우스 포인터는 사용하지 않았다. Scene 뷰포트(SDL 패널)는 캡처에 그려지지 않는다.

## 게임 빌드 (macOS arm64)

끊긴 참조를 남긴 채 빌드하면 두 빌드 모두 기존 검사에서 멈춘다. [첫 오류](build-refused.txt)

```
Scenes/SecondRoom.scene: Rig: TraceCopies.Outside refers to a missing object or required component on 9d6e1b42-7f3a-5e28-b0c5-4a1f8d3e6c21.
```

Outside를 다시 연결해 저장한 사본의 결과:

| 실행 | 결과 | 금지 파일 |
| --- | --- | --- |
| [Release](release.json) | `copied 1 chest 1 outside 1` | 없음 |
| [Native AOT](native-aot.json) | `copied 1 chest 1 outside 1` | 없음 |

두 플레이어는 SecondRoom에서 시작하는 이벤트 없는 10프레임 리플레이를 헤드리스로 실행했다. 사본에만 넣은 관찰 스크립트가 런타임 API로 두 참조의 대상을 확인했다. 금지 파일 검사는 앱 번들에서 Engine.Editor, Engine.Authoring, Avalonia, SkiaSharp, `MoNoon.dll`, 검사기 어셈블리 이름을 찾는다.

## 회귀

`./monoon test` 통과. [ObjectClipboardChecks](../../Engine.Checks/ObjectClipboardChecks.cs)는 중첩 선택의 한 번 복사, 같은 씬·다른 씬 붙여넣기의 참조와 프리팹 연결, 저장 후 다시 열기, 끊긴 참조가 있는 씬의 실행, 잘못된 클립보드 텍스트 거부를 확인한다. 기존 Duplicate 검사들도 공통 함수로 바뀐 경로에서 통과했다.

## 확인하지 않은 것

- 두 에디터 프로세스 사이의 붙여넣기, 다른 프로젝트로의 붙여넣기(프리팹·스프라이트 에셋 ID가 다르면 끊긴다).
- 실제 ⌘C/⌘V 키 입력과 Windows·Linux의 Ctrl 키 경로. 메뉴 항목과 단축키 표시는 확인했다.
- Windows x64·Linux x64 게스트(조정자가 나중에 실행).

Linux x64 게스트(2026-11-23): Xvfb의 OS 클립보드가 값을 돌려주지 않아 원래 검사는 `text field copy`에서 실패했다. 이때 Paste가 동작하지 않던 에디터 문제를 고쳤고, OS 클립보드 검사 세 개만 건너뛴 진단 실행과 Release·AOT는 통과했다. [Linux 결과](../chapter11-linux-x64/README.md)
