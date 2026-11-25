# DisplayLab 정수 배율 검증 (16.8)

`verify.py prepare`는 DisplayLab을 별도 폴더에 복사하고 `GameSettings`에 `integerScale: true`가 있는지 확인한다. 2026-11-26부터 샘플은 쇼케이스 씬 [ShowcaseDisplay](../../../samples/DisplayLab/Scenes/ShowcaseDisplay.scene) 하나이고 정수 배율을 켠 채로 배포한다.

`verify.py play`는 배포 게임을 실제 Mac 창으로 네 번 연다. 프로필의 `display.settings`에 창 콘텐츠 크기 800×450, 720×540, 1300×760을 차례로 쓰고, 마지막에는 파일을 바꾸지 않고 다시 연다. 각 실행에서 `screencapture -l`로 창을 찍어 다음을 본다.

- 검은 여백을 뺀 게임 영역이 정확히 800×450의 정수 배(Retina에서 2·1·3배)이고 가운데에 있다.
- 설정 패널 오른쪽 위 [PixelBadge.png](../../../samples/DisplayLab/Assets/PixelBadge.png)(12×12, 논리 24×24, (750, 26))의 모든 텍셀이 한 색으로 채운 정사각형 블록이고, 전체가 세 색뿐이다. 캡처는 색 관리를 거치므로 정확한 RGB 대신 텍셀끼리 비교한다. 보간하면 경계에 중간색이 생겨 실패한다.
- 다시 연 창의 게임 영역 위치와 배지 픽셀이 직전 1300×760 캡처와 같다. 쇼케이스 방은 주인공·횃불·프레임 미터가 움직여 프레임 전체는 실행마다 다르다.

`compare`는 Release와 Native AOT 캡처의 배지 픽셀이 같은지 비교한다.

```sh
qa=engine/tests/DisplayLab.IntegerScale
work=$(mktemp -d)
python3 "$qa/verify.py" prepare samples/DisplayLab "$work/DisplayLab"
./monoon build "$work/DisplayLab" release --profile distribution --output "$work/builds"
./monoon build "$work/DisplayLab" native-aot --profile distribution --output "$work/builds"
swiftc engine/tests/AudioLab.Vorbis/mac-input.swift -o "$work/mac-input"
sh engine/scripts/gui-lock.sh python3 "$qa/verify.py" play "$work/builds/DisplayLab.app/Contents/MacOS/MoNoonPlayer" "$work/release" "$work/mac-input"
sh engine/scripts/gui-lock.sh python3 "$qa/verify.py" play "$work/builds/NativeAOT/DisplayLab.app/Contents/MacOS/MoNoonPlayer" "$work/aot" "$work/mac-input"
python3 "$qa/verify.py" compare "$work/release" "$work/aot"

engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
sh engine/scripts/gui-lock.sh python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon "$work/DisplayLab" \
  engine/.artifacts/bin/IntegerScaleProbe/release/IntegerScaleProbe.dll "$work/scene-play"
```

도우미는 창 위치(`geometry`)만 읽으므로 키 입력·손쉬운 사용 권한이 필요 없다. 화면 기록 권한은 필요하다. Pillow를 쓰지 않고 PNG를 직접 읽는다. Scene Play 검사는 패키지 에디터에서 Play를 누르고 창을 1280×800, 1600×1000, 1920×1080으로 바꿔 Game 패널의 `Scale × RenderScaling`이 장치 픽셀 기준 정수(Fit을 내림, 최소 1)인지 확인한다. 카메라 좌표 반올림은 `./monoon test`의 [EngineFeatures1Checks](../Engine.Checks/EngineFeatures1Checks.cs)가 확인한다. [검증 결과](../results/engine-features-1/README.md)
