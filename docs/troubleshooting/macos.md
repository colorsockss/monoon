# macOS arm64: Scene 패널과 독립 게임을 따로 재현하기

기준 환경은 Apple Silicon Mac이다. 에디터는 Avalonia, **Scene 패널만** BRUTAL SDL 3.4.12와 Cocoa NSView 브리지를 사용한다. 독립 게임은 자체 SDL 창을 연다. [기존 macOS 검증 기록](../../engine/tests/results/keyroom-desktop/yaml-osx-arm64/verify.txt)에는 에디터에서 YAML로 저장·재열기, Release와 Native AOT에서 열쇠 획득·둘째 방 진입 결과가 따로 있다.

## 엔진·에디터

저장소 루트에서 아래처럼 실행한다. 시작 시 예외는 터미널에서, Scene 초기화 중 잡힌 오류는 패널의 `SDL Scene unavailable.` 뒤에서 확인한다.

```sh
editor/MoNoon.app/Contents/MacOS/MoNoon "$PWD/samples/KeyRoom"
```

- **Scene이 비거나 패널 밖에 그려짐**: **Run → Save & Run**에서 같은 씬이 독립 창에는 그려지는지 확인한다. 독립 창이 정상이면 게임 에셋을 바꾸기 전에 `uname -m`과 `file editor/MoNoon.app/Contents/MacOS/{MoNoon,libSDL3.dylib,libSDL3.0.dylib,libBrutalScenePanel.dylib}`로 arm64 바이너리와 세 라이브러리를 확인한다. Intel 경로와 Apple Silicon의 [Scene 경로 선택](../../engine/src/Engine.Editor/SceneViewport.cs)은 다르다.
- **`The Scene panel needs the bundled SDL 3.4.12 Cocoa patch.`**: [MacSceneHost](../../engine/src/Engine.Editor/MacSceneHost.cs)가 로드된 SDL의 패치 식별자를 확인한 결과다. 버전 번호만 같은 일반 SDL로는 해결되지 않는다. .NET 10 SDK, Xcode Command Line Tools, CMake 3.24 이상이 있는 개발 환경에서 `./engine/scripts/build-editor-mac.sh`로 앱을 재생성한다. [빌드 스크립트](../../engine/scripts/build-scene-sdl-mac.sh)가 SDL 소스 해시와 [Cocoa 패치](../../engine/native/macos/SDL-3.4.12-pr16658.patch)를 관리하고, [패키징 대상](../../engine/src/Engine.Editor/MacScene.targets)이 두 SDL 이름과 브리지를 배치한다. 이 패치는 다른 OS 플레이어에 적용하지 않는다.
- **리사이즈 후 클릭이나 방향키만 어긋남**: 편집에서 모서리 근처 오브젝트 선택 → 창 크기 변경 → 같은 오브젝트 다시 선택 → Play에서 패널 클릭 후 방향키 → Stop 순으로 재현한다. Inspector 입력란에 포커스가 있을 때와 구분하고, 틀어진 위치·창 크기·디스플레이 배율을 기록한다. 패널 외부 침범과 입력 좌표 문제는 [Cocoa 호스트](../../engine/native/macos/ScenePanel.m) 및 [기존 Scene 캡처](../../engine/tests/results/editor/mac-scene.jpg)와 대조한다.

## 빌드된 게임

아래 두 줄은 별개 실행이다. 첫 게임을 닫고 두 번째를 실행한다.

```sh
game-builds/KeyRoom/KeyRoom.app/Contents/MacOS/MoNoonPlayer
game-builds/KeyRoom/NativeAOT/KeyRoom.app/Contents/MacOS/MoNoonPlayer
```

- **Finder에서는 바로 닫힘**: 터미널에서 같은 앱의 실행 파일을 열어 SDL 로드 오류인지 `Game/`의 씬·에셋 오류인지 가른다. 둘 다 실패하면 [공통 배포 점검](README.md)부터, AOT만 실패하면 같은 프로젝트와 시작 씬인지, 현재 엔진의 AOT 키트로 게임 스크립트까지 다시 컴파일했는지 확인한다. 독립 게임에는 Scene 패널 브리지가 필요하지 않다.
- **수정 확인**: 두 앱을 각각 첫 방부터 실행한다. 열쇠를 얻고 문을 지나 둘째 방의 벽과 HUD가 보이는지 확인한다. 기존 비교 화면: [Release](../../engine/tests/results/keyroom-desktop/yaml-osx-arm64/release-room-two.png) · [Native AOT](../../engine/tests/results/keyroom-desktop/yaml-osx-arm64/native-aot-room-two.png).
