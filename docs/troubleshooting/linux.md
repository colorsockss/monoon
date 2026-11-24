# Linux x64: X11 창 초기화와 HUD 그리기

검증 환경은 Docker가 아니라 이 Mac에 보존한 **QEMU Debian 12.15 amd64**다. [실행 기록](../../engine/tests/results/keyroom-desktop/yaml-linux-x64/results.json)의 커널은 6.1.0-54-cloud-amd64, glibc는 2.36이다. 게임은 X11 가상 화면(Xvfb 1280×900×24)에서 SDL 기본 렌더러와 더미 오디오로 실행했다. 실제 GPU, Wayland 세션, 소리 출력을 검증한 기록은 아니다.

## 에디터는 지원하지 않음

MoNoon은 Linux용 에디터를 배포하거나 지원하지 않는다(사용자 결정, 2026-11-23). Linux x64는 게임 내보내기 대상(Release·Native AOT, `tar.gz` 패키지)과 CI의 헤드리스 빌드·리플레이에만 쓴다. 에디터 Scene 편집이나 Play 문제는 [macOS](macos.md) 또는 [Windows](windows.md)에서 재현한다. 2026-11-23 이전의 Linux 에디터 검증 기록은 `engine/tests/results/`에 그대로 남아 있다.

## 빌드된 게임

```sh
game=./game-builds/KeyRoom/KeyRoom-linux-x64
"$game/MoNoonPlayer" 2> /tmp/keyroom-player.log
```

- **`Permission denied` 또는 창이 안 뜸**: `ls -l "$game/MoNoonPlayer"`로 실행 비트를 확인한다. 실행 비트를 유지해 폴더 전체를 다시 복사한다. X11 세션 없이 SSH에서 실행한 경우에는 기존 VM의 [Xvfb 재검사 명령](README.md)을 사용한다. 검은 캡처만으로 HUD 문제라고 판단하지 않는다.
- **열쇠 획득/HUD 표시 중 `SDL_CreateTextureFromSurface … surface … invalid`**: [원래 실패 스택](../../engine/tests/results/keyroom-desktop/linux-original-crash.txt)은 PNG 로더가 아니라 `SdlText.Draw`를 가리킨다. 지금은 [TtfTextRasterizer](../../engine/src/Engine.Desktop/TtfTextRasterizer.cs)가 BRUTAL의 SDL_ttf로 배포한 TTF·OTF에서 글자를 RGBA 버퍼에 그리고, [SdlText](../../engine/src/Engine.Desktop/SdlText.cs)가 그 버퍼로 텍스처를 만든다. 글자 일부만 비면 `Game/`에 기본·대체 폰트가 들어 있는지 확인한다. 프로젝트 글꼴이 없는 게임은 `/usr/share/fonts`, `/usr/local/share/fonts`, `~/.local/share/fonts`에서 DejaVuSans·LiberationSans·NotoSansCJK를 찾고, 하나도 없으면 `No system font is available.`로 멈춘다.
- **라이브러리 문제와 구별**: 게스트에서 `ldd "$game/libSDL3.so.0"` 출력에 `not found`가 있는지 확인한다. 배포 파일이 오래됐으면 Mac에서 `./engine/scripts/build-players.sh linux-x64` 후 Linux x64 Release로 다시 내보낸다.
- **수정 확인**: 초기 화면만 보고 판단하지 않는다. 두 조작 방식으로 실제 열쇠를 먹고, HUD 글자와 둘째 방의 벽이 같이 그려지고, 리사이즈 후에도 글자가 남는지 확인한다. [열쇠 획득](../../engine/tests/results/keyroom-desktop/yaml-linux-x64/wasd/02-key.png) · [둘째 방](../../engine/tests/results/keyroom-desktop/yaml-linux-x64/wasd/03-room-two.png).

## InventoryLab 헤드리스 빌드·회귀

InventoryLab의 입력 리플레이는 디스플레이 없이 실행된다. 엔진 전체 회귀나 PlatformerLab에서 SkSL 셰이더의 픽셀도 그릴 때는 Linux의 오프스크린 Mesa 라이브러리 `libOSMesa.so.8`이 필요하다. Ubuntu·Debian 패키지는 `libosmesa6`다. SkiaSharp 2.88.9의 CPU 표면에서 런타임 셰이더를 그리던 경로가 Linux에서 `SIGILL`로 종료되어, 셰이더 시트를 구울 때 Mesa 컨텍스트를 사용한다.

보존한 Debian 12.15 amd64 VM에서 .NET SDK 10.0.101과 clang 14로 Release와 Native AOT 배포본을 직접 빌드했다. 두 실행 파일 모두 화면 없는 환경에서 Mac의 입력 475프레임을 재생했고, 게스트에서 꺼낸 두 저장 파일 모두 Mac 기준 171바이트와 일치했다. [게스트 빌드 명령](../../engine/tests/InventoryLab.Replay/README.md) · [실행 로그](../../engine/tests/results/input-replay-x64/linux-x64/build.log)

이 VM의 SDK는 `~/monoon-buildtools/dotnet`, 오프라인 NuGet 피드는 `~/monoon-buildtools/feed`, 빌드와 소스는 `~/inventory-replay-check`에 있다. 재실행 셸에서 `DOTNET_ROOT`, `PATH`, `RestoreSources`에 이 위치를 지정한다. 메모리 3GB로 AOT를 빌드할 때 사용한 스왑 파일은 `/var/tmp/monoon-aot.swap`이다. VM 재부팅 후 필요하면 `sudo swapon /var/tmp/monoon-aot.swap`으로 기존 파일을 활성화한다.
