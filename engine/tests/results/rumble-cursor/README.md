# CameraLab — 게임패드 진동과 마우스 커서 (macOS)

2026-11-23, Apple Silicon arm64, macOS 27.0.1. 이 Mac에는 실제 게임패드가 연결되어 있지 않다(사용자 결정). 대신 [rumble-pad.c](../../CameraLab.ScreenEffects/rumble-pad.c)를 `DYLD_INSERT_LIBRARIES`로 검증 프로세스에만 넣어 SDL 가상 게임패드를 붙였다. 엔진은 이 패드를 첫 번째 게임패드로 열고 `SDL_RumbleGamepad`를 호출한다. SDL은 그 세기를 가상 패드의 진동 콜백으로 전달하고, 지속 시간이 끝나면 `0 0`을 보낸다. 같은 실행에서 엔진의 `MONOON_FEEDBACK_LOG` 기록(기기·세기·밀리초·결과, 커서 픽셀·핫스폿)도 남겼다. 실행 경로는 에디터 **Scene Play**, Release, Native AOT이며 [GUI 검증기](../../CameraLab.ScreenEffects/README.md)로 같은 입력을 보냈다. 첫 예외: 없음.

## 진동

재현 입력(패드): West(흔들림·`Rumble(1, .5, .4)`)를 게임 시간을 멈춘 채 두 번 → North(`Rumble(.3, .9, .2)`) → West → RightStick(진동 끄기) → North·West(무시) → 다시 실행 → North(무시) → RightStick(켜기) → North.

| 순서 | 요청 | SDL 호출(저주파, 고주파, ms) | Scene Play: 가상 패드가 받은 값과 멈출 때까지 | Release | Native AOT |
| --- | --- | --- | --- | --- | --- |
| 1 | 흔들림 | 65535, 32768, 400 | 65535, 32768 · 408 ms | 407 ms | 401 ms |
| 2 | 줄인 흔들림 | 65535, 32768, 400 | 408 ms | 408 ms | 399 ms |
| 3 | 짧은 진동 | 19660, 58982, 200 | 209 ms | 200 ms | 205 ms |
| 4 | 흔들림 | 65535, 32768, 400 | 405 ms | 402 ms | 403 ms |
| — | 진동 끔: 짧은 진동·흔들림 | 호출 없음 | 호출 없음 | 호출 없음 | 호출 없음 |
| — | 다시 실행 후(끔 상태 복원) 짧은 진동 | 호출 없음 | 호출 없음 | 호출 없음 | 호출 없음 |
| 5 | 진동을 다시 켠 뒤 짧은 진동 | 19660, 58982, 200 | 209 ms | 209 ms | 204 ms |

세 경로의 엔진 기록 `rumble device="MoNoon rumble verification pad" low=… high=… ms=… result=ok` 다섯 줄은 순서와 값이 같다. 가상 패드가 받은 세기도 다섯 번 모두 요청과 같고, SDL은 매 프레임 `SDL_UpdateGamepads`에서 요청한 시간 뒤 ±12 ms 안에 멈췄다. 기록: [Scene Play 엔진](editor.feedback) · [가상 패드](editor.rumble) · [Release 엔진](release.feedback) · [가상 패드](release.rumble) · [AOT 엔진](aot.feedback) · [가상 패드](aot.rumble)

## 커서

LeftStick으로 [Cursor.png](../../../../samples/CameraLab/Assets/Cursor.png)(17×17, 핫스폿 8, 8)를 커서로 지정하고 다시 눌러 시스템 커서로 되돌렸다.

| 확인 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| 기록 `cursor file=Cursor.png size=17x17 hot=8,8 rgba-sha256=c7c05cfd…bd81` → `cursor system` | 통과 | 통과 | 통과 |
| 해시가 검증기가 PNG를 직접 디코드한 값과 같음 | 통과 | 통과 | 통과 |
| 커서를 포함한 캡처 | 확인 못 함: [포인터만 옮긴 캡처](editor-cursor.png)는 화살표 | [커스텀](release-cursor.png) · [시스템](release-system-cursor.png) | [커스텀](aot-cursor.png) · [시스템](aot-system-cursor.png) |

해시는 완전히 투명한 픽셀을 0으로 둔 RGBA 바이트의 SHA-256이다. 플레이어는 이 픽셀로 `SDL_CreateColorCursor`를 호출한다. Mac Scene Play는 같은 픽셀로 Scene 패널(NSView)의 `NSCursor`를 만든다. 이 패널이 마우스를 직접 받으므로 Avalonia 컨트롤 커서는 패널 위에서 보이지 않는다. Windows·Linux의 Avalonia 미리보기는 같은 픽셀로 Avalonia `Cursor`를 만든다. 캡처는 `CGWarpMouseCursorPosition`으로 포인터를 게임 화면 위에 옮긴 뒤 `screencapture -C`로 찍었다. 이 환경은 OS 마우스 이벤트를 보낼 권한이 없어 포인터 이동 이벤트가 생기지 않는다. 플레이어는 SDL이 커서를 즉시 적용해 캡처에 조준 커서가 보인다. Scene 패널은 커서 영역(`resetCursorRects`)과 `mouseMoved`에서 커서를 적용하므로 실제로 마우스를 움직여야 바뀐다. 그래서 Scene Play는 상태 표시 `Cursor custom`과 픽셀 기록만 확인했고 화면 표시는 확인하지 못했다.

## 확인하지 않은 것

- 실제 패드의 모터 감각, 트리거 진동, 두 대 이상의 패드. 엔진은 기존처럼 첫 번째 패드 한 대만 사용한다.
- Mac Scene Play에서 실제 마우스를 움직였을 때 조준 커서가 보이는지. Windows·Linux x64 게스트(호출 기록 비교 예정)와 Avalonia 미리보기의 커서 표시.
- HiDPI용 2배 커서 이미지. 커서는 PNG 한 픽셀을 한 포인트로 표시한다.

Linux x64 게스트(2026-11-23): 가상 패드 픽스처가 macOS 전용이라 진동·커서는 확인하지 않았다. [Linux 결과](../chapter11-linux-x64/README.md)
