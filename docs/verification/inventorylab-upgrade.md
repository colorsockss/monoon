# InventoryLab 프로젝트 업그레이드 검증

2026-11-15, Apple Silicon Mac, macOS 27.0.1 (26A434), .NET SDK 10.0.101. 엔진 콘텐츠 버전 31 당시의 커밋 `6232209b5ce4a2c7516db0c8fd989ba63a8e77c5`에서 InventoryLab 소스 전체를 추출했다. [업그레이드 가이드](../manual/upgrade.md)의 명령으로 버전 35 에디터, osx-arm64 Release 템플릿, Native AOT 키트를 준비하고 프로젝트 복사본을 빌드했다. 실행 대상은 독립 배포 게임이었고 스모크 테스트와 입력 재생은 창 없이 수행했다.

| 출발 프로젝트 | Release 빌드 ID | Native AOT 빌드 ID | 확인 결과 |
| --- | --- | --- | --- |
| 버전 31의 실제 InventoryLab | `4c6eeeff17384d45943059dd95f917ad` | `203861441b734256a0fa3692308d6127` | 변환 명령과 빌드 후 소스 디렉터리가 보관본과 동일. 두 앱의 의뢰·저장 검사와 리플레이 통과 |
| 같은 샘플로 구형 이름·메타·설정 형식을 재현한 별도 사본 | `85162d8121b64f2281c096e515417c57` | `f478bae7dd2d43f0808255892bb0e059` | 실제 파일 변환 후 두 앱을 새로 빌드했고 리플레이 결과가 같은 기준 저장과 일치 |

**파일 변환.** 버전 31 원본은 이미 단일 확장자 YAML이어서 변환 ZIP이 생기지 않았다. 두 번째 실험에서는 씬 1개와 프리팹 3개를 `.yaml` 이중 확장자로, 해당 메타 4개를 GUID 한 줄 형식으로, 매니페스트를 구형 이름·경로로, 설정을 `GameSettings.json`으로 준비했다. 변환기가 만든 ZIP의 10개 항목을 변환 전 파일과 바이트 단위로 대조했다. 변환 후 씬·프리팹 본문 4개와 에셋 GUID 7개, 프로젝트 ID, 시작 씬 ID가 원본과 일치했다. 변환을 다시 실행해도 소스 트리와 백업 개수는 같았다.

**실행과 저장.** Release와 Native AOT의 의뢰 제안, 보상 지급, 재실행 후 보상 재요청까지 세 시점에서 저장 바이트가 일치했다. 두 프로젝트에서 빌드한 네 앱 모두 475프레임·31이벤트의 같은 입력을 재생해 약초 3개, 수정 1개, `claimed: true`, 대화 노드 `paid`를 기록했다. 저장 크기는 171바이트, SHA-256은 `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795`였다. [기준 입력](../../engine/tests/fixtures/InventoryLab/Herbalist.inputreplay) · [기준 저장](../../engine/tests/fixtures/InventoryLab/Herbalist.save)

| 버전 31 소스로 빌드한 두 앱에 제공한 테스트 슬롯 | L로 읽은 직후 | S로 다시 저장한 결과 |
| --- | --- | --- |
| v1, 첫 칸에 열쇠 1개 | v1 파일의 원본 바이트 유지 | 열쇠의 위치와 수량을 보존하고 빈 의뢰 목록·닫힌 대화를 가진 동일한 v3 저장 생성 |
| v2, 가방과 의뢰에 약초 2개, 보상 미수령 | v2 파일의 원본 바이트 유지 | 약초 한 포기를 더 모아 보상 수령. 두 앱 모두 위 v3 기준 저장과 일치 |

