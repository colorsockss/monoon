# 기존 게임 프로젝트 업그레이드

대상은 MoNoon Engine 콘텐츠 버전 **35**다(이 절차를 검증한 버전이다. 지금 엔진은 쓰는 기능에 따라 씬 스키마 42까지 저장하고, 순서는 같다). 1~5단계의 명령은 Apple Silicon Mac 기준이고, 마지막 절에는 Windows x64·Linux x64의 실행 경로와 저장 비교 방법이 있다. 아래 순서로 버전 31 당시의 InventoryLab 소스를 복사해 최신 에디터로 열 수 있는지 확인하고, 배포용 Release와 Native AOT를 각각 만든다. 모든 명령은 **현재 엔진 저장소의 루트에서 같은 셸**로 실행한다. 두 빌드 형식의 산출물과 심볼은 기존 InventoryLab 배포본과 다른 폴더에 보관한다. [빌더의 버전 검사](../../engine/src/Engine.Authoring/GameBuild.cs) · [샘플 API](api.md)

## 1. 원본과 복구 지점 확보

에디터에서 편집을 저장하고 프로젝트를 닫는다. 실제 게임은 소스 전체를 복사하거나 현재 리비전을 태그해 둔다. `Assets/`, `Scenes/`, `Scripts/`, 설정, 숨김 매니페스트와 모든 `.meta`를 포함한다. 이전 에디터·템플릿, 배포 앱과 빌드 ID별 심볼도 보관한다. 플레이어 저장은 소스 트리 밖에 있으므로 이전 저장 호환성을 시험할 때는 사용자 프로필의 사본을 사용한다.

예제는 Git에 보관된 당시 소스를 추출한다. 엔진 체크아웃을 바꿀 필요가 없다. 이미 보관한 자기 게임으로 진행한다면 `git archive`와 `tar` 대신 `work/original` 아래에 전체 소스를 준비하고 `ditto`의 원본 경로를 바꾼다.

```sh
repo="$PWD"
work=$(mktemp -d "${TMPDIR:-/tmp}/monoon-upgrade.XXXXXX")
old=6232209b5ce4a2c7516db0c8fd989ba63a8e77c5
mkdir -p "$work/original"
git archive --output="$work/source.tar" "$old" samples/InventoryLab
tar -xf "$work/source.tar" -C "$work/original"
project="$work/InventoryLab"
ditto "$work/original/samples/InventoryLab" "$project"
out="$repo/game-builds/InventoryLab-upgrade"
export MONOON_USER_DATA="$work/player-data"
```

버전 31 샘플은 이미 YAML 단일 확장자를 사용하고 `contentBundles` 설정이 없다. 그대로 빌드하면 콘텐츠를 개별 파일로 읽는 기존 방식으로 플레이할 수 있다. 실습에서는 원래 약초 의뢰의 대사와 수정 1개 보상을 기준으로 사용한다. 예제의 앱은 `game-builds/InventoryLab-upgrade/`에 생성된다. 자기 게임에는 별도의 새 출력 폴더를 지정한다. `MONOON_USER_DATA`는 이 셸에서 실행할 게임의 저장·모드·업데이트를 임시 폴더로 분리한다.

## 2. 에디터·플레이어 템플릿·AOT 키트 맞추기

소스에서 준비하는 경우 .NET 10 SDK와 Xcode 명령줄 도구를 사용한다. Mac 에디터의 패치된 SDL을 처음 컴파일하는 환경에서는 CMake 3.24 이상도 필요하다. 아래에서 템플릿과 키트의 숫자는 모두 `35`여야 한다. 이미 같은 리비전의 에디터와 템플릿을 받아 사용한다면 버전 세 곳을 확인하고 3단계로 간다.

```sh
dotnet --version
xcrun --find clang
export MONOON_PLAYERS_DIR="$repo/player-templates"
./engine/scripts/build-editor-mac.sh
./engine/scripts/build-players.sh osx-arm64
cat engine/src/Engine.Player/MoNoonContent.version
cat "$MONOON_PLAYERS_DIR/osx-arm64/MoNoonContent.version"
cat "$MONOON_PLAYERS_DIR/native-aot/src/Engine.Player/MoNoonContent.version"
```

