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
- Windows x64와 Linux x64 게스트의 에디터 Scene Play는 원래 검사로는 실패했다. 아래 절을 참고한다.

## Windows x64와 Linux x64 게스트

2026-11-23, 이 Mac의 QEMU(TCG) 게스트에서 실행했다. 관찰 스크립트를 붙인 같은 사본을 각 게스트에서 Release와 Native AOT(distribution)로 빌드했다. 에디터는 이 브랜치로 만든 Windows·Linux 패키지다. Linux 에디터는 Xvfb에서 실행했다. [게스트 환경과 실행 방법](../x64-guest-10.2-10.8/README.md)

| 게스트 | Release | Native AOT | 에디터 Scene Play | Scene Play 진단(추가 씬 대기 200 ms) |
| --- | --- | --- | --- | --- |
| Windows 11 x64 | 12개 통과 | 12개 통과 | **실패** (2개 통과 후 `C swaps with Carry coin…`) | 12개 통과 |
| Debian 12 x64 | 12개 통과 | 12개 통과 | **실패** (같은 항목) | 12개 통과 |

- 통과한 여섯 실행의 `rebind.trace`·`restart.trace`·`restored.trace`·`rebind.player.settings`는 위 macOS 기록과 바이트 단위로 같았다. [실행별 해시](x64/runs.tsv)
- Scene Play 실패는 두 OS에서 같은 기록을 남겼다. 70프레임의 **Enter**와 80프레임의 **C**가 반영되지 않았고, 90프레임의 **Enter**에서야 입력 대기가 시작됐다. **K**(5프레임)로 연 조작키 화면이 그때까지 읽히는 중이었던 것으로 보인다(아래 진단 참고). 이후 C·E 교환 없이 기록이 진행됐다. [Windows 기록](x64/win-x64-scene-play-rebind.trace) · [Linux 기록](x64/linux-x64-scene-play-rebind.trace)
- 관찰 스크립트는 추가 씬을 읽는 동안 재생 프레임마다 실제 시간 20 ms를 기다린다. 에디터의 재생은 프레임을 연달아 실행하므로 65프레임 동안 약 1.3초를 준다. 에뮬레이션된 에디터에서는 씬을 읽는 데 그보다 오래 걸렸다. 독립 플레이어에서는 같은 대기로 충분했다. 진단을 위해 게스트 사본에서만 대기를 200 ms로 늘리자 두 OS 모두 12개 항목을 통과했고, 기록과 설정 파일이 macOS와 바이트 단위로 같았다. 저장소의 관찰 스크립트는 바꾸지 않았다. 따라서 원래 검사의 Windows·Linux Scene Play는 통과로 기록하지 않는다.
- 이 실행은 재생 입력이다. 실제 키보드·패드, 물리 GPU, 실제 PC의 에디터 속도는 확인하지 않았다.
