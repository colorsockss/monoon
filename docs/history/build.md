# 빌드·배포·검증 히스토리

빌드 CLI와 배포 패키지, 배포 용량·시작 시간, 회귀·플랫폼 검증, BRUTAL 사용, 에이전트 개발 도구가 지금 어떻게 구현되어 있고 왜 그렇게 정했는지 모은다. 사용법은 [빌드와 배포](../manual/build.md)와 [에이전트 작업 흐름](../manual/agent-workflow.md)에 있고, 수치와 원본 로그는 링크한 결과 README에 있다.
대괄호는 2026-11-25에 로드맵에서 뺀 예전 [로드맵](../roadmap.md) 행 번호다. 이유는 결과 README, 계획 문서, 커밋에 적힌 것만 옮겼다.

- [빌드 CLI와 로컬 회귀](#빌드-cli와-로컬-회귀)
- [프로필, 심볼, 크래시 진단](#프로필-심볼-크래시-진단)
- [헤드리스 입력 리플레이와 x64 게스트](#헤드리스-입력-리플레이와-x64-게스트)
- [콘텐츠 번들, 모드, 콘텐츠 업데이트, 업그레이드](#콘텐츠-번들-모드-콘텐츠-업데이트-업그레이드)
- [모바일·웹 타깃](#모바일웹-타깃)
- [배포 에디터 패키징과 3개 OS 에디터 회귀](#배포-에디터-패키징과-3개-os-에디터-회귀)
- [에디터와 CLI가 같은 빌드 설정을 쓴다](#에디터와-cli가-같은-빌드-설정을-쓴다)
- [콘텐츠 증분 빌드](#콘텐츠-증분-빌드)
- [게임 스크립트 테스트](#게임-스크립트-테스트)
- [서명과 배포 패키지](#서명과-배포-패키지)
- [배포 용량과 시작 시간](#배포-용량과-시작-시간)
- [Linux x64 회귀와 Windows 보류](#linux-x64-회귀와-windows-보류)
- [BRUTAL 사용 결정](#brutal-사용-결정)
- [에이전트 개발 도구](#에이전트-개발-도구)

## 빌드 CLI와 로컬 회귀

[5.1] 1차. 저장소 루트의 `monoon` 셸 스크립트가 `build`·`content-update`·`crash`·`migrate`·`profile`은 `Engine.Cli`로, `test`는 `Engine.Checks`로 넘긴다. `Engine.Cli`는 `Engine.Authoring`의 `GameBuild`를 불러 에디터의 **Build Game…**과 같은 코드로 내보낸다. 종료 코드는 성공 0, 빌드·검사 실패 1, 사용법 오류 2다. `./monoon ci`는 호스트(macOS arm64 또는 Linux x64)용 템플릿·AOT 키트를 만들고 InventoryLab을 두 형식으로 내보낸 뒤 `test inventorylab`·`test replay`를 돌린다. macOS에서는 마지막에 크기 기준 검사도 한다. [사용법](../manual/build.md#터미널에서-빌드와-회귀-검사) · [검증](../../engine/tests/results/build-cli/README.md)

- `test inventorylab`은 플레이어의 `--smoke-test`로 창 없이 실제 실행 파일을 시작해, 저장 슬롯을 예상 YAML과 대조하고 두 빌드의 저장 바이트를 비교한다.
- 처음에는 GitHub Actions로 세 OS 회귀를 돌렸다. 2026-11-17에 개발 중 회귀를 이 Mac과 기존 VM에서 로컬로 돌리고 Actions 워크플로를 지웠다(`4513142`, 사용자 선택). 그래서 [AGENTS.md](../../AGENTS.md)의 로컬 테스트 규칙과 [로컬 회귀 검사](../manual/build.md#로컬-회귀-검사)가 기준이다. 야간 전체 회귀도 보류 중이다(next-work 결정 16).

## 프로필, 심볼, 크래시 진단

[5.1] 2차, [5.2] 크래시 진단. 두 프로필 모두 최적화한 Release 구성이고 심볼을 두는 곳만 다르다. 개발용은 앱 안에, 배포용은 출력 폴더의 `Symbols/<게임>/<RID>/<형식>/<빌드 ID>/`에 둔다. 빌드마다 새 GUID `buildId`를 만들고 실행 파일 옆 `MoNoonBuild` 영수증에 대상·형식·실행 파일 SHA-256·PDB ID를 적는다. 이전 배포본의 오류도 그 빌드의 심볼로 해석하기 위해서다. [사용법](../manual/build.md#개발용배포용-프로필과-디버그-심볼) · [검증](../../engine/tests/results/build-profiles/README.md)

- 크래시 보고서는 플레이어의 관리 코드([CrashDiagnostics.cs](../../engine/src/Engine.Player/CrashDiagnostics.cs))가 첫 예외부터 바깥 예외까지 YAML로 쓴다(콘텐츠 버전 35). `monoon crash explain`은 Release에서는 portable PDB로 메서드 토큰·IL 오프셋을, macOS Native AOT에서는 dSYM UUID 대조 뒤 `atos`로 주소를 소스 줄로 바꾼다. [검증](../../engine/tests/results/crash-diagnostics/README.md)
- BRUTAL의 `Brutal.Monitor`(감시 프로세스, 덤프) 대신 이 방식을 유지한다. Monitor는 플랫폼별 감시 실행 파일(약 2.5 MiB)을 두 번째 프로세스로 띄운다([유지 결정](../brutal.md#유지-결정)).
- 덤프를 쓰지 않으므로 배포용 Release에서 디버거 런타임 파일을 뺄 수 있었다(15.1, 아래).

## 헤드리스 입력 리플레이와 x64 게스트

[5.1] 3·4차. 플레이어의 `--record-input`이 실제 창의 키·포인터·게임패드·텍스트 입력과 프레임 간격을 `.inputreplay` YAML로 남기고, `--replay-input`이 창과 오디오 장치 없이 같은 입력 라우터로 재생한다. 입력이 없는 프레임도 보관해 원래 간격으로 UI와 60 Hz 고정 업데이트를 진행한다. 기준은 실제 플레이의 InventoryLab 약초 의뢰(475프레임, 31이벤트)와 그때의 저장 파일(171바이트, `eed41def…`)이다. [사용법](../manual/build.md#헤드리스-입력-리플레이) · [Mac 검증](../../engine/tests/results/input-replay/README.md)

- 2026-11-14에 기존 QEMU Windows 11 x64와 Debian 12 amd64 게스트 안에서 Release·Native AOT를 직접 빌드해 같은 입력을 재생했고, 네 배포본의 저장이 Mac 기준과 바이트까지 같았다. [x64 검증](../../engine/tests/results/input-replay-x64/README.md) · [게스트 실행기](../../engine/tests/InventoryLab.Replay/README.md)
- Linux에서 셰이더 시트를 구울 때 Mesa(`libOSMesa.so.8`)를 쓴다. SkiaSharp 2.88.9의 CPU 표면이 런타임 셰이더에서 `SIGILL`로 끝났기 때문이다(`f436488`). Windows 빌드 도구는 같은 이유(`0xC000001D`)로 ANGLE·D3D11 WARP를 쓴다. [Linux](../troubleshooting/linux.md) · [Windows](../troubleshooting/windows.md)
- x64에서는 Release(JIT)와 Native AOT가 다른 비트를 낼 수 있다. 게스트 CPU의 AVX2를 JIT만 쓰기 때문이다(PlatformerLab Joints, [11장 Linux 기록](../../engine/tests/results/chapter11-linux-x64/README.md)). 시뮬레이션 코드는 FMA·추정 경로 대신 곱셈과 덧셈을 따로 하는 `Affine2D`를 쓴다(next-work 결정, [x64 결정론](../../engine/tests/results/x64-determinism/README.md)).

## 콘텐츠 번들, 모드, 콘텐츠 업데이트, 업그레이드

[5.2] 배포·유지보수. 모두 이미 배포한 게임을 다시 빌드하지 않고 데이터만 바꾸는 경로다. [사용법](../manual/build.md#콘텐츠-번들)

| 기능 | 구현 | 기록 |
| --- | --- | --- |
| 콘텐츠 번들 | `GameSettings.contentBundles`의 폴더를 `ContentBundles/` 아카이브로 묶는다. 소속은 내보내는 경로로 정하고 번들 사이 참조는 GUID 그대로다. 플레이어는 첫 실행에 게임 프로필의 `BundleCache/v1/Game`에 풀고 SHA-256을 확인한 뒤 완료 표시를 남긴다. 다음 실행은 해시만 확인하고 압축 해제를 건너뛴다 | [번들](../../engine/tests/results/content-bundles/README.md) · [대형 번들 로딩](../../engine/tests/results/bundle-loading/README.md) |
| 데이터 모드 | `.mod`는 VYaml 문서(`version: 1`, `gameId`, `replacements`)이고 배포한 `.data`·`.table` 에셋을 GUID로 교체한다. 번들 원본의 무결성을 확인한 뒤 이번 실행용 사본에 적용한다. 같은 에셋을 바꾸는 두 모드는 함께 켜지 않는다 | [모드](../../engine/tests/results/data-mods/README.md) |
| 콘텐츠 업데이트 | 콘텐츠 버전 34부터. `monoon content-update pack`이 기준 매니페스트로 번들을 만들고 SHA-256을 낸다. 플레이어는 임시 파일에서 해시·게임 ID·매니페스트·YAML 형식을 검사하고 임시 프로필로 시작 씬 한 틱을 돌린 뒤에만 `ContentUpdates/Active.bundle`을 교체한다. 실패하면 마지막 정상 번들, 없으면 출고 콘텐츠로 돌아간다 | [업데이트](../../engine/tests/results/content-updates/README.md) |
| 업그레이드 가이드 | 콘텐츠 버전 31 InventoryLab을 35 에디터로 변환·빌드하고 옛 v1·v2 슬롯을 읽어 v3로 저장하는 절차. macOS와 두 x64 게스트에서 같은 저장 바이트를 냈다 | [가이드](../manual/upgrade.md) · [검증](../verification/inventorylab-upgrade.md) |

- 프로젝트 변환은 에디터의 **Open Project…**·`--migrate-project`와 에디터 없는 `monoon migrate`가 같은 `LegacyProject.Upgrade`를 쓴다. Linux에는 에디터가 없으므로(아래) Linux와 CI에서는 `monoon migrate`를 쓴다. 이전 JSON 메타데이터 읽기는 `Engine.Authoring/LegacyJson.cs`에만 있어 플레이어에 들어가지 않는다(15.3).

## 모바일·웹 타깃

[5.2] **보류.** BRUTAL이 Android, iOS, 웹을 각각 공식 지원할 때 재개한다. 코드의 빌드 대상은 `osx-arm64`, `win-x64`, `linux-x64` 셋뿐이다([BuildSettings.cs](../../engine/src/Engine.Authoring/BuildSettings.cs)의 `Targets`). 배포 렌더링을 BRUTAL SDL3로 한다는 규칙([AGENTS.md](../../AGENTS.md#배포-게임-렌더링))이 이 보류의 근거다.

## 배포 에디터 패키징과 3개 OS 에디터 회귀

[6.8] 2026-11-16에 Windows·Linux x64 배포 에디터를 패키징해 KeyRoom 열기, Inspector의 Player X 편집, Play, 저장·재열기의 F5 저장이 Mac Scene Play·Release·Native AOT와 같은지 확인했다. [기록](../../engine/tests/results/editor-packaging/README.md) · [검사기](../../engine/tests/KeyRoom.Desktop/README.md#배포-에디터-조작-회귀)

[12.8] 2026-11-23에 새 도킹 에디터로 세 OS 패키지 회귀(도킹 다섯 단계, 테마, 패널, 프리팹 편집, 6.8 조작)와 1280·1600·1920px 캡처를 남겼다. 100% 배율에서만 보이는 1px 정렬 오류 세 건을 고쳤다. 게스트 TCG 지연 때문에 검사기 대기 시간을 늘렸고 합격 기준은 그대로 두었다. [기록](../../engine/tests/results/editor-ui-x64/README.md)

- **Linux 에디터 중단(사용자 결정, 2026-11-23).** 그 뒤 [package-editor.py](../../engine/scripts/package-editor.py)는 `win-x64`와 KeyRoom 샘플 ZIP만 만든다. Linux는 게임 내보내기 대상(Release·Native AOT, `tar.gz`)과 CI의 헤드리스 빌드·리플레이로만 쓴다. 6.8·12.8의 Linux 에디터 기록은 중단 전 기록으로 남아 있다.
- 6.8·12.8의 Windows 결과는 그때 게스트 기록이다. 그 뒤 Windows 검증은 사용자 장비 준비까지 보류 중이다(아래).

## 에디터와 CLI가 같은 빌드 설정을 쓴다

로드맵 행 없음(11장 작업과 함께 들어감). **Build Game…**의 Platform·Build type·Profile·Package·Content를 프로젝트 루트의 확장자 없는 `BuildSettings`(VYaml, `Engine.Authoring`의 레코드)에 저장하고 `monoon build <프로젝트>`가 같은 파일을 읽는다. CLI 인자는 그 실행만 덮어쓴다. 확장자가 없어 에셋 스캐너가 무시하므로 게임 데이터·패키지에 들어가지 않고 플레이어도 읽지 않는다. [사용법](../manual/build.md#프로젝트-빌드-설정) · [기록](../../engine/tests/results/editor-build/README.md)

- 에디터와 CLI의 결과를 비교하다 재현성 문제 두 개를 고쳤다. 스크립트 컴파일 참조를 파일 이름순으로 정렬했고(자체 포함 에디터와 공유 런타임 CLI가 `System.*`을 다른 순서로 넘겨 MVID가 달랐다), Native AOT 작업 폴더 전체에 `PathMap`을 걸어 실행 파일에 임시 경로가 남지 않게 했다. 이제 빌드 ID, 서명, AOT 링커 UUID만 다르다.

## 콘텐츠 증분 빌드

[9.4] [ContentBuildCache.cs](../../engine/src/Engine.Authoring/ContentBuildCache.cs)가 생성물마다 입력(셰이더 소스·프레임 설정, 타일 청크의 셀과 한 셀 경계·타일셋·레이아웃, 번들에 넣은 경로와 배포 바이트 해시, 생성기 버전)을 기록하고, 같으면 출력 폴더의 `.monoon-build-cache`에서 재사용한다. 재사용할 파일의 SHA-256이 다르거나 없으면 다시 만든다. 캐시는 프로젝트 ID·호스트·대상·형식·프로필별로 나눈다. CLI·Console에 `build`/`reuse`와 이유를 쓴다. [사용법](../manual/build.md#콘텐츠-증분-빌드와-재빌드-사유) · [기록](../../engine/tests/results/incremental-content/README.md)

- 판정은 InventoryLab 대사 한 줄을 바꾼 뒤 전체 빌드와 콘텐츠 해시·약초 의뢰 리플레이를 대조하는 것이다. 11.13에서 글리프 시트 굽기를 없애 생성물이 8개에서 7개가 됐다(게임 텍스트를 SDL_ttf로 실행 중에 그리게 됨).
- 스크립트 컴파일, 플레이어 스테이징, 참조 검사는 매번 실행한다. 재사용 대상은 생성 비용이 큰 콘텐츠뿐이다.

## 게임 스크립트 테스트

[11.9] [ProjectTests.cs](../../engine/src/Engine.Authoring/ProjectTests.cs)가 `Tests/`의 C# 파일을 `Scripts/`와 함께 Roslyn으로 컴파일하고, `[GameTest(scene)]` 메서드마다 빈 임시 사용자 저장 폴더에서 씬을 창 없이 연다. 입력은 `.inputreplay` 재생과 같은 입력 라우터로 넣고 프레임마다 UI 1/60초와 게임 틱 하나를 진행한다. 기본 제한은 3600프레임(`MaxFrames`)이다. 빌더는 `Tests/`를 게임 스크립트로 읽지 않으므로 Release·Native AOT에 들어가지 않는다. 테스트 형식은 `Engine.Authoring`에 있어 런타임이 빌드 도구를 참조하지 않는다는 경계도 지킨다. [사용법](../manual/scripting.md#게임-스크립트-테스트) · [기록](../../engine/tests/results/project-tests/README.md)

- KeyRoom 상자·문 테스트 9개가 macOS arm64와 Linux x64 게스트의 패키지 CLI에서 통과했다([11장 Linux](../../engine/tests/results/chapter11-linux-x64/README.md)). Windows는 보류.

## 서명과 배포 패키지

[11.12] [DistributionPackage.cs](../../engine/src/Engine.Authoring/DistributionPackage.cs)가 배포용 빌드 뒤에 대상별 단계를 실행한다(`--package on` 또는 **Package → Sign and package**). [사용법](../manual/build.md#서명과-배포-패키지) · [기록](../../engine/tests/results/signing-packages/README.md)

| 대상 | 구현된 단계 | 확인한 것 |
| --- | --- | --- |
| macOS | `codesign`(강화된 런타임, Release만 `allow-jit`), 설정 시 `notarytool`·`stapler`, `ditto` ZIP, `hdiutil` DMG | 애드혹 서명 앱·ZIP·DMG의 `codesign --verify --deep --strict`, 서명한 InventoryLab 리플레이 |
| Windows | `signtool`(Windows 호스트에서 인증서를 설정했을 때만), ZIP | 인수 구성과 비밀번호 가림을 대체 도구로 검사 |
| Linux | 실행 권한을 보존한 `tar.gz`(Pax), `.desktop`, 아이콘, `install-desktop-entry.sh` | Mac에서 만든 `tar.gz` 4개를 Linux 게스트에서 풀어 권한·링크·스모크·리플레이 통과([next1-x64](../../engine/tests/results/next1-x64/README.md#빌드-출력-검사)) |

- 실제 인증서가 없어 Developer ID 서명·공증·`signtool` 실행은 확인하지 않았다(사용자 결정으로 단계만 구현).
- **Windows 설치 프로그램은 만들지 않았다.** WiX·Inno Setup 같은 도구를 내려받아야 하기 때문이다. 앱 아이콘(`.icns`, exe 리소스)도 넣지 않는다.
- 서명 설정은 사용자 폴더의 `signing.yaml`, 비밀번호는 환경 변수나 파일에서 읽는다. 설정·PFX·비밀번호 파일이 프로젝트, 그 Git 저장소, 출력 폴더 안에 있으면 실패한다. 비밀이 저장소나 배포본에 들어가지 않게 하려는 규칙이다.
- 처음 구현에서 codesign이 `Contents/MacOS/Game/.monoon-compiled`를 중첩 번들로 읽어 실패했다. 그래서 서명 전에 데이터 폴더를 `Contents/Resources`로 옮기고 상대 링크를 남긴다.

## 배포 용량과 시작 시간

[15.1–15.13] [AGENTS.md](../../AGENTS.md)의 목표(배포 용량·시작 시간·프레임 비용 우선)에 따라 2026-11-24에 조사([빌드 최적화](../plans/next-work/2-build-optimisation.md), [trim](../plans/next-work/3-trim.md))를 하고 세 묶음과 AOT 후속 작업으로 적용했다. 모든 단계에서 결정성 게이트(KeyRoom 월드 체크포인트, InventoryLab 리플레이, PlatformerLab Joints 등)가 기준값과 같았다. 수치는 osx-arm64 배포용 기준이다.

| 행 | 구현 | 효과 | 이유·기록 |
| --- | --- | --- | --- |
| 15.1 | [Directory.Build.targets](../../engine/Directory.Build.targets)가 Box2D 버전 사본과 Linux `libSDL3.so.0.4.12`를 빼고, 배포용 Release는 DAC·DBI·`createdump`·대체 GC·LTTng 공급자를 복사하지 않는다(`GameBuild`). 개발용은 유지 | KeyRoom Release −6.8 MB, AOT −0.54 MB | 런타임이 버전 없는 이름만 읽는다(`objdump -p`로 확인). 크래시 보고서가 덤프를 쓰지 않는다. [size-wave1](../../engine/tests/results/size-wave1/README.md) |
| 15.2 | 에셋·스크립트 이름의 Windows 예약어 검사를 `ProjectAssets.WindowsDeviceName`으로 | AOT −0.73 MB, Regex 기호 5,748 → 0 | 이름 270개에서 이전 정규식과 판정이 같다. [size-wave1](../../engine/tests/results/size-wave1/README.md) |
| 15.3 | 이전 JSON 형식 읽기를 `Engine.Authoring/LegacyJson.cs`로 | AOT −1.1~1.2 MB | 다형 컴포넌트 메타데이터가 JSON 변환기 코드를 붙잡고 있었다. 변환은 에디터·CLI만 한다 |
| 15.4 | macOS 배포용에서 dylib에 `strip -x`, 다시 서명 | Release −3.1 MB, AOT −0.65 MB | 실행 파일과 dSYM은 그대로. 도구가 없으면 건너뛴다 |
| 15.5 | 기존 의존성 탐색이 처음 찾은 참조 경로를 기록해 빌드 캐시에 `assets.yaml`을 쓴다 | 배포본 0 B | 별도 탐색을 두지 않았다. `Assets/` 밖은 경고하지 않는다(trim 결정 4). [build-report](../../engine/tests/results/build-report/README.md) |
| 15.6 | `BuildSizeChecks`가 KeyRoom·PlatformerLab·InventoryLab Release·AOT를 [baseline.tsv](../../engine/tests/results/build-size/baseline.tsv)와 비교(1% 초과 실패), AOT 키트 map의 VYaml `Serialize`·`JsonSerializer` 노드 검사 | 회귀 감지 | 참조형 `Encode<T>` 호출 하나로 2.3 MB가 되돌아오므로 원인을 바로 알리려고 map 검사를 둔다. 시작 시간은 부하에 따라 수십 ms 흔들려 기록만 한다. [build-size](../../engine/tests/results/build-size/README.md) |
| 15.7 | Release도 게임마다 AOT 키트를 Trim + ReadyToRun으로 publish(crossgen2 `osx-arm64` 하나로 세 RID) | KeyRoom 89.5 → 33.2 MB, 첫 프레임 244 → 115 ms | crossgen2 내려받기는 사용자 승인(결정 2). composite R2R는 첫 프레임 78 ms로 더 빠르지만 앱 +8.3 MB, 메모리 +4.8 MiB라 쓰지 않았다. 빠른 시작에서 드러난 로그 초기화 경합을 고쳤다. [size-wave2](../../engine/tests/results/size-wave2/README.md) |
| 15.8 | 플레이어가 쓰는 형식 7개에 같은 바이트를 내는 비제네릭 기록기(`ProjectYamlWriters`), 프로젝트 폴더 실행 전용 코드는 `MoNoon.ExportedGameOnly` 스위치(AOT만) | AOT 실행 파일 약 −2.3 MB | ILC가 참조형 `Serialize` 호출 하나를 `__Canon` 사용으로 기록해 생성 포매터 102개의 쓰기 코드가 모두 남았다. 스위치를 `||`로 묶으면 컴파일러가 비단락 `or`로 바꿔 ILC가 접지 못했다. System.Text.Json 전체 제거는 공개 API 변경이 커서 하지 않았다. [aot-serializer](../../engine/tests/results/aot-serializer/README.md) · [aot-size-2](../../engine/tests/results/aot-size-2/README.md) |
| 15.9 | `NativeAotBuild.PrefersSize`: 배포용 osx-arm64·linux-x64 AOT에 `OptimizationPreference=Size` | 실행 파일 −2.26%(Mac), −2.43%(Linux 게스트) | win-x64는 Windows 검증 재개 때 정한다. [size-wave3](../../engine/tests/results/size-wave3/README.md) |
| 15.10 | 1 MiB 넘는 WAV에 `AssetBuildReport.WavWarning` 한 문구로 보고서·빌드 로그·**Import Assets…** 경고 | 경고만 | 인코더가 없어 자동 변환하지 않는다(결정 6) |
| 15.11 | [BuildFeatures.cs](../../engine/src/Engine.Authoring/BuildFeatures.cs)가 포함 씬·프리팹의 `[EngineFeature]` 컴포넌트, 충돌 타일셋, 배포 오디오·글꼴, localization, 스크립트 이름(Roslyn 의미 모델)으로 text·audio·physics 사용을 판정해 SDL_ttf·SDL_mixer·Box2D를 뺀다 | SpriteLab AOT 14.9 → 12.5 MB | 자동 판정 + 보고서 이유 + `GameSettings.features` 강제 포함(결정 4). 빌드 도구에서만 리플렉션을 쓴다. 빠진 기능을 부르면 추가 방법을 알려 주고 멈춘다. [size-wave2](../../engine/tests/results/size-wave2/README.md) |
| 15.12 | 판정을 코드 기능 8종으로 넓혀 AOT publish에 `MoNoon.Feature.<이름>` 상수를 넘긴다([EngineFeatures.cs](../../engine/src/Engine.Core/EngineFeatures.cs), `[FeatureSwitchDefinition]` + `Trim="true"`). VYaml 포매터 등록을 기능별 블록([PlayerYamlRegistration.cs](../../engine/src/Engine.Core/PlayerYamlRegistration.cs))으로 나눴다. 앞서 월드 체크포인트·스크립트 필드를 `JsonSerializer` 없이 읽고 쓴다 | AOT 실행 파일 −6.1~−17.5% | `#if` 없이 기존 `ExportedGameOnly`와 같은 방식이라 Release·Scene Play 코드가 그대로다. UI 전체(약 0.12 MB)는 동등성 확인이 별도 작업이라 남겼다. [aot-feature-switches](../../engine/tests/results/aot-feature-switches/README.md) |
| 15.13 | 쓰는 셀이 시트의 70% 미만(`GameBuild.RepackBelow`)이면 쓰는 셀과 1px 테두리만 새 시트로 패킹 | SpriteLab 아틀라스 171 → 153 B | 기존 빌드 도구 `AtlasPacker`(SkiaSharp, 배포 제외)를 재사용한다. 텍셀이 같아 Scene Play·Release·AOT 캡처가 바이트까지 같다. [size-wave3](../../engine/tests/results/size-wave3/README.md) |

- 하지 않기로 한 것: 글꼴 부분 집합(결정 3, 번역 테이블 때문에 빌드 때 쓰는 글자를 모으기 어렵다), 플레이어 Invariant 전역화(결정 5, PC 로케일을 따른다). 출처: [next-work 결정](../plans/next-work/README.md#결정-2026-11-24-사용자-확정).
- Release와 AOT의 `deps.json`에는 빠진 네이티브 파일 항목이 남는다. Linux 게스트에서 모든 Release가 시작·재생해 문제가 없음을 확인했다. Windows는 확인하지 않았다.
- `Brutal.Logging`은 AOT를 약 519 KiB 키우지만 BRUTAL 우선 원칙으로 유지한다(사용자 결정 2026-11-23, [brutal.md](../brutal.md)).

## Linux x64 회귀와 Windows 보류

- 2026-11-23 phase1 최종 회귀: 11장, BRUTAL Sdl 2026.10.3·Mixer·Ttf·Logging 전환, 성능·에디터 구조 작업을 macOS arm64와 Linux x64 게스트에서 함께 확인한 뒤 main에 병합했다(`d523ec7`). [phase1-regression](../../engine/tests/results/phase1-regression/README.md)
- 2026-11-24–25 13–16장 회귀: 보존한 Debian 12 게스트(TCG 에뮬레이션)에서 Mac이 만든 CLI와 linux-x64 템플릿·키트로 하네스 26개의 Release·Native AOT를 빌드·재생했다. Box2D Joints, 파티클 상태 해시, SDL_mixer 마지막 비트, OS 글꼴 HUD만 Mac과 달랐고 모두 기록만 하는 항목이다. 게스트에 맞게 검사 두 곳을 고쳤다(`MONOON_QA_SLOW`에서 파티클 시간 예산을 판정하지 않음, 하네스 목록에서 `__pycache__` 제외). [next1-x64](../../engine/tests/results/next1-x64/README.md)
- 게스트 빌드 한 번이 Release·AOT 각 약 8–9분이라 같은 준비 프로젝트를 쓰는 하네스의 빌드를 묶는 방법을 정리했다(확정 묶음만 −187분 추정). 매니페스트 `buildGroups`와 `--share-builds`는 **제안만** 있고 구현하지 않았다. 시작 씬을 기록에서 읽게 하는 플레이어 변경은 런타임 변경이라 제안하지 않았다. [build-groups.md](../../engine/tests/results/next1-x64/build-groups.md)
- Linux는 게임 실행 대상과 CI만이다(사용자 결정 2026-11-23). Windows 검증은 사용자가 장비 준비를 알릴 때까지 보류이고, 다시 할 항목은 [Windows 목록](../../engine/tests/results/next1-x64/README.md#windows-x64--장비-준비-후)에 있다.

## BRUTAL 사용 결정

[BRUTAL 기능 정리](../brutal.md)가 패키지와 유지 결정을 관리한다. 빌드·배포 쪽에서 정한 것:

- 게임 창·입력·렌더링은 `Brutal.Sdl`, 텍스트는 `Brutal.Sdl.Ttf`(빌드 때 굽던 글리프 시트와 StbTrueTypeSharp 제거, 11.13), 오디오는 `Brutal.Sdl.Mixer`, 물리는 `Brutal.Box2D`, 파티클 난수 형태는 `Brutal.Maths`다. Release·AOT에는 SkiaSharp·Avalonia·Engine.Editor·Engine.Authoring이 없고, 모든 배포 검사가 이를 확인한다.
- BRUTAL 패키지의 같은 네이티브 파일 사본은 한 벌만 배포하고(SDL_ttf 세 벌 → 하나, 약 4 MB), Windows `SDL3.dll`은 공식 빌드로 바꾼다(VCRUNTIME140 의존). [SDL 2026.10.3 전환](../../engine/tests/results/brutal-sdl-upgrade/README.md)
- 겹치지만 유지하는 구현(SDL 코어 PNG, 도구의 SkiaSharp, `System.IO.Compression`, `System.Numerics` 등)과 이유는 [유지 결정](../brutal.md#유지-결정) 표에 있다.

## 에이전트 개발 도구

[AI 작업 흐름 조사](../plans/next-work/5-ai-workflow.md)의 W 항목. 로드맵 대신 [개발 문서](../development.md#에이전트-개발-도구-진행-상태)에서 상태를 관리한다(결정 1). 모두 스크립트·테스트·설정만 바꿔 배포본 영향은 0이고 BRUTAL에 해당 기능이 없다. 2026-11-24에 구현했다.

| 항목 | 구현 | 이유·기록 |
| --- | --- | --- |
| W1 워크트리 준비 | [bootstrap-worktree.sh](../../engine/scripts/bootstrap-worktree.sh)가 메인 체크아웃의 패치한 `libSDL3.dylib`·Scene 브리지를 복사하고(새 mtime으로 복사해 SDL 재빌드 방지), 플레이어 소스 트리 해시로 `<메인>/.monoon-cache/players/<hash>` 템플릿을 링크한다. `build-scene-sdl-mac.sh`는 소스 묶음이 없으면 내려받지 않고 실패한다. [.worktreeinclude](../../.worktreeinclude), `worktree.baseRef: head`(결정 17) | 워크트리마다 SDL 재빌드·승인 안 된 내려받기(약 10분)와 템플릿 세 벌 재빌드를 없앤다. 캐시는 scratchpad가 아니라 메인 체크아웃(재부팅에 지워지지 않음, 결정 16). [agent-tooling](../../engine/tests/results/agent-tooling/README.md) |
| W2 공용 운영 문서 | `CLAUDE.md`는 `@AGENTS.md`를 가져오고 규칙은 [agent-workflow.md](../manual/agent-workflow.md)에 둔다. GUI 잠금은 [gui-lock.sh](../../engine/scripts/gui-lock.sh), 잠금 폴더는 `<메인>/.monoon-cache/gui.lock` | Codex와 Claude가 같은 규칙을 읽게 한다(결정 12). [agent-rules](../../engine/tests/results/agent-rules/README.md) |
| W3 프로브 라이브러리 | [Probe.cs](../../engine/tests/Shared/Probe.cs)의 `Start`·`Until`·`Delay`, 프로브가 열지 않은 창을 약 0.25초 안에 감지해 종료 코드 3. 에디터 하네스 제한 시간을 측정 기반 240–900 s로 줄였다 | 확인 창에서 15–30분 멈추던 일을 없앤다. 에디터 쪽 `MONOON_QA_MODAL`은 에디터 변경이라 범위에서 뺐다 |
| W4 하네스 매니페스트 | [harnesses.json](../../engine/tests/harnesses.json)과 [run-harness.py](../../engine/scripts/run-harness.py): 모드·필요 조건·`watch` glob·단계, `--changed` 선택, `summary.json`. `HarnessManifestChecks`가 폴더 목록과 대조 | README의 명령을 옮겨 적지 않고 바뀐 경로에 맞는 하네스만 돌린다. [agent-tooling-2](../../engine/tests/results/agent-tooling-2/README.md) |
| W5 검사 등록·계층 | `[Check(순서, 이름, StopAfter, Templates, Full)]` 특성, `--only`, `--tier fast`(템플릿 묶음 7개 생략). 옛 `--<이름>-only`는 한 단계 별칭 | `Program.cs`가 가장 잦은 병합 충돌 파일이었다(결정 16). 테스트 프로젝트라 리플렉션을 쓴다 |
| W7 권한·가드 | [settings.json](../../.claude/settings.json)의 deny, PreToolUse [guard.py](../../.claude/hooks/guard.py)(내려받기·`.test-env` 변경·잠금 없는 창 실행 차단, 종료 코드 2). 입력을 못 읽으면 통과 | 브리프 문장으로만 있던 금지 규칙을 강제한다(결정 13). 가드 결함 하나가 모든 세션을 멈추지 않게 실패 시 연다. Bash sandbox는 `caffeinate`·`screencapture`와의 충돌 확인 전이라 보류 |
| W8 skill·서브에이전트 | `.claude/skills/`의 4개, `.claude/agents/`의 `implementer`(워크트리 격리), `harness-runner`(Sonnet, 낮은 effort, 편집 없음) | 저장소 스크립트를 부르는 얇은 안내로 두어 스크립트만 고치면 되게 했다 |
| W10(a) 결정성 정적 검사 | `DeterminismScanChecks`(`determinism-scan`)가 런타임 소스의 FMA·`SinCos`·Brutal.Numerics 융합 경로 API를 막는다. 그리기 전용 파일만 허용 목록 | x64 비결정성을 QEMU 회귀 전에 잡는다. osx-x64 팩·Rosetta 비교는 보류(결정 15) |

- 하지 않은 것: W6(에디터 자동화 API, 프로브는 아직 리플렉션), W9(기준선 캐시·A/B 실행기), W11의 `integrate.sh`, W13(창 없는 Scene Play, Avalonia.Headless 내려받기 필요). 상태 표는 [개발 문서](../development.md#에이전트-개발-도구-진행-상태)에 있다.
