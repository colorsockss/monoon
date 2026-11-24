# DisplayLab 정수 배율 검증 (16.8)

`verify.py prepare`는 DisplayLab을 별도 폴더에 복사하고 `GameSettings`에 `integerScale: true`를 더한다. 저장소의 DisplayLab은 레터박스 그대로 두어 [8.7 화면 설정 검사](../DisplayLab.Desktop/README.md)의 기준을 바꾸지 않는다.

`verify.py play`는 배포 게임을 실제 Mac 창으로 네 번 연다. 프로필의 `display.settings`에 창 콘텐츠 크기 800×450, 720×540, 1300×760을 차례로 쓰고, 마지막에는 파일을 바꾸지 않고 다시 연다. 각 실행에서 `screencapture -l`로 창을 찍어 다음을 본다.

- 검은 여백을 뺀 게임 영역이 정확히 800×450의 정수 배(Retina에서 2·1·3배)이고 가운데에 있다.
- 오른쪽 위 [PixelBadge.png](../../../samples/DisplayLab/Assets/PixelBadge.png)(12×12, 논리 48×48)의 모든 텍셀이 한 색으로 채운 정사각형 블록이고, 전체가 세 색뿐이다. 캡처는 색 관리를 거치므로 정확한 RGB 대신 텍셀끼리 비교한다. 보간하면 경계에 중간색이 생겨 실패한다.
- 다시 연 창의 게임 픽셀이 직전 1300×760 캡처와 같다.

`compare`는 Release와 Native AOT 캡처의 콘텐츠 픽셀이 같은지 비교한다. 첫 1pt(2px) 행은 창 포커스에 따라 바뀌는 제목 표시줄 구분선이라 제외한다.

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

도우미는 창 위치(`geometry`)만 읽으므로 키 입력·손쉬운 사용 권한이 필요 없다. 화면 기록 권한은 필요하다. Pillow를 쓰지 않고 PNG를 직접 읽는다. Scene Play 검사는 패키지 에디터에서 Play를 누르고 창을 1280×800, 1600×1000, 1920×1080으로 바꿔 Game 패널의 `Scale × RenderScaling`이 장치 픽셀 기준 정수(Fit을 내림, 최소 1)인지 확인한다. 카메라 좌표 반올림은 `./monoon test`의 [EngineFeatures1Checks](../Engine.Checks/EngineFeatures1Checks.cs)가 확인한다. [검증 결과](../results/engine-features-1/README.md#168-정수-배율-표시)