`MoNoonContent.version`은 빌더와 플레이어의 호환 번호다. 씬의 `schemaVersion`은 사용하는 컴포넌트에 따라 정해진다. InventoryLab의 씬에는 `14`가 기록돼 있다. 저장 슬롯의 `version`은 게임 스크립트가 정의하며 InventoryLab은 `3`을 쓴다. 버전 불일치 메시지가 나오면 해당 리비전의 템플릿·키트를 다시 만들고 세 경로를 확인한다. [씬 스키마](../../engine/src/Engine.Core/SceneDocument.cs) · [슬롯 버전](../../samples/InventoryLab/Scripts/WorkbenchSave.cs)

## 3. 작업 사본 변환과 참조 확인

```sh
editor="$repo/editor/MoNoon.app/Contents/MacOS/MoNoon"
"$editor" --migrate-project "$project"
cat "$project/GameSettings"
cat "$project/Scenes/Inventory.scene.meta"
```

`YAML project`와 종료 코드 0을 확인한다. 에디터 없이 `./monoon migrate "$project"`로도 같은 변환을 한다. **File → Open Project…**로 소스 폴더를 여는 방법도 같은 변환기를 사용한다. 변환기는 출고용 `Game/` 안의 `.monoon-player`를 인식해 건너뛰므로 반드시 소스 프로젝트를 지정한다. [변환기](../../engine/src/Engine.Authoring/LegacyProject.cs)

| 기존 프로젝트의 상태 | 변환 결과와 확인할 내용 |
| --- | --- |
| 이미 `GameSettings`, `.monoon-assets`, `.scene`을 사용 | 형식과 프로젝트 ID가 현재 조건을 충족하면 새 ZIP이 생기지 않는다. 버전 31 InventoryLab이 이 경우다. |
| `.scene.yaml`, `.prefab.yaml`, `.clip.yaml` 또는 대응하는 `.json` 사용 | 목적지는 `.scene`, `.prefab`, `.clip`이다. 해당 `.meta`도 이동하고 기존 GUID를 유지한다. `GameSettings.yaml/json`과 `.monoon-assets.yaml/json`도 각각 단일 이름으로 옮긴다. 구형 JSON 씬의 내장 이동·획득 동작은 해당되는 씬에서 프로젝트 스크립트로 변환한다. |
| 목적지에 내용이 다른 파일이 있음 | 오류에 나온 원본·목적지를 소스 백업과 비교한다. 작업 사본에서 사용할 버전을 결정한 뒤 재실행한다. |
| `projectId`가 없음 | 변환기가 새 ID를 발급할 수 있다. 이미 배포한 게임이면 이전 앱의 `Game/GameSettings`와 사용자 프로필을 먼저 대조하고, 작업 사본에 기존 게임의 ID를 유지한다. 이 ID가 슬롯·모드·콘텐츠 업데이트를 찾는 기준이다. |

파일을 변환하면 로그의 `Migration backup`이 `.monoon/migrations/*.zip`을 가리킨다. ZIP에는 변환 대상 중 원래 존재하던 파일이 들어 있다. 전체 프로젝트로 되돌릴 때는 1단계의 소스 사본을 쓴다. [마이그레이션 백업](../../engine/src/Engine.Authoring/MigrationBackup.cs)

InventoryLab의 `projectId`는 `ade09cc3-a769-56d7-9832-a2bea14c7e71`이고, `startScene`과 `Inventory.scene.meta`의 ID는 모두 `6a7b735e-336d-5674-b2e7-a136db5a7771`이다. 시작 씬이 `includedScenes`에 포함되는지 확인한다. 에디터에서는 **Project 패널 → + → Check Missing References…**로 끊긴 연결을 찾을 수 있다. 스크립트 ID는 `Scripts/` 아래 상대 경로를 사용하므로 폴더나 파일 이름이 달라졌다면 씬·프리팹의 연결도 확인한다. InventoryLab에서는 **Bag**의 `InventoryContainer.Catalog`, `QuestLog.Catalog`, `DialoguePlayer.Story`가 각각 아이템 표, 의뢰 표, 약초상 대사를 가리킨다.

## 4. 배포용 앱 두 개 빌드하기

```sh
./monoon build "$project" release --target osx-arm64 --profile distribution --output "$out"
./monoon build "$project" native-aot --target osx-arm64 --profile distribution --output "$out"
```

