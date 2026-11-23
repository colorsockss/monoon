# BRUTAL SDL 2026.10.3 전환

2026-11-23, macOS 27.0 / Apple Silicon arm64. `Ahwoo.BRUTAL.Sdl`을 2026.10.2에서 2026.10.3으로 올렸다. 다음 작업(오디오를 `Brutal.Sdl.Mixer`로, 게임 텍스트를 `Brutal.Sdl.Ttf`로 전환)에 필요한 두 패키지 2026.10.3도 참조에 추가했다. 오디오·텍스트 코드는 바꾸지 않았다. 측정하는 동안 이 Mac에서 QEMU Debian 게스트가 실행 중이었다. 시간 측정은 하지 않았고 바이트 비교와 크기만 기록했다.

## 패키지와 참조

| 패키지 | 버전 | 참조 방식 |
| --- | --- | --- |
| `Ahwoo.BRUTAL.Sdl` | 2026.10.2 → 2026.10.3 | [Engine.Desktop](../../../src/Engine.Desktop/Engine.Desktop.csproj) 직접 참조 |
| `Ahwoo.BRUTAL.Sdl.Native` | 2026.10.3 (새 패키지) | Sdl이 가져온다. SDL3 네이티브 라이브러리가 이 패키지로 옮겨졌다 |
| `Ahwoo.BRUTAL.Sdl.Mixer` (+ `.Native`) | 2026.10.3 | 컴파일 참조만 둔다: `ExcludeAssets="runtime;native"` |
| `Ahwoo.BRUTAL.Sdl.Ttf` (+ `.Native`) | 2026.10.3 | 컴파일 참조만 둔다: `ExcludeAssets="runtime;native"` |
| 함께 올라간 전이 패키지 | Logging 2026.10.3, Interop 2026.10.2, Strings 2026.10.2 | Core 2026.10.4는 Box2D가 이미 가져왔다 |

Mixer와 Ttf는 아직 코드에서 쓰지 않는다. 관리 어셈블리나 네이티브 라이브러리를 배포본에 넣지 않기 위해 런타임·네이티브 자산을 제외했다. Release 템플릿의 `MoNoonPlayer.deps.json`에도 두 패키지가 나타나지 않는다. 오디오·텍스트 전환 작업에서 해당 줄의 `ExcludeAssets`를 지우면 된다. 그때 늘어나는 네이티브 크기는 Mixer osx-arm64 341 KB, linux-x64 404 KB, win-x64 281 KB, Ttf 각각 2.06 MB, 2.37 MB, 1.59 MB다. 내려받은 것은 위의 `Ahwoo.BRUTAL.*` 패키지뿐이다.

## SDL 버전 (RID별)

| RID | 2026.10.2 `Ahwoo.BRUTAL.Sdl` | 2026.10.3 `Ahwoo.BRUTAL.Sdl.Native` | 배포본에 들어가는 파일 |
| --- | --- | --- | --- |
| osx-arm64 | `libSDL3.dylib` 3.4.12 (`SDL-3.4.12-release-3.4.12`), SHA-256 `f88320cbab4d…` | 같은 파일(바이트 동일) | BRUTAL 빌드 그대로. 변화 없음 |
| linux-x64 | `libSDL3.so.0.4.12` 3.4.12, `98ef6fe5f8cb…` | 같은 파일(바이트 동일) | BRUTAL 빌드 그대로. 변화 없음 |
| win-x64 | `SDL3.dll` 3.4.12, `4fdd311f65d2…`, VCRUNTIME140 가져옴 | 3.4.12로 다시 빌드한 파일 `13f3171496e9…`, VCRUNTIME140 가져옴 | 교체한 공식 SDL 3.4.12(`SDL-release-3.4.12-0-gf87239e71 (libsdl.org)`, `2a2704678bf6…`). 변화 없음 |

세 RID 모두 3.4.12여서 플레이어가 읽는 SDL 버전은 그대로다. 에디터 Scene은 계속 [PR 16658 패치](../../../native/macos/SDL-3.4.12-pr16658.patch)를 적용한 3.4.12(`BrutalEditor-PR16658`, `c7cd6e0a09d7…`)를 쓴다. 새로 빌드한 `editor/MoNoon.app`의 두 `libSDL3` 파일 모두 이 해시였다. 패치 빌드와 BRUTAL 빌드는 같은 SDL 3.4.12 ABI다.

## Windows DLL 교체

[Directory.Build.targets](../../../Directory.Build.targets)는 `NuGetPackageId == 'Ahwoo.BRUTAL.Sdl'`인 `runtimes/win-x64/native/SDL3.dll`을 찾아 교체했다. 2026.10.3에서는 이 파일의 패키지가 `Ahwoo.BRUTAL.Sdl.Native`로 바뀌어 그대로 두면 교체가 조용히 빠진다. 패키지 안 경로는 같으므로 패키지 ID만 바꿨다. 다시 패키지 구성이 바뀌어 교체가 빠지는 일을 막기 위해, win-x64 게시에 `Brutal.Sdl`이 있는데 교체 대상이 없으면 오류를 낸다. 패키지 ID를 일부러 틀리게 바꿔 win-x64 게시가 이 오류로 멈추는 것을 확인한 뒤 되돌렸다.

이 Mac에서 만든 win-x64 Release 템플릿의 `SDL3.dll`은 `2a2704678bf6…`(교체본)이다. 빌드 중간 출력(`release_win-x64`)도 같다. `llvm-objdump`가 없어 Xcode의 `objdump -p`로 가져오는 DLL을 확인했다.

