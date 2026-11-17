# 세 데스크톱에서 InventoryLab 입력 재생

macOS arm64, Windows x64 또는 Linux x64에서 저장소 루트를 연다. Python 3, .NET 10 SDK, 해당 OS의 Native AOT 도구가 필요하다. macOS에서는 Xcode 명령줄 도구, Debian·Ubuntu에서는 clang과 zlib 개발 파일, Windows에서는 MSVC x64 빌드 도구와 Windows SDK를 사용한다. [Native AOT 플랫폼 요구 사항](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)

```sh
python3 engine/tests/InventoryLab.Replay/build_guest.py game-builds/InventoryLab game-builds/replay-results
```

Windows에서는 같은 인자를 `python.exe`에 전달한다. 첫 인자는 게임 출력 폴더이고 두 번째 인자는 이번 결과를 보관할 새 폴더다. 실행기는 현재 OS의 SDK로 Release 템플릿과 CLI를 컴파일하고, 같은 엔진 소스에서 준비한 AOT 키트로 두 배포본을 만든다. 두 실행 파일 모두 별도의 빈 저장 폴더에서 `--replay-input`으로 시작한다. 테스트에서는 SDL 비디오와 오디오 드라이버를 사용할 수 없도록 설정한다. `--expected-rid`에 대상 RID를 지정하면 현재 OS와 CPU도 확인한다.

결과 폴더에는 각 실행 파일이 만든 `release.save`, `native-aot.save`와 해당 빌드의 `.receipt`가 남는다. 두 저장 파일을 원래 Mac에서 기록한 기준 저장과 각각 바이트 단위로 비교한다. Windows 체크아웃에서도 기준 저장 파일의 바이트를 보존하도록 기준 파일이 있는 폴더에 Git 줄 바꿈 규칙을 지정했다. 입력 SHA-256, 저장 크기와 SHA-256, SDK·OS 정보는 표준 출력에 기록된다. [공통 입력](../fixtures/InventoryLab/Herbalist.inputreplay) · [Mac 기준 저장](../fixtures/InventoryLab/Herbalist.save)

## 버전 31 → 35 업그레이드

버전 31 커밋 `6232209b5ce4a2c7516db0c8fd989ba63a8e77c5`가 있는 체크아웃에서 실행한다. 각 실행에는 새 결과 폴더를 지정한다.

```sh
python3 engine/tests/InventoryLab.Replay/build_guest.py game-builds/InventoryLab-upgrade game-builds/upgrade-results --upgrade-from-v31
```

실행기는 Git에서 당시 InventoryLab 소스를 임시 폴더로 추출하고 버전 35 에디터의 `--migrate-project`를 실행한다. 기존 YAML 프로젝트의 파일과 GUID가 변환·빌드 뒤에도 보존됐는지 검사한다. 각 배포 플레이어를 헤드리스로 두 번 실행한다. 첫 실행은 약초 두 포기와 진행 중인 의뢰가 든 v2 슬롯을 `L`로 읽고 126바이트가 보존됐는지 확인한다. 두 번째 실행은 `L 1 T 2 1 S`로 의뢰를 마치고, 최종 v3 슬롯 171바이트를 Mac 기준과 비교한다. 결과 폴더의 `*-loaded.save`는 읽기 직후의 슬롯이고 `release.save`와 `native-aot.save`는 보상 수령 후 슬롯이다. [v2 입력](../results/inventorylab-upgrade-x64/v2-input.save) · [Mac 기준 저장](../fixtures/InventoryLab/Herbalist.save)

## 저장 슬롯 복구 회귀

게스트 빌드 명령에 `--slot-recovery`를 붙이면 Release와 Native AOT를 빌드한 후 두 플레이어에서 손상·복구 검사를 이어서 실행한다. 이미 빌드한 게임을 검사할 때는 실행 파일과 기준 입력 폴더, 새 결과 폴더를 지정한다. 예를 들어 macOS Release에서는 다음과 같이 실행한다.

```sh
python3 engine/tests/InventoryLab.Replay/check_recovery.py \
  game-builds/InventoryLab/InventoryLab.app/Contents/MacOS/MoNoonPlayer \
  engine/tests/fixtures/InventoryLab game-builds/slot-recovery-mac-release
```

실행기는 임시 프로필에서 약초상 입력을 재생하고 서로 다른 두 저장본을 만든다. 최신 슬롯을 잘라내거나 수량만 편집한 뒤 새 게임 프로세스에서 L로 읽는다. 수령 상태와 수량, 복원한 파일 전체, 보상 재청구 직후의 대화 분기와 재저장한 백업을 검사한다. [세 OS의 검증 결과](../results/slot-recovery/README.md)