| 확인할 항목 | Release | Native AOT |
| --- | --- | --- |
| 게임 | `$out/InventoryLab.app` | `$out/NativeAOT/InventoryLab.app` |
| 스크립트 | 앱 내부 `Contents/MacOS/Game/.monoon-compiled/MoNoon.GameScripts.dll` | 게임 스크립트와 생성된 팩터리가 `MoNoonPlayer`에 정적으로 컴파일됨 |
| 별도 심볼 | `$out/Symbols/InventoryLab/osx-arm64/release/<빌드 ID>/` | 같은 트리의 `native-aot/<빌드 ID>/` |

각 앱의 `Contents/MacOS/MoNoonBuild`에 기록된 `buildId`로 심볼 폴더를 연결한다. AOT는 대상 OS·아키텍처에서 빌드한다. 두 번째 빌드에서 스크립트가 실패하면 콘솔의 첫 C# 오류부터 확인한다. 프로젝트의 C# 소스 전체와 공개 필드 연결 코드가 두 형식 모두에서 컴파일되어야 한다. [스크립트 컴파일 규칙](scripting.md) · [크래시 보고서와 심볼](build.md#크래시-진단)

## 5. 직접 실행해 업그레이드 판정하기

```sh
./monoon test inventorylab "$out" --profile distribution
./monoon test replay "$out"
```