**산출물.** 버전 31 원본으로 만든 배포용 두 앱과 해당 심볼을 `game-builds/InventoryLab-upgrade/`에 보관했다. 프로필 검사에서 Release DLL과 보관 PDB들이 대응했고, Native AOT 실행 파일과 dSYM의 arm64 UUID는 모두 `90EC1C03-5BCC-30E9-8F80-3BE52AD068C2`였다. 기존 사용자의 저장·모드와 분리한 임시 프로필에서 기본 보상을 확인했다. [빌드 프로필과 심볼 경로](../manual/build.md#개발용배포용-프로필과-디버그-심볼)

## Windows x64·Linux x64 게스트 검증

2026-11-15에 같은 Mac의 기존 QEMU Windows 11 Enterprise LTSC x64(10.0.26100.1742)와 Debian 12.15 amd64(커널 6.1.0-54-cloud-amd64)를 사용했다. 버전 31 커밋에서 추출한 InventoryLab 파일 25개를 각 게스트 안에서 작업 사본으로 복사했다. 두 OS 모두 x64 .NET SDK 10.0.101로 버전 35 에디터를 빌드해 `--migrate-project`를 실행했다(Linux 에디터 지원을 멈춘 2026-11-23 전 기록이다. 지금은 에디터 없이 `monoon migrate`로 같은 변환을 한다). 변환 직후와 두 게임을 빌드한 후에 파일 목록과 각 파일의 SHA-256이 버전 31 보관본과 동일했다. 프로젝트 ID는 `ade09cc3-a769-56d7-9832-a2bea14c7e71`로 유지됐다.

두 게스트에서 각각 콘텐츠 버전 35인 플레이어 소스·Release 템플릿·AOT 키트를 확인하고 `distribution` 프로필로 두 게임을 직접 빌드했다. 아래 저장 파일은 각 배포 플레이어에서 회수한 원본이다.

| 게스트 | 게임 빌드 | 빌드 ID | L 직후 v2 | 보상 수령·S 직후 v3 |
| --- | --- | --- | --- | --- |
| Windows x64 | Release | [09c257b3](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/release.receipt) | [126바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/release-loaded.save) | [171바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/release.save) |
| Windows x64 | Native AOT | [c3871ec0](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/native-aot.receipt) | [126바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/native-aot-loaded.save) | [171바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/native-aot.save) |
| Debian x64 | Release | [53a27d25](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/release.receipt) | [126바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/release-loaded.save) | [171바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/release.save) |
| Debian x64 | Native AOT | [d655e8c0](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/native-aot.receipt) | [126바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/native-aot-loaded.save) | [171바이트 일치](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/native-aot.save) |

각 배포본에 Mac에서 사용한 v2 슬롯을 별도로 복사하고 창 없이 두 번 실행했다. 첫 실행에서 `L`만 눌렀을 때 입력 파일의 SHA-256은 그대로 `7450a6e0d3dab14ec6f69717f9b13e4f733197c7cb8a8f183738fc51bd13e1b2`였다. 두 번째 실행의 `L 1 T 2 1 S`는 진행 중인 의뢰에서 약초 한 포기를 더 모으고 약초상에게 보상을 받아 저장하는 입력이다. 네 v3 슬롯 모두 약초 3개, 수정 1개, 의뢰 수집량 3, `claimed: true`, 대화 노드 `paid`를 담았다. [시작 슬롯](../../engine/tests/results/inventorylab-upgrade-x64/v2-input.save) · [입력 바인딩과 샘플 API](../manual/api.md)

Linux 결과는 SSH로, Windows 결과는 정상 종료한 VM의 가상 USB에서 회수했다. Mac에서 네 최종 슬롯을 이 문서 첫 실험의 Mac Release·Native AOT 결과 및 저장소 기준 파일과 각각 바이트 단위로 대조했다. 모두 171바이트이고 SHA-256은 `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795`다. [호스트 비교 기록](../../engine/tests/results/inventorylab-upgrade-x64/comparison.txt) · [Mac 기준 파일](../../engine/tests/fixtures/InventoryLab/Herbalist.save) · [Windows 빌드·실행 로그](../../engine/tests/results/inventorylab-upgrade-x64/win-x64/build.log) · [Debian 빌드·실행 로그](../../engine/tests/results/inventorylab-upgrade-x64/linux-x64/build.log)

게스트 디스크에 변환한 소스와 각 배포본·심볼을 보관했다. 빌드 출력 위치는 Windows의 `C:\MN3135\exports`와 Debian의 `~/MN3135/exports`다. [게스트별 빌드 및 재실행 명령](../manual/upgrade.md#windows-x64와-linux-x64에서-같은-슬롯-검사하기)
