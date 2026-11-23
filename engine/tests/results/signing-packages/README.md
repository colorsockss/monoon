# 배포본 서명과 패키지 검증

2026-11-23에 macOS 27.0.1 (26A434), Apple Silicon arm64, .NET SDK 10.0.101에서 실행했다. KeyRoom 복사본의 `GameSettings`에 아이콘(`Assets/Key.png`)을 지정하고 `./monoon build <프로젝트> <형식> --package on`으로 네 가지 배포본을 만들었다. 모두 배포용 프로필이다. 실제 인증서는 없으므로 macOS는 애드혹으로 서명했다. 프로젝트 밖 사용자 설정 파일에 PFX 경로와 비밀번호 파일을 지정한 상태로 빌드해 비밀번호가 기록되지 않는지 확인했다. Windows·Linux VM은 사용하지 않았다.

| 대상 | 빌드 | 검사 | 결과 |
| --- | --- | --- | --- |
| osx-arm64 | Release | `codesign --verify --deep --strict` 앱, ZIP(`ditto -x -k`), DMG 안의 앱 | 통과. 애드혹, 강화된 런타임. 권한 `allow-jit`, `disable-library-validation` |
| osx-arm64 | Native AOT | 같은 세 검사 | 통과. 애드혹, 강화된 런타임. 권한 `disable-library-validation` |
| osx-arm64 | Release, Native AOT | ZIP에서 푼 앱의 `--smoke-test`, 창 실행 5초 후 프로세스와 SDL3 로드 확인 | 통과. [Release 창](release-window.png) · [Native AOT 창](native-aot-window.png) |
| osx-arm64 | Release, Native AOT | 서명한 InventoryLab의 `./monoon test inventorylab --profile distribution`, `./monoon test replay` | 43개 항목 통과. 헤드리스 리플레이 저장이 원본 기록과 같음 |
| osx-arm64 | 둘 다 | 공증 | 건너뜀. 애드혹 서명은 공증할 수 없다는 메시지 출력 |
| win-x64 | Release | ZIP 구성, 서명 단계 | `KeyRoom-win-x64/MoNoonPlayer.exe`, `SDL3.dll`, `Game/` 포함 268개 파일. `signtool`은 Windows 호스트에서만 실행한다는 메시지와 함께 건너뜀 |
| linux-x64 | Release | tar 권한, 데스크톱 항목, 아이콘 | `MoNoonPlayer` `rwxr-xr-x`, `install-desktop-entry.sh` `rwxr-xr-x`, `createdump`는 템플릿 권한 그대로 `rwxr--r--`, 데이터 `rw-r--r--`. `KeyRoom.desktop`과 `KeyRoom.png` 포함 |
| 전체 | 네 빌드 | 출력 폴더, ZIP 3개, tar, DMG 2개의 금지 파일 검사 | 2580개 항목에서 SkiaSharp, Avalonia, HarfBuzz, Engine.Editor, Engine.Authoring, Roslyn 0개 |
| 전체 | 네 빌드 | 설정한 PFX 비밀번호 검색 | 빌드 로그, 표준 출력, 출력 폴더에서 0건 |
| 엔진 | — | `./monoon test` (패키지 단위 검사 포함) | 680개 PASS, `All checks passed.` |

[Mac 서명·ZIP·DMG 기록](mac-verify.txt) · [x64 패키지 구성과 금지 파일 검사](x64-layout.txt) · [창 실행 기록](windowed.txt) · [서명한 InventoryLab 검사](inventorylab-signed.txt)

압축을 푼 Linux 폴더에서 `install-desktop-entry.sh`를 `XDG_DATA_HOME`을 임시 폴더로 지정해 실행했다. 생성된 항목의 `Exec`와 `Icon`이 압축을 푼 폴더의 `MoNoonPlayer`와 `KeyRoom.png` 절대 경로를 가리켰다. 개발용 프로필에 `--package on`을 붙이면 종료 코드 2로 거부했다. 서명 설정 파일을 프로젝트 안에 두면 패키지 단계가 실패했다.

## 발견하고 고친 문제

처음 구현에서 Release 앱 묶음 서명이 `bundle format unrecognized ... Contents/MacOS/Game/.monoon-compiled`로 실패했다. `Contents/MacOS` 아래의 확장자가 있는 폴더를 codesign이 중첩 번들로 읽기 때문이다. 서명 단계에서 `Contents/MacOS`의 폴더를 `Contents/Resources`로 옮기고 상대 링크를 남기도록 바꿨다. 그 뒤 서명한 InventoryLab Release의 스모크 실행이 `Unexpected directory link in game content: .../Contents/MacOS/Game`으로 종료 코드 1을 반환했다. 콘텐츠 검사가 게임 폴더 자체의 링크도 거부했기 때문이다. 플레이어가 시작할 때 `Game` 링크를 실제 경로로 바꾸도록 수정했다. 게임 폴더 안의 링크는 여전히 거부한다. 수정 후 템플릿과 AOT 키트를 다시 빌드했고 위 검사를 모두 다시 실행했다.

## 구현하지 않은 것과 확인하지 않은 것

- 실제 Developer ID 서명, `notarytool` 제출, `stapler`, Windows `signtool` 실행은 인증서가 없어 실행하지 않았다. 해당 명령 경로는 코드에만 있다. `signtool` 인수 구성과 출력 가림은 인수를 그대로 출력하는 대체 도구(`/bin/echo`, `/bin/ls`)로 `./monoon test`에서 검사한다.
- Windows 설치 프로그램은 만들지 않았다. 도구를 내려받아야 하기 때문이다. 설치 단계를 추가하려면 WiX Toolset(MSI) 또는 Inno Setup이 빌드 컴퓨터에 있어야 한다. 설치 경로, 시작 메뉴 바로 가기, 제거 항목과 업그레이드 코드를 정의하고, 생성한 설치 파일을 같은 인증서로 `signtool` 서명한 뒤 타임스탬프를 붙여야 한다.
- 앱 아이콘(macOS `.icns`, Windows 실행 파일 리소스)은 넣지 않았다. 아이콘은 Linux 데스크톱 항목에만 쓴다.
- 앱 안의 DLL 서명은 확장 속성에 저장된다. ZIP을 `unzip`으로 풀면 `codesign --verify`가 실패한다. Finder나 `ditto -x -k`로 풀어야 한다. DMG에서는 문제가 없다.
- 서명한 앱의 `MoNoonBuild`에 있는 `playerSha256`은 서명 전 값이다. 서명 종류는 `codeSignature`에 기록하고, InventoryLab 검사는 이 값이 있으면 해시 비교를 건너뛴다.
- 에디터 **Build Game…** 대화 상자에 추가한 Profile, Package, Icon 행은 빌드만 확인했다. 화면 조작 권한을 받지 못해 1280/1600/1920px 정렬과 긴 이름 표시는 확인하지 않았다. 기존 행과 같은 `78,*` 열과 같은 Row 함수를 사용한다.
- Windows·Linux에서 패키지를 풀어 실행하는 검사는 하지 않았다. 이 Mac에서 만든 압축 구성과 권한만 확인했다.

Linux x64 게스트(2026-11-23): 이 Mac에서 만든 tar.gz가 권한을 유지해 풀렸고 `install-desktop-entry.sh`가 올바른 항목을 썼으며 풀린 플레이어가 스모크 실행과 헤드리스 재생을 통과했다. [Linux 결과](../chapter11-linux-x64/README.md)