첫 명령은 실제 플레이어를 여러 번 시작해 의뢰 제안 → 보상 수령 → 재실행 후 재수령 시도의 슬롯을 비교하고, 앱과 보관 심볼의 대응도 검사한다. 둘째 명령은 같은 475프레임 입력을 두 앱에서 창 없이 재생한다. 합격 기준은 약초 3개, 수정 1개, 보상 수령 완료, 대화 노드 `paid`이고 두 저장 파일 전체가 Mac 기준 171바이트와 같아야 한다. 다른 게임을 올릴 때는 그 게임의 프로젝트·시작 씬으로 기록한 입력과 기존 기준 저장으로 검사한다. [InventoryLab 기준 입력·저장](../../engine/tests/fixtures/InventoryLab/) · [입력 리플레이](build.md#헤드리스-입력-리플레이)

기존 이용자의 세이브도 시험하려면 구버전 슬롯을 별도 폴더에 복사해 그 폴더를 플레이어의 `--smoke-test` 인자로 지정한다. InventoryLab은 **L**로 메모리에서 v1·v2를 읽고 **S**로 저장할 때 v3 슬롯을 쓴다. 의뢰가 진행 중인 슬롯이라면 가방·수집량·수령 여부를 함께 비교한다. 읽기에 실패했을 때는 저장하기 전에 복사본에서 원인을 확인한다. [저장 API와 버전 이전](api.md#저장-슬롯과-버전-이전)

다음은 진행 중인 v2 슬롯의 예다. 약초 두 포기를 가진 상태에서 의뢰를 이어서 마치기까지 확인할 수 있다. 아래 명령은 1단계의 새 작업 폴더에서 실행한다.

```sh
mkdir -p "$work/v2-release" "$work/v2-aot"
cat > "$work/previous.save" <<'SAVE'
version: 2
values:
  capacity: 3
  item0: "herb"
  count0: 2
  quests:
    herbalist:
      collected: 2
      claimed: false
SAVE
release="$out/InventoryLab.app/Contents/MacOS/MoNoonPlayer"
aot="$out/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer"
cp "$work/previous.save" "$work/v2-release/slot-001.save"
cp "$work/previous.save" "$work/v2-aot/slot-001.save"
"$release" --smoke-test "$work/v2-release" L
"$aot" --smoke-test "$work/v2-aot" L
cmp "$work/previous.save" "$work/v2-release/slot-001.save"
cmp "$work/previous.save" "$work/v2-aot/slot-001.save"
"$release" --smoke-test "$work/v2-release" L 1 T 2 1 S
"$aot" --smoke-test "$work/v2-aot" L 1 T 2 1 S
cmp engine/tests/fixtures/InventoryLab/Herbalist.save "$work/v2-release/slot-001.save"
cmp engine/tests/fixtures/InventoryLab/Herbalist.save "$work/v2-aot/slot-001.save"
```

처음 두 `cmp`는 L로 읽기만 한 v2 파일이 원본과 같은지 확인한다. 마지막 두 `cmp`는 약초 한 포기를 추가하고 보상을 받은 뒤 S로 저장한 v3 파일이 기준과 같은지 확인한다. 모두 일치하면 `cmp`는 출력 없이 종료 코드 0을 반환한다.

새 배포에 모드나 업데이트를 제공했던 게임은 해제 상태에서 기준 동작을 먼저 확인하고, 복사한 프로필에서 기존 모드와 활성 번들도 시험한다. 업데이트가 참조한 기본 매니페스트가 바뀌면 새 배포본의 매니페스트로 업데이트 번들을 다시 발행한다. 검증이 끝난 뒤 새 앱과 이번 빌드의 심볼을 함께 보관한다. 문제가 생기면 기존 배포 앱과 그 버전의 저장 사본으로 복귀할 수 있다. [콘텐츠 업데이트](build.md#콘텐츠-업데이트) · [이 Mac의 업그레이드 검증 기록](../verification/inventorylab-upgrade.md)

## Windows x64와 Linux x64에서 같은 슬롯 검사하기

각 OS에서 버전 31 프로젝트의 작업 사본을 준비한다. 같은 버전 35 엔진 소스로 CLI와 해당 RID의 Release 템플릿을 빌드하고 Native AOT 키트를 준비한다. Windows에서는 MSVC x64와 Windows SDK, Linux에서는 clang과 zlib 개발 파일이 필요하다. CLI를 빌드한 폴더에서 `dotnet MoNoon.Cli.dll migrate <프로젝트>`를 실행하면 에디터 없이 3단계를 수행할 수 있다. Linux에는 에디터가 없으므로(2026-11-23부터) 이 명령을 쓴다. [게스트별 SDK와 빌드 환경](../troubleshooting/README.md)

아래는 보존된 게스트에서 사용한 폴더 배치다. 작업 루트는 Windows에서 `C:\MN3135`, Debian에서 `~/MN3135`다. CLI는 `cli/`, 버전 35 템플릿과 AOT 키트는 `templates/`, 변환한 샘플은 `samples/InventoryLab/`에 둔다.

Windows PowerShell에서는 작업 루트에서 실행한다.

```powershell
$env:MONOON_PLAYERS_DIR = (Resolve-Path templates).Path
dotnet cli/MoNoon.Cli.dll build samples/InventoryLab release --target win-x64 --profile distribution --output exports
dotnet cli/MoNoon.Cli.dll build samples/InventoryLab native-aot --target win-x64 --profile distribution --output exports
```

Linux에서는 작업 루트에서 실행한다.

```sh
export MONOON_PLAYERS_DIR="$PWD/templates"
dotnet cli/MoNoon.Cli.dll build samples/InventoryLab release --target linux-x64 --profile distribution --output exports
dotnet cli/MoNoon.Cli.dll build samples/InventoryLab native-aot --target linux-x64 --profile distribution --output exports
```

다음 경로는 생성된 `exports/` 폴더를 기준으로 한다.

| 게스트 | Release 실행 파일 | Native AOT 실행 파일 |
| --- | --- | --- |
| Windows x64 | `InventoryLab-win-x64/MoNoonPlayer.exe` | `NativeAOT/InventoryLab-win-x64/MoNoonPlayer.exe` |
| Linux x64 | `InventoryLab-linux-x64/MoNoonPlayer` | `NativeAOT/InventoryLab-linux-x64/MoNoonPlayer` |

빌드마다 빈 슬롯 폴더를 하나씩 만들고 v2 입력 파일을 `slot-001.save`로 복사한다. 5단계처럼 `--smoke-test <슬롯 폴더> L`을 실행해 입력 파일과 비교한 후, 같은 폴더로 `--smoke-test <슬롯 폴더> L 1 T 2 1 S`를 실행해 Mac 기준 저장과 비교한다. 바이트 비교에는 Windows의 `fc.exe /b` 또는 Linux의 `cmp`를 쓸 수 있다. [v2 입력 파일](../../engine/tests/results/inventorylab-upgrade-x64/v2-input.save) · [Mac v3 기준 저장](../../engine/tests/fixtures/InventoryLab/Herbalist.save)
