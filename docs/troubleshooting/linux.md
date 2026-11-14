# Linux x64: X11 창 초기화와 HUD 그리기

검증 환경은 Docker가 아니라 이 Mac에 보존한 **QEMU Debian 12.15 amd64**다. [실행 기록](../../engine/tests/results/keyroom-desktop/yaml-linux-x64/results.json)의 커널은 6.1.0-54-cloud-amd64, glibc는 2.36이다. 게임은 X11 가상 화면(Xvfb 1280×900×24)에서 SDL 기본 렌더러와 더미 오디오로 실행했다. 실제 GPU, Wayland 세션, 소리 출력을 검증한 기록은 아니다.

## 엔진·에디터

Linux 패키지 에디터의 통과 기록은 없다. 소스와 .NET 10 SDK가 있는 Linux 그래픽 데스크톱에서 저장소 루트 기준으로 실행한다.

```sh
dotnet --info
dotnet run --project engine/src/Engine.Editor -- samples/KeyRoom
```

편집/Play는 [Avalonia Scene](../../engine/src/Engine.Editor/SceneViewport.cs), **Save & Run**은 독립 SDL 창이다. X11에서 둘 다 열리지 않으면 같은 셸의 `printenv DISPLAY`와 그래픽 세션을 먼저 확인한다. 둘 중 하나에서만 틀리면 편집/Play와 독립 창의 캡처를 각각 남기고 해당 렌더링 경로를 조사한다.

## 빌드된 게임

```sh
game=./game-builds/KeyRoom/KeyRoom-linux-x64
"$game/MoNoonPlayer" 2> /tmp/keyroom-player.log
```

- **`Permission denied` 또는 창이 안 뜸**: `ls -l "$game/MoNoonPlayer"`로 실행 비트를 확인한다. 실행 비트를 유지해 폴더 전체를 다시 복사한다. X11 세션 없이 SSH에서 실행한 경우에는 기존 VM의 [Xvfb 재검사 명령](README.md)을 사용한다. 검은 캡처만으로 HUD 문제라고 판단하지 않는다.
- **열쇠 획득/HUD 표시 중 `SDL_CreateTextureFromSurface … surface … invalid`**: [원래 실패 스택](../../engine/tests/results/keyroom-desktop/linux-original-crash.txt)은 PNG 로더가 아니라 `SdlText.Draw`를 가리킨다. 현재 [텍스트 경로](../../engine/src/Engine.Desktop/SdlText.cs)는 Skia의 RGBA 픽셀과 실제 행 간격으로 SDL surface를 만들고 포인터를 확인한 후 텍스처를 생성한다. [런타임 패키지](../../engine/src/Engine.Desktop/Engine.Desktop.csproj)에서 SkiaSharp와 Linux NativeAssets 버전이 일치하는지 확인한다(현재 둘 다 2.88.9).
- **라이브러리 문제와 구별**: 게스트에서 `ldd "$game/libSkiaSharp.so"`, `ldd "$game/libSDL3.so.0"`의 `not found`를 확인한다. 글자만 비면 `fc-match sans`로 사용할 폰트도 확인한다. 배포 파일이 오래됐으면 Mac에서 `./engine/scripts/build-players.sh linux-x64` 후 Linux x64 Release로 다시 내보낸다.
- **수정 확인**: 초기 화면만 보고 판단하지 않는다. 두 조작 방식으로 실제 열쇠를 먹고, HUD 글자와 둘째 방의 벽이 같이 그려지고, 리사이즈 후에도 글자가 남는지 확인한다. [열쇠 획득](../../engine/tests/results/keyroom-desktop/yaml-linux-x64/wasd/02-key.png) · [둘째 방](../../engine/tests/results/keyroom-desktop/yaml-linux-x64/wasd/03-room-two.png).

## InventoryLab 헤드리스 빌드·회귀

보존한 Debian 12.15 amd64 VM에서 .NET SDK 10.0.101과 clang 14로 Release와 Native AOT 배포본을 직접 빌드했다. 두 실행 파일 모두 화면 없는 환경에서 Mac의 입력 475프레임을 재생했고, 게스트에서 꺼낸 두 저장 파일 모두 Mac 기준 171바이트와 일치했다. [게스트 빌드 명령](../../engine/tests/InventoryLab.Replay/README.md) · [실행 로그](../../engine/tests/results/input-replay-x64/linux-x64/build.log)

이 VM의 SDK는 `~/monoon-buildtools/dotnet`, 오프라인 NuGet 피드는 `~/monoon-buildtools/feed`, 빌드와 소스는 `~/inventory-replay-check`에 있다. 재실행 셸에서 `DOTNET_ROOT`, `PATH`, `RestoreSources`에 이 위치를 지정한다. 메모리 3GB로 AOT를 빌드할 때 사용한 스왑 파일은 `/var/tmp/monoon-aot.swap`이다. VM 재부팅 후 필요하면 `sudo swapon /var/tmp/monoon-aot.swap`으로 기존 파일을 활성화한다.
