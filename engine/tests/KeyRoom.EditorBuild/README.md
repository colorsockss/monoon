# KeyRoom 에디터 빌드와 CLI 빌드 비교

배포 에디터로 KeyRoom 복사본을 열고 **Build → Build Game…** 대화 상자의 컨트롤만으로 빌드 설정을 바꿔 빌드한다. 같은 복사본을 `./monoon build <프로젝트>`(인자 없음)로 다시 빌드해 두 결과를 파일별 SHA-256으로 비교한다. macOS arm64 전용이다.

1. 메인 창을 1280px과 1920px로 바꿔 대화 상자를 열고 캡처한다. 8개 행(Start scene부터 Icon까지)의 레이블 x, 입력 x, 세로 중심, 콤보 높이, 아래 버튼 세 개의 y를 측정한다.
2. **Platform** `osx-arm64`, **Build type** Release, **Profile** Distribution, **Package** 체크, **Icon** `Assets/Key.png`로 바꾸고 **Save Settings**를 누른다. 다시 열어 같은 값이 보이는지 확인한 뒤 **Build**를 누른다(Release, 서명·zip·dmg).
3. 출력 폴더를 옮겨 두고 `./monoon build <복사본>`을 실행한다. `BuildSettings`가 바뀌지 않았는지 확인하고 두 결과를 비교한다.
4. 에디터를 다시 시작한다. 저장한 값이 그대로 보이는지 확인한다. `win-x64` Native AOT가 저장된 파일로 열면 **Release**만 표시되는지 보고 파일을 되돌린다. Platform을 `linux-x64`로 바꾸면 Build type이 Release뿐인지 확인하고, `osx-arm64` · **Native AOT**로 **Build**를 누른다.
5. 3과 같이 CLI로 다시 빌드해 비교한다. 마지막으로 `win-x64` Native AOT가 저장된 상태에서 CLI가 종료 코드 1과 `Build the win-x64 Native AOT player on a matching host.`로 끝나는지 확인한다.

에디터가 대화 상자 외의 창(오류 상자, 폴더 선택 창 등)을 열면 [검사 훅](StartupHook.cs)이 즉시 `error.txt`를 쓰고 종료한다.

## 비교 규칙

앱, `Symbols/`, zip과 dmg를 펼친 내용까지 모든 항목의 종류, SHA-256, 실행 비트, 심볼릭 링크 대상이 같아야 한다. 출력 옆의 `.monoon-build-cache`와 임시 폴더는 산출물이 아니므로 제외한다. 아래 경우만 차이를 허용하며 각각 남은 부분은 다시 비교한다.

| 파일 | 다른 이유 | 대신 비교하는 것 |
| --- | --- | --- |
| `Contents/MacOS/MoNoonBuild`, `Symbols/.../MoNoonBuild` | 영수증의 `buildId`는 빌드마다 새로 만드는 무작위 ID이며 심볼 폴더 이름이다. Native AOT는 링커 UUID(`nativeUuid`)와 그 UUID가 든 실행 파일의 해시(`playerSha256`)도 기록한다 | 이 줄들을 뺀 나머지 줄 |
| `Contents/_CodeSignature/CodeResources` | 번들 봉인이 영수증의 해시를 포함한다 | 영수증 항목을 뺀 봉인 plist |
| `Contents/MacOS/MoNoonPlayer` | 내장 ad-hoc 서명이 봉인을 포함한다. Native AOT는 링커가 만든 `LC_UUID`도 다르다 | `codesign --remove-signature` 후 SHA-256(Native AOT는 `LC_UUID` 16바이트를 0으로 바꾼 뒤) |
| Native AOT `MoNoonPlayer.dSYM/.../DWARF/MoNoonPlayer` | 실행 파일과 같은 `LC_UUID`를 가진다 | `LC_UUID`를 0으로 바꾼 SHA-256 |
| Native AOT `MoNoonPlayer.dSYM/.../Relocations/aarch64/MoNoonPlayer.yml` | `binary-path`가 빌드마다 새로 만드는 임시 링크 폴더다 | `binary-path` 줄을 뺀 나머지 줄 |
| `*.zip`, `*.dmg` 자체 | 컨테이너에 파일 시간이 들어간다 | 펼친 내용을 위 규칙으로 파일별 비교 |

Native AOT의 `LC_UUID`는 ld가 링크할 때 목적 파일의 디버그 맵(임시 작업 폴더 경로와 파일 시각)을 포함한 내용으로 계산한다. UUID만 다르고 나머지 바이트는 같다.

심볼 폴더 경로의 build ID는 `<buildId>`로 바꿔 짝을 맞춘다. 게임 스크립트 DLL·PDB를 포함한 그 밖의 모든 파일은 바이트가 같아야 한다.

## 실행

저장소 루트에서 실행한다. 결과 폴더는 실행마다 새 경로를 쓴다. 서명 설정은 결과 폴더의 빈 프로필을 쓰므로 ad-hoc 서명이고 공증은 하지 않는다.

```sh
qa=engine/tests/KeyRoom.EditorBuild
engine/scripts/build-players.sh osx-arm64
engine/scripts/build-editor-mac.sh
dotnet build "$qa" -c Release -m:1 -p:UsedAvaloniaProducts=
python3 "$qa/editor.py" editor/MoNoon.app/Contents/MacOS/MoNoon samples/KeyRoom \
  engine/.artifacts/bin/EditorBuildProbe/release/EditorBuildProbe.dll "$(mktemp -d)/editor-build"
```

결과 폴더에는 `dialog-1280.png`, `dialog-1920.png`, `dialog-reopened.png`, 빌드 직후 Console 캡처 `console-release.png`·`console-aot.png`, 단계별 `result-*.json`(Console 줄 포함, 에셋 보고서 요약과 경로를 확인한다), 비교 결과 `compare-release.json`·`compare-native-aot.json`, 에디터·CLI 로그, 네 빌드(`editor-release`, `cli-release`, `editor-aot`, `cli-aot`)가 남는다. [Mac 결과](../results/editor-build/README.md)
