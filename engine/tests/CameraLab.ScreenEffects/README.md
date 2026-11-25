# CameraLab 화면 효과·진동·커서 GUI 검증

[verify.py](verify.py)는 CameraLab 복사본(시작 씬은 쇼케이스 `Showcase.scene`, 효과 패널의 키가 ScreenFxDemo로 간다)을 에디터 Scene Play, Release, Native AOT 앱으로 차례로 열고 같은 입력을 보낸다. 입력은 OS 키 이벤트가 아니라 [rumble-pad.c](rumble-pad.c)가 붙이는 SDL 가상 게임패드로 보낸다. 이 픽스처는 `DYLD_INSERT_LIBRARIES`로 검증 프로세스에만 로드되며, SDL이 패드에 보내는 진동(`SDL_RumbleGamepad`가 도달한 저주파·고주파 세기와 SDL이 멈출 때의 `0 0`)을 시각과 함께 기록한다. 에디터 Play는 [시작 훅](StartupHook.cs)이 Play 버튼을 눌러 시작한다. 실제 패드는 필요 없다.

| 단계 | 패드 입력 | 확인하는 내용 |
| --- | --- | --- |
| 정지 캡처 | Start(정지) → West·East·RightShoulder·LeftShoulder → 오른쪽 트리거로 6·6·24·15틱 진행 | 흔들림·줌, 플래시, 색 보정, 페이드의 정지 화면. 기준 화면과 달라진 게임 영역 픽셀의 평균색 |
| 접근성 | Back(25%) → West·East | 같은 위상의 흔들림이 덜 움직이고 플래시가 약해짐 |
| 진동 | North → West → RightStick(끄기) → North·West | 가상 패드가 받은 세기와 지속 시간, 끈 뒤에는 호출 없음 |
| 커서 | LeftStick 두 번 | `MONOON_FEEDBACK_LOG`의 커서 픽셀 해시·핫스폿, 커서를 포함한 화면 캡처 |
| 다시 실행 | North → RightStick → North | 저장한 25%·진동 끄기 복원, 다시 켠 뒤 진동 |

효과 값은 [ScreenFxDemo](../../../samples/CameraLab/Scripts/ScreenFxDemo.cs)가 트리거 후 1·6·12·24번째 게임 틱에 슬롯 3에 기록하며, 세 실행 경로의 기록은 바이트 단위로 같아야 한다.

저장소 루트에서 실행한다. 창 도구 `engine/.artifacts/clip-qa/window`와 SDL 3.4.12 헤더 아카이브는 기존 GUI 검증과 같다. 도구나 드라이버는 설치하지 않는다.

```sh
qa=$(mktemp -d); src=engine/tests/CameraLab.ScreenEffects
tar -xf engine/.artifacts/native/downloads/SDL3-3.4.12.tar.gz -C "$qa" SDL3-3.4.12/include
clang -dynamiclib -I"$qa/SDL3-3.4.12/include" "$src/rumble-pad.c" -o "$qa/rumble.dylib"
engine/scripts/build-players.sh osx-arm64 && engine/scripts/build-editor-mac.sh
dotnet build "$src" -c Release -m:1 -p:UsedAvaloniaProducts=
cp -R samples/CameraLab "$qa/CameraLab"
./monoon build "$qa/CameraLab" release --profile distribution --output "$qa/builds"
./monoon build "$qa/CameraLab" native-aot --profile distribution --output "$qa/builds"
MONOON_QA_WINDOW=engine/.artifacts/clip-qa/window MONOON_QA_RUMBLE_LIBRARY="$qa/rumble.dylib" python3 "$src/verify.py" \
  "$qa/CameraLab" "$qa/builds/CameraLab.app" "$qa/builds/NativeAOT/CameraLab.app" \
  engine/.artifacts/bin/CameraLabProbe/release/CameraLabProbe.dll "$qa/out"
```

경로 하나만 다시 확인하려면 마지막에 `editor`, `release` 또는 `aot`를 붙인다. 패드는 게임 창에 포커스가 있을 때만 읽으므로 검증기는 입력마다 창을 앞으로 가져온다. 같은 동작의 단위 검사는 `./monoon test`의 [ScreenEffectsChecks](../Engine.Checks/ScreenEffectsChecks.cs)에 있다.

Mac 창 도구와 가상 패드가 없는 Linux·Windows에서는 [replay.py](replay.py)로 같은 키 순서를 `--replay-input`으로 재생해 효과 기록·`player.settings`·정지 프레임만 비교한다(`replay.py play <MoNoonPlayer> <out>`, `replay.py compare <out> <out>`). 진동·커서·Scene Play는 확인하지 않는다.
