# 빌드와 배포

[매뉴얼 목차](README.md)

## 편집 중 실행과 배포본 실행

| 목적 | 조작 | 실행하는 내용 |
| --- | --- | --- |
| 바로 편집 결과 확인 | **Play**, 종료는 **Stop / Esc** | 에디터 안에서 현재 씬의 플레이 상태를 만든다. 플레이어가 이동한 위치는 편집 좌표와 별개다. |
| 독립 SDL 창에서 확인 | **Run → Save & Run** | 현재 씬을 저장한 뒤 에디터를 통해 별도 게임 창에서 연다. |
| 다른 컴퓨터에 전달 | **Build → Build Game…** | 선택한 시작 씬과 포함 씬, 참조 에셋, 스크립트를 플랫폼별 게임으로 내보낸다. |

## Release로 내보내기

1. **Build → Build Game…**에서 **Start scene**을 선택하고 **Included scenes**에 시작 씬과 이동할 모든 씬을 체크한다. KeyRoom에는 **KeyRoom.scene**, **SecondRoom.scene**, **RoomGuide.scene**을 포함한다.
2. **Platform**에서 `osx-arm64`, `win-x64`, `linux-x64` 중 하나를 고르고 **Build type → Release**를 선택한다. **Save Settings**는 설정만 저장한다. **Build**는 기본 위치에, **Build To…**는 선택한 폴더에 출력한다. 세 버튼 모두 고른 값을 [프로젝트 빌드 설정](#프로젝트-빌드-설정)에 저장하고, 다음에 대화 상자를 열면 그 값으로 시작한다.
3. 저장소 안 샘플의 기본 출력은 `game-builds/<프로젝트 폴더명>/`이다. 일반 프로젝트에서는 프로젝트와 나란한 `game-builds/` 아래에 생성된다. 실제 게임 이름은 `GameSettings.gameTitle` 또는 프로젝트 폴더명에서 온다.
4. macOS에서는 게임 `.app` 전체를 옮겨 더블클릭한다. Windows에서는 출력 폴더를 통째로 복사하고 `MoNoonPlayer.exe`를 실행한다. Linux에서는 출력 폴더에서 `./MoNoonPlayer`를 실행한다. 실행 파일 옆 `Game/`에는 포함 씬과 게임 데이터가 있다. Mac에서는 앱 내부 `Contents/MacOS/Game/`에 해당한다.

Release 템플릿은 플랫폼별 .NET 런타임을 포함한 최적화 빌드다. 프로젝트의 컴포넌트와 공용 C# 파일은 [하나의 스크립트 어셈블리](scripting.md)로 컴파일해 포함한다. 각 플랫폼의 템플릿이 있으면 이 Mac에서 세 플랫폼용 Release를 내보낼 수 있다.

게임별 세이브와 사용자 설정은 운영체제의 사용자 데이터 폴더에 기록한다. 같은 게임의 Release와 Native AOT 사이에서 이어 할 수 있다. [데이터 에셋과 플레이어 저장](player-data.md)

## 프로젝트 빌드 설정

**Build Game…**의 **Platform**, **Build type**, **Profile**, **Package**, **Content**(Rebuild all content)는 프로젝트 루트의 `BuildSettings` 파일에 저장된다. 에디터와 `./monoon build`가 같은 파일을 읽는다. 시작 씬, 포함 씬, 번들, 아이콘은 지금처럼 `GameSettings`에 저장된다. 값은 CLI 인자와 같은 단어를 쓰는 YAML이다.

```yaml
target: osx-arm64
type: release
profile: distribution
content: incremental
package: true
```

파일이 없거나 키가 빠지면 호스트 플랫폼, `release`, `distribution`, `incremental`, 패키지 끔을 사용한다. `BuildSettings`는 빌드할 때만 읽는다. 확장자가 없어 에셋 목록에 들어가지 않고, 게임 데이터(`Game/`)와 배포 패키지에 복사되지 않으며, 플레이어는 이 파일을 읽지 않는다. 다른 OS용 Native AOT가 저장되어 있으면 대화 상자는 Platform을 바꿀 때처럼 **Release**를 표시한다. CLI는 `Build the win-x64 Native AOT player on a matching host.`로 종료 코드 1을 반환한다.

```sh
./monoon build samples/KeyRoom                       # 저장한 설정 그대로
./monoon build samples/KeyRoom native-aot            # 이번 실행만 Native AOT
./monoon build samples/KeyRoom --profile development # 이번 실행만 개발용, 저장된 패키지 설정은 끔
```

명령줄에 적은 빌드 종류와 옵션은 그 실행에만 저장값 대신 쓰이고, `BuildSettings` 파일은 바꾸지 않는다. `--profile development`만 적으면 대화 상자처럼 패키지를 끈다. `--profile development --package on`은 사용법 오류다. 출력 위치(`--output`, **Build To…**)는 저장하지 않는다. 같은 프로젝트와 설정이면 에디터와 CLI의 결과는 빌드 ID, 서명, Native AOT 링커 UUID를 빼고 바이트 단위로 같다. [검증 기록](../../engine/tests/results/editor-build/README.md)

## Native AOT로 내보내기

현재 컴퓨터와 같은 OS·아키텍처를 고르면 **Build type → Native AOT**가 나타난다. 사용 중인 C# 스크립트와 필드 연결 코드, VYaml 포매터를 플레이어에 함께 컴파일한다. 배포 데이터는 Release와 같은 YAML을 쓴다. 결과는 `NativeAOT/` 하위에 생성되어 일반 Release와 함께 보관할 수 있다. 빌드하는 컴퓨터에는 .NET 10 SDK와 해당 플랫폼의 네이티브 링커가 필요하다.

macOS arm64의 Native AOT 배포 프로필은 실행 파일을 크기 우선(`OptimizationPreference=Size`)으로 컴파일한다. 배포 게임은 BRUTAL의 SDL3 렌더러로 화면을 그린다. 스프라이트 마스크는 SDL로 읽은 RGBA 픽셀에 적용하고, 셰이더는 빌드 시 생성한 프레임 시트를 사용한다. 게임 텍스트는 BRUTAL의 SDL_ttf(`Brutal.Sdl.Ttf`, FreeType·HarfBuzz)로 배포한 TTF·OTF에서 실행 중에 그린다. 빌드 때 글리프를 미리 굽지 않으므로 새 입력이나 콘텐츠 업데이트로 추가된 글자도 같은 방식으로 그려진다. 배포본의 `Licenses/BRUTAL/BRUTAL-SDL-TTF-THIRD-PARTY-NOTICES.txt`에 SDL_ttf·FreeType·HarfBuzz·zlib 고지가 들어간다. SkiaSharp는 에디터와 빌드 도구에서만 사용한다. macOS에서 내용이 같은 두 SDL 라이브러리는 상대 심볼릭 링크로 연결한다. ZIP으로 전달할 때는 이 링크와 실행 권한을 보존하도록 다음처럼 압축한다.

```sh
./monoon build samples/KeyRoom native-aot
cd game-builds/KeyRoom/NativeAOT
ditto -c -k --keepParent KeyRoom.app KeyRoom.zip
```

게임 플레이어와 에디터·빌드 도구는 별도 어셈블리로 빌드한다. Release는 SDK가 대상 플랫폼에 대해 작성한 파일 목록과 빌드 시 컴파일한 게임 스크립트 DLL을 사용한다. Native AOT는 같은 스크립트를 실행 파일에 정적으로 연결한다. Roslyn, 프로젝트 변환기, 임포터, 빌더는 에디터와 CLI에 포함된다. 이전 템플릿을 사용하고 있다면 저장소 루트에서 `./engine/scripts/build-players.sh`로 갱신한다.

## 개발용·배포용 프로필과 디버그 심볼

CLI의 `--profile`로 심볼을 보관할 위치를 고른다. 두 프로필 모두 최적화된 Release 구성으로 컴파일한다. 기본값은 `distribution`이다. 에디터의 **Build Game…**에서는 **Profile**로 고른다.

| 프로필 | macOS Release 앱 | macOS Native AOT 앱 | 심볼 위치 |
| --- | --- | --- | --- |
| `development` | `Development/InventoryLab.app` | `Development/NativeAOT/InventoryLab.app` | 앱 안의 실행 파일과 DLL 곁 |
| `distribution` | `InventoryLab.app` | `NativeAOT/InventoryLab.app` | 출력 폴더 아래 `Symbols/<게임>/<대상 RID>/<release 또는 native-aot>/<빌드 ID>/` |

표의 경로는 출력 폴더 기준이다. InventoryLab의 기본 출력 폴더는 `game-builds/InventoryLab/`이다. 배포용 앱을 다시 빌드하면 새 ID로 심볼을 보관하므로 이전 버전에서 발생한 오류도 추적할 수 있다. 앱을 전달할 때는 해당 앱만 복사한다.

```sh
./monoon build samples/InventoryLab release --profile development
./monoon build samples/InventoryLab native-aot --profile development
./monoon test inventorylab game-builds/InventoryLab/Development --profile development

./monoon build samples/InventoryLab release --profile distribution
./monoon build samples/InventoryLab native-aot --profile distribution
./monoon test inventorylab game-builds/InventoryLab --profile distribution
```

특정 배포본을 진단할 때는 실행 파일 옆의 `MoNoonBuild`에서 `buildId`, `target`, `mode`를 읽고 같은 ID의 심볼 폴더를 연다. Mac 앱의 기록은 `Contents/MacOS/MoNoonBuild`에 있다. 심볼 폴더에도 같은 기록이 들어 있다. 이 파일에는 실행 파일의 SHA-256과 Release DLL에 대응하는 PDB ID가 기록된다.

Release는 엔진·플레이어·게임 스크립트의 portable PDB를 저장한다. macOS Native AOT의 네이티브 주소를 해석할 때는 `MoNoonPlayer.dSYM`을 사용한다. `dwarfdump --uuid`에 실행 파일과 dSYM을 함께 넘겨 두 UUID를 대조할 수 있다. Windows Native AOT의 네이티브 심볼은 `.pdb`, Linux에서는 `.dbg`다. [InventoryLab 심볼과 소스 줄 검증](../../engine/tests/results/build-profiles/README.md)

## 서명과 배포 패키지

배포용 프로필로 빌드한 게임은 서명과 압축 단계를 이어서 실행할 수 있다. CLI에서는 `--package on`을 붙인다. 에디터에서는 **Build Game…**의 **Profile → Distribution**을 고르고 **Package → Sign and package**를 체크한다. 개발용 프로필에서는 이 단계를 실행하지 않는다.

```sh
./monoon build samples/KeyRoom release --package on
./monoon build samples/KeyRoom native-aot --package on
./monoon build samples/KeyRoom release --target win-x64 --package on
./monoon build samples/KeyRoom release --target linux-x64 --package on
```

패키지는 게임 폴더나 `.app` 옆에 만든다. Native AOT 패키지는 `NativeAOT/` 아래에 생성된다. 각 단계의 결과와 건너뛴 이유는 CLI 표준 오류와 에디터 Console에 기록된다.

| 대상 | 실행하는 단계 | 결과 |
| --- | --- | --- |
| `osx-arm64` | `codesign` 서명(강화된 런타임), 설정한 경우 `notarytool` 공증과 `stapler` | `<게임>-osx-arm64.zip`, `<게임>-osx-arm64.dmg` |
| `win-x64` | Windows에서 인증서를 설정한 경우 `signtool`로 `MoNoonPlayer.exe` 서명 | `<게임>-win-x64.zip` |
| `linux-x64` | 실행 권한을 보존한 압축, `.desktop` 항목, 아이콘, 메뉴 등록 스크립트 | `<게임>-linux-x64.tar.gz` |

**macOS.** 서명할 때 `Contents/MacOS`의 `Game`, `EngineAssets`, `Licenses` 폴더를 `Contents/Resources`로 옮기고 같은 이름의 상대 링크를 남긴다. 플레이어가 읽는 경로는 그대로다. 안쪽 dylib·DLL·빌드 기록을 먼저 서명한 뒤 앱 묶음을 서명하고 `codesign --verify --deep --strict`로 확인한다. Release는 CoreCLR JIT를 위해 `com.apple.security.cs.allow-jit` 권한을 받는다. Native AOT는 추가 권한이 필요 없다. Developer ID를 지정하지 않으면 애드혹(`-`)으로 서명한다. 이 경우 팀 ID가 없는 라이브러리를 읽도록 `disable-library-validation`을 추가한다. 애드혹 앱은 빌드한 Mac에서 실행할 수 있다. 다른 Mac에 배포하려면 Developer ID로 서명하고 공증한다. 앱 안의 DLL 서명은 확장 속성에 저장되므로 ZIP은 Finder나 `ditto -x -k`로 풀어야 한다. `unzip`으로 풀면 서명 검사가 실패한다. DMG는 이 문제가 없다.

빌드 기록 `MoNoonBuild`의 `playerSha256`은 서명 전 실행 파일의 값이다. 서명한 빌드에는 `codeSignature`(`macos-adhoc`, `macos-developer-id`, `windows-authenticode`)가 추가된다. 보관한 심볼 폴더의 기록도 같은 내용으로 갱신된다.

**Windows.** 서명은 Windows SDK의 `signtool.exe`가 PATH에 있는 Windows에서만 실행한다. Mac·Linux에서 내보내거나 인증서를 지정하지 않으면 서명하지 않은 ZIP을 만든다. 설치 프로그램은 만들지 않는다. 설치 프로그램 단계를 추가하려면 WiX Toolset 또는 Inno Setup이 빌드 컴퓨터에 설치되어 있어야 한다. 시작 메뉴 바로 가기, 설치 경로, 제거 항목을 정의하고, 생성한 설치 파일에도 같은 인증서로 서명해야 한다.

**Linux.** 압축에는 게임 폴더와 `<게임>.desktop`, `<게임>.png`, `install-desktop-entry.sh`가 들어 있다. 아이콘은 **Build Game… → Icon**에서 프로젝트의 PNG를 고르며 `GameSettings`의 `icon`에 저장된다. 아이콘은 패키지에만 포함되고 게임 데이터에는 들어가지 않는다. 압축을 푼 폴더에서 `./install-desktop-entry.sh`를 실행하면 `~/.local/share/applications/`에 해당 폴더의 절대 경로를 가리키는 메뉴 항목을 등록한다. 게임은 `./MoNoonPlayer`로 바로 실행할 수도 있다.

### 서명 설정과 비밀 정보

서명 설정은 프로젝트가 아닌 사용자 설정 파일에서 읽는다. 기본 위치는 macOS `~/Library/Application Support/MoNoon/signing.yaml`이고, 환경 변수 `MONOON_SIGNING_SETTINGS`로 다른 경로를 지정할 수 있다. 파일이 없으면 애드혹 서명만 실행하고 공증과 `signtool`은 건너뛴다.

```yaml
macIdentity: "Developer ID Application: Example Games (TEAMID1234)"
notaryKeychainProfile: monoon-notary
windowsCertificateThumbprint: 0123456789ABCDEF0123456789ABCDEF01234567
# 인증서 저장소 대신 PFX를 쓸 때
windowsPfx: /Users/me/Secrets/game.pfx
windowsPfxPasswordFile: /Users/me/Secrets/game-pfx-password.txt
windowsTimestampUrl: http://timestamp.digicert.com
```

공증 자격 증명은 키체인에 저장하고 프로필 이름만 적는다. `xcrun notarytool store-credentials monoon-notary`로 만든다. PFX 비밀번호는 환경 변수 `MONOON_WINDOWS_PFX_PASSWORD` 또는 `windowsPfxPasswordFile`이 가리키는 파일에서 읽는다. 설정 파일, PFX, 비밀번호 파일이 프로젝트, 프로젝트가 속한 Git 저장소 또는 빌드 출력 폴더 안에 있으면 빌드가 실패한다. 빌더는 도구에 전달한 인수를 기록하지 않는다. 도구 출력에 비밀번호가 나타나면 `********`로 바꾼 뒤 기록한다. [KeyRoom 서명·패키지 검증](../../engine/tests/results/signing-packages/README.md)

## 크래시 진단

콘텐츠 버전 35의 독립 플레이어는 게임 실행을 끝내는 C# 예외를 게임별 사용자 프로필의 `Crashes/*.crash`에 YAML로 기록한다. 일반 실행·입력 기록·리플레이·스모크 실행에서 같은 형식을 쓴다. 보고서에는 UTC 시각, OS·CPU, 씬 ID, 실행 방식, 빌드 기록, 최초 예외부터 바깥 예외까지의 메시지와 스택이 들어 있다. 터미널로 실행하면 저장 경로도 표준 오류에 출력된다. macOS의 기본 위치는 `~/Library/Application Support/MoNoon/Games/<게임 ID 32자리>/Crashes/`이며 `MONOON_USER_DATA`를 지정한 경우 그 폴더 아래에서 찾는다. SDL·네이티브 라이브러리의 프로세스 신호 오류는 macOS의 시스템 진단 보고서를 사용한다.

보고서의 `build.values`에서 게임명, `target`, `mode`, `buildId`를 확인하고 해당 빌드의 심볼 폴더를 지정한다. 개발용 프로필은 `MoNoonBuild`와 심볼이 같이 있는 앱의 바이너리 폴더를 지정한다. 아래 예시는 보관한 InventoryLab 보고서를 실제 배포 심볼로 읽는다.

```sh
reports=engine/tests/results/crash-diagnostics
symbols=game-builds/InventoryLab/Symbols/InventoryLab/osx-arm64
./monoon crash explain "$reports/release.crash" "$symbols/release/7c99a497b83b48b1ba346fbb3142a2f5"
./monoon crash explain "$reports/native-aot.crash" "$symbols/native-aot/959313df7a7340f69cf84d922d4c4ce7"
```

Release에서는 모듈 ID와 portable PDB를 확인하고 메서드 토큰·IL 오프셋을 소스 줄로 해석한다. macOS arm64 Native AOT에서는 dSYM의 UUID를 대조하고 `atos`로 보고서의 이미지 상대 주소를 해석한다. 첫 예외 블록 맨 위에서 오류 지점을 보고, 이어지는 프레임에서 호출한 스크립트와 엔진 콜백을 확인한다. 실행 파일과 심볼을 보관한 상태에서 다음 명령으로 같은 실패와 복구를 다시 시험할 수 있다. [원본 보고서와 해석 결과](../../engine/tests/results/crash-diagnostics/README.md)

```sh
python3 engine/tests/InventoryLab.Crashes/check.py game-builds/InventoryLab
```

## 터미널에서 빌드와 회귀 검사

저장소 루트에서 다음 명령 하나로 InventoryLab을 검사한다. .NET 10 SDK와 호스트용 네이티브 툴체인을 사용한다. 이 Mac에서는 Xcode 명령줄 도구를 사용한다.

```sh
./monoon ci
```

이 명령은 호스트용 Release 템플릿과 Native AOT 키트를 소스에서 갱신하고, InventoryLab을 두 형식으로 내보낸 뒤 각 플레이어를 직접 실행한다. 에디터 앱이나 미리 빌드한 템플릿이 없는 체크아웃에서도 같은 명령을 쓴다. macOS 결과는 **game-builds/InventoryLab/InventoryLab.app**과 **game-builds/InventoryLab/NativeAOT/InventoryLab.app**이다.

| 하고 싶은 일 | 명령 |
| --- | --- |
| 저장한 빌드 설정으로 빌드 | `./monoon build samples/KeyRoom` |
| Release만 빌드 | `./monoon build samples/InventoryLab release` |
| Native AOT만 빌드 | `./monoon build samples/InventoryLab native-aot` |
| 대상과 출력 위치를 지정 | `./monoon build samples/InventoryLab release --target win-x64 --output game-builds/InventoryLab` |
| 서명하고 배포 패키지 생성 | `./monoon build samples/InventoryLab release --package on` |
| 이미 만든 두 InventoryLab 빌드 검사 | `./monoon test inventorylab game-builds/InventoryLab` |
| 기록된 약초 의뢰를 두 빌드에서 창 없이 재생하고 원본 저장과 비교 | `./monoon test replay game-builds/InventoryLab` |
| 프로젝트의 게임 스크립트 테스트 실행 | `./monoon test project samples/KeyRoom` |
| 엔진 전체 회귀 검사 | `./monoon test` |

**build**는 저장된 프로젝트의 **GameSettings**에서 시작 씬, 포함 씬과 게임 이름을 읽고, 빌드 종류와 옵션은 [BuildSettings](#프로젝트-빌드-설정)에서 읽는다. 명령줄에 적은 값이 우선한다. 둘 다 없으면 호스트 RID의 Release 배포용 빌드다. Release는 대상 플랫폼의 템플릿을 사용하고, Native AOT는 같은 OS·아키텍처에서 빌드한다. 깨끗한 체크아웃에서 개별 빌드 명령을 쓸 때는 먼저 `./engine/scripts/build-players.sh osx-arm64`처럼 필요한 템플릿을 만든다. 전체 회귀 검사는 세 대상의 내보내기까지 검사하므로 처음에는 `./engine/scripts/build-players.sh`로 세 템플릿을 준비한다.

**test inventorylab**은 패키지 검사 후 Release와 Native AOT의 실제 실행 파일을 각각 세 번 시작한다. 플레이어의 `--smoke-test` 옵션은 지정한 테스트 폴더를 저장 슬롯으로 사용하고 프로젝트에 바인딩된 키를 고정 60Hz로 누르고 뗀다. 창을 만들지 않고 시작 씬, 내보낸 스크립트와 데이터, 저장·재실행을 검사한다. 같은 단계의 YAML을 예상값과 대조한 뒤 두 빌드의 저장 바이트도 비교한다. 각 실행은 별도 임시 프로필을 사용한다.

소스 체크아웃에서 **monoon**은 macOS·Linux 셸 진입점이다. .NET에서 빌더를 직접 호출할 때는 `dotnet run --project engine/src/Engine.Cli -c Release -- build samples/InventoryLab release`처럼 같은 인자를 사용한다. 성공은 종료 코드 0, 빌드·검사 실패는 1, 잘못된 CLI 사용법은 2다.

## 게임 스크립트 테스트

`./monoon test project <프로젝트 폴더>`는 프로젝트의 `Tests/` 폴더에 있는 C# 테스트를 `Scripts/`의 게임 스크립트와 함께 컴파일한다. 테스트마다 빈 임시 사용자 저장 폴더를 만들고 지정한 씬을 창 없이 연다. 입력은 `.inputreplay` 재생과 같은 입력 라우터로 넣고, 프레임마다 UI 시간 1/60초와 게임 틱 하나를 진행한다. 테스트 작성법은 [게임 스크립트 테스트](scripting.md#게임-스크립트-테스트)에서 설명한다.

```text
$ ./monoon test project samples/KeyRoom
PASS ChestTests.InteractSpawnsOneCoin
...
PASS DoorTests.OpenDoorLeadsToSecondRoom
9 passed, 0 failed.
```

모든 테스트가 통과하면 종료 코드 0, 하나라도 실패하거나 컴파일 오류가 있거나 테스트가 없으면 1을 반환한다. 실패한 테스트는 실패한 프레임 번호(1부터 시작)와 게임 틱, 첫 예외, 프로젝트 파일의 줄 위치, 마지막 게임 로그 다섯 줄을 출력한다. 게임 스크립트에서 난 예외도 같은 형식으로 표시된다. 시간 제한은 기본 3600프레임이며 `[GameTest("Scenes/KeyRoom.scene", MaxFrames = 600)]`처럼 바꾼다.

```text
FAIL DeliberateFailure.HintAfterFourTenths (Scenes/KeyRoom.scene) at frame 38, tick 38
  GameTestFailure: the hint is visible 0.4 s after the door opens
    at DeliberateFailure.HintAfterFourTenths(GameTestRun run)+MoveNext() in ./Tests/DeliberateFailure.cs:line 14
  log: Key acquired.
  log: Door received KeyAcquired.
```

`Tests/`는 게임 빌드에 들어가지 않는다. Release·Native AOT 빌더는 `Scripts/`와 Project 패널에서 옮긴 게임 스크립트만 컴파일하고, `Tests/` 아래의 `.cs`는 `.cs.meta`가 있어도 게임 스크립트로 읽지 않는다. 엔진 전체 회귀 검사 `./monoon test`도 KeyRoom의 테스트를 실행한다. [KeyRoom 테스트 실행과 빌드 출력 검사](../../engine/tests/results/project-tests/README.md)

## 헤드리스 입력 리플레이

플레이어의 `--record-input <기록 파일> <저장 폴더>`로 게임 창을 열고 평소처럼 조작한다. Esc로 종료하거나 창을 닫으면 입력과 프레임 시간을 확장자 `.inputreplay`의 YAML 파일에 기록한다. 기록 파일에는 새 이름을 사용하고, 처음부터 진행하는 테스트는 빈 저장 폴더에서 시작한다.

키를 누른 직후의 화면을 검사하려면 기록을 시작할 때 `MONOON_KEY_FRAME_DIR`을 새 출력 폴더로 지정한다. 플레이어는 키를 처음 누른 이벤트를 처리한 프레임의 SDL 화면을 `<frame>.png`로 저장한다. 파일명의 숫자는 입력 기록에 있는 0부터 시작하는 `frame` 값과 같다.

```sh
run=$(mktemp -d)
player=game-builds/InventoryLab/InventoryLab.app/Contents/MacOS/MoNoonPlayer
native=game-builds/InventoryLab/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer

"$player" --record-input "$run/Herbalist.inputreplay" "$run/record"
"$player" --replay-input "$run/Herbalist.inputreplay" "$run/release"
"$native" --replay-input "$run/Herbalist.inputreplay" "$run/aot"
cmp "$run/record/slot-001.save" "$run/release/slot-001.save"
cmp "$run/record/slot-001.save" "$run/aot/slot-001.save"
```

InventoryLab에서는 **T → 1 → 1 → S**로 의뢰를 받고 저장한다. **1**로 대화를 닫고 **1**을 세 번 더 눌러 약초를 모은 뒤 **S**로 저장한다. **T → 2 → 1 → S**로 수정 보상을 받아 마지막으로 저장한다. 위 예제는 기록과 두 번의 재생에 각기 다른 저장 폴더를 쓴다.

`--replay-input`은 지정한 프로젝트와 시작 씬을 확인하고 화면이나 오디오 장치를 열지 않고 실행한다. 키 누름·해제, 논리 좌표의 포인터·클릭·휠, 게임패드 값, 텍스트 조합·확정, 포커스 초기화를 기록한 순서대로 처리한다. 입력이 없는 프레임도 보관해 원래 프레임 간격으로 UI와 60Hz 고정 업데이트를 진행한다. 기록에는 저장 데이터가 들어 있지 않으므로, 기존 슬롯을 불러오는 시나리오는 재생 폴더에 같은 초기 슬롯을 준비한다. 실제 오디오 출력 시계나 비동기 작업 완료 시점처럼 입력 이외의 외부 상태가 필요한 시나리오는 별도로 검증한다.

이미 빌드된 InventoryLab에서는 `./monoon test replay game-builds/InventoryLab` 한 줄로 저장소의 기록을 재생한다. Release·Native AOT가 각각 만든 저장 파일 전체를 실제 기록 시점의 저장 파일과 대조하고, 다르면 종료 코드 1을 반환한다. `./monoon ci`에도 이 검사가 포함된다. [기준 입력](../../engine/tests/fixtures/InventoryLab/Herbalist.inputreplay) · [기준 저장](../../engine/tests/fixtures/InventoryLab/Herbalist.save) · [이 Mac의 검증 기록](../../engine/tests/results/input-replay/README.md) · [Windows·Linux x64 게스트의 네 배포본 검증](../../engine/tests/results/input-replay-x64/README.md)

## 콘텐츠 증분 빌드와 재빌드 사유

같은 프로젝트를 같은 출력 폴더로 다시 빌드하면 에디터와 CLI가 셰이더 프레임 시트, 스트리밍 타일 청크, 콘텐츠 번들을 재사용한다. **Build → Build Game…**에서 빌드한 뒤 **Console**을 열면 각 항목의 `build` 또는 `reuse`와 이유가 표시된다. **Rebuild all content**를 선택하면 모든 항목을 다시 생성한다.

| 생성물 | 다시 만드는 입력 |
| --- | --- |
| 셰이더 프레임 시트 | 셰이더 소스·프레임 수·크기·FPS. 내장 빛과 파티클 시트도 각각 추적한다. |
| 타일 청크 | 해당 청크의 셀, 인접 청크와 접하는 한 셀 너비의 경계, 타일셋 정의, 그리드 레이아웃. 이웃 청크의 오토타일 연결도 반영한다. |
| 콘텐츠 번들 | 포함한 파일의 경로와 배포 바이트 해시. 변경 이유에 추가·수정·제거된 경로가 표시된다. |

CLI 기본값은 `--content incremental`이다. 다음 명령을 실행하고 InventoryLab의 약초상 인사말을 수정한 뒤 첫 번째 명령을 반복해 본다. `Assets/Dialogue` 번들의 변경 이유에 `Assets/Dialogue/Herbalist.data`가 나타난다. 두 번째 명령은 같은 소스로 전체 빌드를 만든다.

```sh
./monoon build samples/InventoryLab release --output game-builds/InventoryLab
./monoon build samples/InventoryLab release --output game-builds/InventoryLab --content full
```

캐시는 출력 폴더 아래 `.monoon-build-cache`에 저장한다. 프로젝트 ID, 빌드 호스트, 대상 플랫폼, Release·Native AOT, 프로필별로 구분한다. 생성기 버전도 입력에 포함한다. 재사용할 파일의 SHA-256이 기록과 다르거나 파일이 없으면 다시 생성한다. 성공한 마지막 빌드의 `report.yaml` 경로는 CLI와 에디터 Console에 표시된다. 모든 빌드가 끝난 뒤 캐시 폴더를 삭제하면 다음 빌드가 처음부터 생성한다.

플레이어 스테이징, 스크립트 컴파일, 참조 검사와 배포 교체는 빌드마다 실행한다. 따라서 생성물 재사용 개수는 해당 빌드가 건너뛴 콘텐츠 생성 작업의 수다. [셰이더·청크를 포함한 InventoryLab 실습](../../engine/tests/InventoryLab.Incremental/README.md) · [플랫폼별 검증](../../engine/tests/results/incremental-content/README.md)

## 콘텐츠 번들

**Build → Build Game… → Bundles**에 프로젝트 루트 기준 폴더 경로를 한 줄에 하나씩 입력한다. **Save Settings**로 저장하면 에디터와 CLI의 Release·Native AOT 빌드에 함께 적용된다. 비워 두면 에셋을 기존 폴더 구조로 내보낸다. InventoryLab의 설정은 다음과 같다.

```yaml
contentBundles:
  - Assets/Dialogue
  - Assets/Quests
  - Assets/Market
```

`Assets` 또는 `Scenes` 아래에서 빌드할 씬이 참조하는 에셋이 있는 폴더를 고른다. 부모·자식 폴더를 겹쳐 지정하거나 내보낼 에셋이 없는 폴더를 지정하면 빌드가 실패한다. 각 파일의 소속은 내보내는 경로로 결정되며, 번들 사이의 참조는 기존 에셋 GUID를 그대로 사용한다.

`./monoon build samples/InventoryLab release`를 실행하면 플레이어 옆 `ContentBundles/Assets/`에 대화·의뢰·시장 배경음 번들이 생긴다. InventoryLab의 시작 씬은 `MarketMorning.wav`를 음악 버스에서 반복 재생한다. 카탈로그와 모든 번들을 앱에 함께 배포한다.

### 대형 번들 로딩과 시작 진단

기본 플레이어는 첫 실행에서 게임 프로필의 `BundleCache/v1/Game`에 번들을 풀고 원본과 에셋의 SHA-256을 확인한다. 성공한 뒤에만 완료 표시를 기록한다. 다음 실행에서는 배포 아카이브, 캐시 에셋, 씬과 스크립트를 포함한 개별 파일을 다시 확인하고 압축 해제를 생략한다. 파일이 추가·변경·누락됐거나 지난 실행의 준비가 중단됐다면 다시 만든다. 배포 원본의 해시가 맞지 않으면 시작 오류를 보고한다.

캐시는 게임당 한 벌을 보관한다. 같은 게임의 두 번째 실행, 활성 모드나 콘텐츠 업데이트가 있는 실행은 독립된 임시 폴더를 쓰고 정상 종료 시 지운다. 게임 종료 후 프로필의 `BundleCache` 폴더를 지우면 다음 실행에서 자동으로 다시 만든다. `MONOON_BUNDLE_CACHE=off`를 지정하면 기존 임시 폴더 경로로 성능을 비교할 수 있다.

macOS에서 새 CSV 경로를 지정해 앱을 실행하고 첫 화면에서 `1`을 눌러 약초를 담는다. 120프레임 이상 지난 뒤 Esc로 종료한다. 창이 일찍 닫히면 그때까지의 프레임 수와 최댓값이 기록된다.

```sh
MONOON_STARTUP_TRACE="$PWD/inventory-startup.csv" \
  game-builds/InventoryLab-Bundles/InventoryLab.app/Contents/MacOS/MoNoonPlayer
```

| CSV 이벤트 | 읽는 법 |
| --- | --- |
| `content_begin` → `content_ready` | 업데이트 조회부터 번들 준비 완료까지의 경과 시간. `value`에 캐시 최초 생성·재생성(`miss`), 재사용(`hit`), 비활성(`off`), 동시 실행(`busy`), 오버라이드(`overlay`), 캐시 사용 실패(`unavailable`), 개별 파일 배포(`loose`)가 표시된다. |
| `bundle` | 폴더별 아카이브 바이트, 에셋 수, 이번에 압축 해제한 바이트와 읽기 시간. 재사용 시 번들별 시간에는 아카이브 해시 검사만 들어간다. 캐시 파일 자체를 검사한 시간은 전체 콘텐츠 준비 시간에 포함된다. |
| `temp_mount_bytes`, `cache_mount_bytes`, `loose_bytes` | 준비 완료 시 해당 폴더에 있는 파일 길이의 합. 캐시 완료 표시·파일시스템 할당량·종료 후 잔여량은 별도로 측정한다. |
| `first_present`, `first_action` | 플레이어 진입 후 첫 SDL 화면을 표시한 시점과 매핑된 누름을 소비한 첫 시뮬레이션 틱이 끝난 시점. 액션 이름도 기록한다. |
| `frames`, `max_frame_ms` | SDL 루프 시작부터 첫 화면 표시까지와 이후 연속 화면 표시 사이의 간격 중 최댓값. 기본 관찰 구간은 첫 120프레임이다. |

Mac 측정기는 프로세스 시작 직전부터 약초 획득까지 별도로 시간을 재고 실제 저장 슬롯을 확인한다. 두 배포본의 빌드 명령과 실행별 CSV는 검증 기록에 있다. [InventoryLab 실행 기록](../../engine/tests/results/bundle-loading/README.md)

## 콘텐츠 업데이트

콘텐츠 버전 34부터 이미 배포한 게임의 번들에 포함된 `.data`와 `.table`을 한 파일로 업데이트할 수 있다. 제작자는 교체할 파일만 원래 상대 경로대로 준비한다. InventoryLab 수확제 샘플은 약초상의 인사·보상 대사와 약초 세 포기의 보상을 창고 열쇠 1개로 바꾼다. [수확제 교체 파일](../../samples/ContentUpdates/InventoryLab/Harvest/Assets)

**제작.** 저장소 루트에서 다음 명령을 실행한다. CLI는 기준 게임의 에셋 매니페스트를 읽고 번들을 만든 뒤 SHA-256을 출력한다. 번들과 이 해시를 이용자에게 전달한다. 같은 기준 매니페스트를 쓰는 Release와 Native AOT에는 같은 파일을 배포한다.

```sh
bin=game-builds/InventoryLab/InventoryLab.app/Contents/MacOS
bundle=game-builds/InventoryLab/Updates/Harvest.bundle
./monoon content-update pack "$bin/Game" samples/ContentUpdates/InventoryLab/Harvest "$bundle"
```

**적용.** 아래 해시는 이 Mac에서 검증한 수확제 번들의 값이다. 교체 파일이나 기준 매니페스트가 달라지면 제작자가 새로 발행한 값을 사용한다.

```sh
sha=8288D051CC082460F3D4F20B8A4B53224CE5F77E8DF00DB4346CD8059409C15D
"$bin/MoNoonPlayer" --content-update apply "$bundle" "$sha"
"$bin/MoNoonPlayer"
```

플레이어는 임시 파일에서 번들 전체의 SHA-256, 게임 ID, 기준 매니페스트, 각 문서의 GUID·경로·해시와 YAML 형식을 검사한다. 이어 임시 저장 프로필로 시작 씬을 열고 한 틱을 실행한다. 모두 통과하면 번들 하나를 게임별 사용자 폴더의 `ContentUpdates/Active.bundle`로 교체한다. 이미 실행 중인 게임은 종료할 때까지 그 실행에서 연 콘텐츠를 사용한다. 새 실행의 적용 순서는 기본 번들 → 콘텐츠 업데이트 → 활성 모드다. 샘플 대사를 보려면 달맞이 장터 모드를 꺼 둔다.

**후속 배포와 복구.** 각 업데이트에는 최초 배포본을 기준으로 계속 유지할 변경분을 모두 넣는다. 적용에 실패하면 명령이 종료 코드 1과 이유를 출력하며 마지막 정상 번들이 계속 쓰인다. 첫 업데이트가 실패하거나 다음 실행에서 활성 번들의 손상이 발견되면 출고 콘텐츠로 플레이할 수 있다. 같은 게임의 Release·Native AOT는 적용 상태를 공유한다. 원본으로 되돌릴 때는 아래 명령을 실행하고 게임을 다시 연다. 저장 슬롯과 이미 받은 보상은 보존된다.

```sh
"$bin/MoNoonPlayer" --content-update reset
```

문서당 최대 16 MiB, 교체 문서 합계 최대 64 MiB를 지원한다. 콘텐츠 버전 33 이하 플레이어에 이 기능을 처음 제공할 때는 버전 34로 한 번 내보낸다. 이후의 대사·보상 갱신은 번들 제작과 적용만으로 끝난다. [Release·Native AOT 실행 결과](../../engine/tests/results/content-updates/README.md)

## 데이터 모드

InventoryLab에는 약초상 대사와 의뢰 보상을 바꾸는 **달맞이 장터** 모드가 들어 있다. 처음에는 꺼져 있다. Mac에서 게임을 종료한 뒤 저장소 루트에서 다음 명령을 실행한다.

```sh
player=game-builds/InventoryLab/InventoryLab.app/Contents/MacOS/MoNoonPlayer
"$player" --mods list
"$player" --mods on moon-market
"$player"
```

**T**로 약초상에게 말을 걸어 달맞이 장터 인사를 확인한다. 약초 세 포기의 보상은 수정 두 개다. 게임을 종료하고 `"$player" --mods off moon-market`을 실행한 뒤 다시 열면 인사와 새 의뢰의 보상이 기본값인 수정 한 개로 돌아온다. 기존 슬롯에 지급된 보상과 완료 기록은 유지된다. 같은 게임의 Release·Native AOT는 모드 선택을 공유한다.

**추가 모드 설치.** `--mods list`가 표시하는 사용자 Mods 폴더에 `.mod` 파일을 넣고, 파일 이름에서 확장자를 뺀 ID로 활성화한다. 모드 ID는 영문 소문자, 숫자, 하이픈, 밑줄을 사용한다. 제작자가 게임과 함께 배포할 모드는 소스 프로젝트의 `Mods/`에 넣으면 빌더가 검증 후 포함한다.

**모드 작성.** `.mod`는 VYaml 문서다. `version: 1`, 게임의 `gameId`, 표시 이름 `name`과 교체 목록 `replacements`를 지정한다. 각 항목의 `asset`은 배포에 포함된 기존 에셋의 GUID다. `.data`는 `data`, `.table`은 `table` 아래에 해당 에셋의 전체 YAML 내용을 넣는다. 번들에서 읽은 원본의 무결성을 확인한 후 이번 실행용 사본에 교체 내용을 적용한다. 같은 에셋을 교체하는 두 모드를 함께 켜면 충돌한 경로를 표시하고 기존 선택을 유지한다. [달맞이 장터 모드 원본](../../samples/InventoryLab/Mods/moon-market.mod)

## 로컬 회귀 검사

엔진을 수정한 뒤 저장소 루트에서 `./monoon test`를 실행한다. 새 체크아웃에서 세 플랫폼의 Release 내보내기 템플릿을 준비하거나 플레이어 소스 변경을 반영하려면 먼저 `./engine/scripts/build-players.sh`를 실행한다.

플랫폼별 검사는 Apple Silicon Mac과 기존 Windows x64·Linux x64 VM에서 실행한다. 각 검사에 새 결과 폴더를 지정하면 해당 OS의 저장 파일, 측정값 또는 캡처를 로컬에서 확인할 수 있다. [VM 실행 방법](../troubleshooting/README.md)

| 대상 | 검사 방법 |
| --- | --- |
| InventoryLab 배포 게임 | Release와 Native AOT에서 약초 의뢰 입력을 재생하고 저장 바이트를 Mac 기준과 대조한다. v31 프로젝트를 v35로 변환하고 기존 v2 슬롯에서 의뢰를 마치는 경우도 재생할 수 있다. [실행 명령](../../engine/tests/InventoryLab.Replay/README.md) |
| KeyRoom 배포 에디터 | Inspector에서 Player X를 바꾸고 씬을 저장한다. 편집 직후와 다시 연 뒤의 Play에서 F5 저장을 기준 파일과 비교한다. [OS별 조작 회귀 명령](../../engine/tests/KeyRoom.Desktop/README.md#배포-에디터-조작-회귀) |
| KeyRoom 에디터 빌드 | **Build Game…**의 컨트롤로 설정을 저장하고 Release(패키지 포함)와 Native AOT를 빌드한 뒤, 같은 설정으로 `./monoon build`한 결과와 파일별 SHA-256을 비교한다. [실행 명령](../../engine/tests/KeyRoom.EditorBuild/README.md) |
| KeyRoom 배포 게임 | Release와 Native AOT에서 코인을 생성하고 저장을 복원한 뒤 획득 상태를 대조한다. [월드 체크포인트 실행 명령](../../engine/tests/KeyRoom.WorldCheckpoints/README.md) |
| AudioLab 배포 게임 | Release와 Native AOT에서 Ogg 루프를 재생하고 F 키로 정지해 SDL 출력과 페이드 길이를 대조한다. [실행 명령](../../engine/tests/AudioLab.Vorbis/README.md) |

## 에디터와 템플릿 관리

- 에디터는 [editor](../../editor/)에, 게임 빌드 템플릿은 [player-templates](../../player-templates/)에 있다. 에디터를 옮긴 경우 필요한 템플릿을 담은 `player-templates/`를 앱 옆에 두거나 환경 변수 `MONOON_PLAYERS_DIR`로 상위 폴더를 지정한다.
- 엔진 소스를 수정했다면 저장소 루트에서 `./engine/scripts/build-editor-mac.sh`로 Mac 에디터를, `./engine/scripts/build-players.sh`로 세 Release 템플릿과 AOT 키트를 갱신한다. 특정 플랫폼만 만들려면 스크립트 뒤에 `osx-arm64` 같은 플랫폼 이름을 준다. AOT 키트만 갱신하는 명령은 `./engine/scripts/build-native-aot-kit.sh`다.
- 새 격자를 담은 씬은 스키마 9로 저장한다. 이전 직교 타일맵에서 Layout을 생략한 경우 **Orthogonal**로 읽는다. 현재 빌더는 콘텐츠 버전 **35** 템플릿·키트를 확인한다. 구형 템플릿 오류가 나오면 템플릿을 갱신하고 게임도 다시 내보낸다.
- 이전 JSON 또는 `.clip.yaml` 같은 이중 확장자 프로젝트는 복사본을 **File → Open Project…**로 열어 변환한 뒤 씬 참조와 저장·재열기를 확인한다. 터미널에서 같은 변환은 `editor/MoNoon.app/Contents/MacOS/MoNoon --migrate-project <프로젝트 폴더>` 또는 에디터 없이 `./monoon migrate <프로젝트 폴더>`다(CI와 Linux에서 사용). 같은 새 경로에 다른 내용이 이미 있으면 양쪽 파일을 확인하고 이름 충돌을 해결한다.

## 개발 도구와 문제 해결

| 상황 | 확인할 곳 |
| --- | --- |
| 컴포넌트 스크립트가 컴파일되지 않음 | **Project → Console…**에서 첫 오류와 파일명을 확인한다. [에디터 사용법](editor.md) |
| 씬은 열리지만 Play나 빌드에 실패 | 시작 카메라, 시작·포함 씬, 오류에 표시된 PNG·타일 ID·스크립트 참조를 확인한다. [공통 진단](../troubleshooting/README.md) |
| 특정 OS에서 실행·입력·렌더링 실패 | 에디터의 Scene, Play, 독립 게임 중 어디에서 실패했는지 기록하고 플랫폼별 실행 절차를 따른다. [macOS](../troubleshooting/macos.md) · [Windows](../troubleshooting/windows.md) · [Linux](../troubleshooting/linux.md) |
| 엔진 수정 후 회귀 확인 | 저장소 루트에서 `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD"`를 실행한다. 씬·참조·UI·애니메이션·타일·게임 내보내기를 검사한다. [검사 코드](../../engine/tests/Engine.Checks/) |
| Mac SDL 텍스처와 타일 픽셀 비교 | `dotnet run --project engine/benchmarks/Sprites2D/Brutal -c Release -- metal --verify-batching`을 실행한다. 소프트웨어 렌더러는 `metal` 자리에 `software`를 지정한다. [스프라이트 벤치마크와 기존 측정 결과](../../engine/benchmarks/Sprites2D/) |
