# KeyRoom 조작키 설정과 영구 저장 검증

2026-11-22, macOS 27.0.1(26A434) arm64와 .NET SDK 10.0.101에서 확인했다. 관찰 스크립트를 붙인 KeyRoom 복사본을 distribution 프로필로 Release와 Native AOT로 빌드했다. 각 빌드는 새 프로필 하나에서 세 번 다시 시작하며 `--replay-input`으로 창 없이 입력을 재생했다. 같은 세 입력을 MoNoon 패키지 에디터의 Scene Play에서도 Play 시작·정지를 반복하며 재생했다. [재현 명령과 입력](../../KeyRoom.Rebinding/README.md)

| macOS arm64 실행 대상 | 빌드 | 결과 |
| --- | --- | --- |
| Release | distribution, 빌드 ID `b5425bc4501f44729934352f898494be` | [12개 통과](release.txt) |
| Native AOT | distribution, 빌드 ID `d92b7bb1d2ec4620917c58a62c85dbb5` | [12개 통과](native-aot.txt) |
| 에디터 Scene Play | `engine/scripts/build-editor-mac.sh`로 만든 `editor/MoNoon.app` | [12개 통과](scene-play.txt) |

세 대상의 기록과 실행별 `player.settings`는 바이트 단위로 같았다. 아래 링크는 Release 기록이다.

## 관찰 결과

1. 첫 실행의 첫 프레임 안내는 `Interact: E`였다. **K**로 조작키 화면을 열고 **Enter**를 누르자 상자 열기가 입력 대기 상태가 됐다. **C**를 누르자 상자 열기는 `C, Space, Pad.South`, 동전 들기는 `E`로 교환됐고 안내가 `Interact: C`로 바뀌었다. [rebind.trace](rebind.trace)
2. 다시 **Enter → Esc**를 누르자 대기만 끝났고 바인딩은 바뀌지 않았다. **→**와 **Enter**로 게임패드 칸을 고르고 패드 **North**를 누르자 상자 열기의 패드 입력은 North, 방 안내(RoomGuide)는 South가 됐다. 같은 프레임에 안내가 패드 표기 `Move: DpadLeft / DpadRight   Interact: North`로 바뀌었다.
3. **K**로 닫은 뒤 **E**를 눌러도 상자는 닫혀 있었다. 저장된 [player.settings](rebind.player.settings)에는 바뀐 Interact·CarryCoin·RoomGuide만 기록됐다.
4. 두 번째 실행은 입력 전 첫 프레임부터 `Interact: C`였다. 10프레임의 **E**로는 열리지 않았고 30프레임의 **C**를 누른 다음 프레임(31)에 상자가 열렸다. 조작키 화면의 **Restore defaults**를 클릭하자 안내가 `Interact: E`로 돌아왔고 `inputBindings` 키가 설정에서 사라졌다. [restart.trace](restart.trace)
5. 세 번째 실행에서는 기본값 **E**로 상자가 열렸다. [restored.trace](restored.trace)

`./monoon test`의 [RebindingChecks](../../Engine.Checks/RebindingChecks.cs)는 같은 KeyRoom 화면을 입력 라우터로 조작한다. 키 반복 무시, 마우스·패드·키로 하는 취소, 연결할 수 없는 키 무시, 잘못된 저장값의 기본값 대체, 화면 열과 버튼의 정렬도 검사한다. 같은 날 이 Mac의 작업 트리에서 실행한 `./monoon test`는 `All checks passed.`로 끝났고, 이 검사의 [26개 항목](checks.txt)이 모두 통과했다.

## 확인하지 않은 항목

- 이번 기록은 실제 창의 키보드·패드 이벤트가 아니라 재생 입력이다. 배포 게임 창에서 Esc가 게임을 끝내지 않고 입력 대기를 취소하는 경로와 에디터 Scene 패널에서 Esc를 누르는 경우는 실행하지 않았다.
- 물리 게임패드는 사용하지 않았다.
- Windows x64와 Linux x64에서는 실행하지 않았다.
