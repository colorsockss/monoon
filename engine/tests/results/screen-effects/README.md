# CameraLab — 화면 흔들림과 화면 전체 효과 (macOS)

2026-11-23, Apple Silicon arm64, macOS 27.0.1. [CameraLab](../../../../samples/CameraLab) 복사본을 패키지 에디터의 **Scene Play**(Mac SDL Scene 패널), Release 앱, Native AOT 앱으로 열고 [GUI 검증기](../../CameraLab.ScreenEffects/README.md)로 같은 입력을 보냈다. 입력은 검증 프로세스에만 로드한 SDL 가상 게임패드로 보냈다. 첫 예외: 없음.

## 재현 입력

Start로 게임 시간을 멈추고 효과마다 버튼을 누른 뒤 오른쪽 트리거로 정해진 틱만큼 진행해 창을 캡처했다. 흔들림·줌(West, 6틱), 플래시(East, 6틱), 색 보정(RightShoulder, 24틱), 색 보정 위 페이드 아웃(LeftShoulder, 15틱)이다. 이어서 Back으로 흔들림·깜빡임을 25%로 줄이고 흔들림과 플래시를 같은 틱에서 다시 캡처했다. 마지막으로 앱을 종료하고 같은 프로필로 다시 실행했다.

| 실행 경로 | 효과 없음 | 흔들림·줌 | 줄인 흔들림 | 플래시 | 줄인 플래시 | 색 보정 | 페이드 | 다시 실행 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Scene Play | [still](editor-still.png) | [shake](editor-shake.png) | [25%](editor-shake-reduced.png) | [flash](editor-flash.png) | [25%](editor-flash-reduced.png) | [grade](editor-grade.png) | [fade](editor-fade.png) | [restart](editor-restart.png) |
| Release arm64 | [still](release-still.png) | [shake](release-shake.png) | [25%](release-shake-reduced.png) | [flash](release-flash.png) | [25%](release-flash-reduced.png) | [grade](release-grade.png) | [fade](release-fade.png) | [restart](release-restart.png) |
| Native AOT arm64 | [still](aot-still.png) | [shake](aot-shake.png) | [25%](aot-shake-reduced.png) | [flash](aot-flash.png) | [25%](aot-flash-reduced.png) | [grade](aot-grade.png) | [fade](aot-fade.png) | [restart](aot-restart.png) |

## 결과

| 확인 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 색 보정: 게임 영역 평균색이 어둡고 따뜻해짐 (기준 → 보정) | (59, 70, 85) → (49, 51, 50) | (59, 70, 85) → (48, 50, 50) | (59, 70, 85) → (49, 51, 50) |
| 페이드 15/30틱: 보정된 화면의 절반 밝기 | (24, 25, 25) | (24, 25, 25) | (24, 25, 25) |
| 플래시 > 줄인 플래시 > 없음 (평균 밝기) | 181 > 99 > 71 | 181 > 99 > 71 | 182 > 99 > 72 |
| 정지 화면에서 바뀐 표본 픽셀: 흔들림 > 줄인 흔들림 | 6843 > 3926 | 6023 > 3905 | 6023 > 3900 |
| 효과 값 기록 (트리거 후 1·6·12·24틱) | [기록](editor.record) | 같은 바이트 | 같은 바이트 |
| `player.settings`: 25%·진동 끔 저장, 다시 실행 후 복원 | [첫 실행](editor-first.player.settings) · [재실행](editor-restart.player.settings) | [첫 실행](release-first.player.settings) · [재실행](release-restart.player.settings) | [첫 실행](aot-first.player.settings) · [재실행](aot-restart.player.settings) |
| 전체 | 통과 | 통과 | 통과 |

평균색과 바뀐 픽셀 수는 다섯 정지 캡처가 모두 서로 다른 픽셀, 즉 HUD를 뺀 게임 월드에서 구했다. Scene Play 수치는 에디터 Game 패널 안의 같은 영역이다. 줄인 플래시 6틱의 불투명도는 기록상 39/255로 전체(154/255)의 4분의 1이고, 줄인 흔들림은 같은 시드의 위상에서 진폭만 4분의 1이다. [단위 검사](../../Engine.Checks/ScreenEffectsChecks.cs)는 같은 입력을 두 세션에 넣어 프레임 값이 같은지, 미니맵은 흔들리지 않는지, `TryGetView`가 바뀌지 않는지, 색 보정 → HUD → 페이드 순서로 그리는지도 확인한다.

다시 실행한 화면의 오른쪽 위 상태 줄은 `Shake 25%   Flash 25%`, `Rumble off`이며, 이어서 진동을 다시 켜면 `accessibility.rumble: true`가 저장된다.

## 렌더링 방식

색 보정 중에는 월드를 창 픽셀 크기의 SDL3 렌더 타깃(`SDL_TEXTUREACCESS_TARGET`)에 그린다. 창의 논리 표시·뷰포트·클립을 타깃에 그대로 옮기므로 HiDPI 선명도가 같다. 타깃 위에 곱하기(`SDL_BLENDMODE_MOD`)·더하기(`SDL_BLENDMODE_ADD`) 채우기로 보정한 뒤 창에 블렌드해 합성하고 HUD를 그 위에 그린다. Mac Scene Play는 같은 `SdlCanvas` 경로를 쓴다. 이번 작업에서 `SdlCanvas.FillRectangle`이 반투명 색을 블렌드하도록 고쳤다. 이전에는 렌더러의 기본 그리기 블렌드가 `NONE`이라 반투명 채우기가 불투명하게 그려졌다.

BRUTAL: 렌더 타깃, 블렌드, 게임패드 진동과 커서 모두 기존 `Ahwoo.BRUTAL.Sdl` 2026.10.2의 `Brutal.SdlApi`로 호출했다. 새 패키지는 내려받지 않았다.

## 확인하지 않은 것

- Windows·Linux 에디터의 Avalonia 미리보기 경로(블렌드 채우기로 같은 계산)와 x64 게스트는 이번에 실행하지 않았다.
- 물리 GPU별 차이, 전체 화면 모드에서의 렌더 타깃 크기 변경은 창 모드에서만 확인했다.

배포본 검사: 두 앱에 SkiaSharp, Avalonia, `Engine.Editor`, `Engine.Authoring`, `MoNoon.dll`, `libBrutalScenePanel` 파일이 없다.

Linux x64 게스트(2026-11-23): 헤드리스 변형 [replay.py](../../CameraLab.ScreenEffects/replay.py)로 Release·AOT의 효과 기록과 설정 파일이 Mac 창 실행 기록과 같았다. Scene Play와 캡처 기준 판정은 건너뛰었다. [Linux 결과](../chapter11-linux-x64/README.md)