| DLL (2026.10.3) | VC 런타임 의존성 | 나머지 |
| --- | --- | --- |
| `SDL3.dll` (Sdl.Native) | VCRUNTIME140.dll | Windows 시스템 DLL, UCRT(`api-ms-win-crt-*`) |
| `SDL3_mixer.dll` (Mixer.Native, SDL_mixer 3.2.4) | VCRUNTIME140.dll | KERNEL32, SDL3.dll, UCRT |
| `SDL3_ttf.dll` (Ttf.Native, SDL_ttf 3.2.2) | VCRUNTIME140.dll, VCRUNTIME140_1.dll | GDI32, USER32, RPCRT4, USP10, SDL3.dll, UCRT |
| 교체용 공식 `SDL3.dll` | 없음 | Windows 시스템 DLL만 |

Release 템플릿(.NET 자체 포함 게시)에는 `vcruntime140*.dll`이 없다. 따라서 Mixer·Ttf를 실제로 쓸 때는 Windows에서 같은 문제가 생긴다. 공식 빌드로 교체할지, VC 런타임을 함께 넣을지는 해당 작업에서 정하고 기록한다. 지금은 두 DLL이 배포본에 들어가지 않는다. Windows에서 실제로 실행하는 검증은 보류 상태다.

## 라이선스 고지

`$(PkgAhwoo_BRUTAL_Sdl)/LICENSES/**`는 2026.10.3에서도 `BRUTAL-SDL-THIRD-PARTY-NOTICES.txt`를 찾는다. Sdl.Native 패키지의 같은 이름 파일과는 둘째 줄의 패키지 이름만 다르고 SDL 고지 본문은 같으므로 기존 경로를 유지했다. `LICENSE.txt`, `NOTICE.txt`는 바이트 단위로 같고, `CONTRIBUTORS.md`는 469 → 448바이트로 갱신됐다. 세 Release 템플릿과 KeyRoom·PlatformerLab의 Release·Native AOT 출력 모두 `Licenses/BRUTAL/`에 SDL·Box2D 고지, `LICENSE.txt`, `NOTICE.txt`, `CONTRIBUTORS.md`가 있다. win-x64에는 교체한 SDL의 `Licenses/SDL/LICENSE.txt`도 그대로 있다.

## 크기 (바이트, 심볼릭 링크 제외)

| 출력 | 전 | 후 | 차이 | 바뀐 파일 |
| --- | --- | --- | --- | --- |
| KeyRoom Release `KeyRoom.app` | 97,446,773 | 97,447,696 | +923 | `Brutal.Strings.dll` +512, `deps.json` +438, 고지 −27 |
| KeyRoom Native AOT `KeyRoom.app` | 17,928,145 | 17,928,118 | −27 | 고지만. 실행 파일 크기 동일 |
| PlatformerLab Release | 95,273,297 | 95,274,220 | +923 | KeyRoom과 같음 |
| PlatformerLab Native AOT | 15,582,461 | 15,582,434 | −27 | 고지만 |
| Release 템플릿 osx-arm64 | 98,823,132 | 98,824,239 | +1,107 | 위 파일과 pdb |
| Release 템플릿 win-x64 | 89,560,435 | 89,561,542 | +1,107 | 위와 같음 |
| Release 템플릿 linux-x64 | 102,027,989 | 102,029,096 | +1,107 | 위와 같음 |

파일 수는 모든 출력에서 그대로다. SDL 네이티브 파일과 `Brutal.Sdl.dll` 크기는 변하지 않았다(`Brutal.Sdl.dll`의 해시는 달라짐).

## 회귀 (macOS arm64)

| 검사 | 대상 | 결과 |
| --- | --- | --- |
| `./monoon test` | 전체 엔진 검사 | 통과 (`All checks passed.`) |
| `engine/scripts/build-players.sh` | 세 Release 템플릿 + Native AOT 키트 재생성 | 통과. 키트의 `Directory.Build.targets`·Engine.Desktop은 저장소와 동일 |
| `engine/scripts/build-editor-mac.sh` | `editor/MoNoon.app` | 통과. 패치한 SDL 3.4.12 포함 |
| [KeyRoom 조작키 설정](../../KeyRoom.Rebinding/README.md) | Release, Native AOT | 통과. 세 실행의 `.trace`, `.player.settings`가 업그레이드 전 Release와 바이트 동일 |
| [PlatformerLab 조인트](../../PlatformerLab.Joints/README.md) | Release, Native AOT | 통과. `trace.tsv` 80,853바이트·300행이 업그레이드 전과 바이트 동일 |
| PlatformerLab 조인트 | 에디터 Scene Play | 통과. 업그레이드 후 Scene Play·Release·Native AOT와 업그레이드 전 Release 네 기록이 동일 |
| 금지 파일 | 위 빌드 출력과 세 템플릿 | 통과. Engine.Authoring, Engine.Editor, SkiaSharp, Avalonia, `MoNoon.dll`, Mixer·Ttf 파일, vcruntime 없음 |

업그레이드 전 기준은 같은 작업 트리의 b524375에서 템플릿을 빌드해 같은 명령으로 만들었다. 실패나 예외는 없었다.

## 검증하지 않은 것

- Windows x64에서의 실행. 이 Mac에서는 win-x64 Release 템플릿의 파일 구성과 `SDL3.dll` 해시만 확인했다. Windows Native AOT는 이 Mac에서 빌드할 수 없다.
- Linux x64 게스트에서의 실행과 Native AOT 빌드. Native AOT 키트를 쓰는 게스트는 새 패키지(Sdl/Sdl.Native/Mixer/Ttf 2026.10.3과 전이 패키지)를 복원할 수 있어야 한다.
