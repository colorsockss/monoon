# InventoryLab 플랫폼별 헤드리스 회귀 검증

2026-11-14에 이 Mac의 기존 QEMU Windows 11 Enterprise LTSC x64(26100.1742)와 Debian 12.15 amd64(커널 6.1.0-54-cloud-amd64)에서 실행했다. 두 게스트 모두 x64 .NET SDK 10.0.101로 소스에서 플레이어 템플릿과 빌드 CLI를 컴파일하고 InventoryLab을 배포용 Release와 Native AOT로 내보냈다. Linux는 clang 14.0.6, Windows는 MSVC v143과 Windows SDK 10.0.26100 계열을 사용했다.

| 게스트 | 배포본 | 빌드 기록 | 회수한 저장 파일 | Mac 원본과 비교 |
| --- | --- | --- | --- | --- |
| Windows x64 | Release | [02774137](win-x64/release.receipt) | [release.save](win-x64/release.save) | 171바이트 일치 |
| Windows x64 | Native AOT | [54bc0926](win-x64/native-aot.receipt) | [native-aot.save](win-x64/native-aot.save) | 171바이트 일치 |
| Debian x64 | Release | [052cf7c9](linux-x64/release.receipt) | [release.save](linux-x64/release.save) | 171바이트 일치 |
| Debian x64 | Native AOT | [dd036e32](linux-x64/native-aot.receipt) | [native-aot.save](linux-x64/native-aot.save) | 171바이트 일치 |

네 배포본에 같은 475프레임·31개 이벤트를 `--replay-input`으로 전달했다. 입력 SHA-256은 `22638c8c350a7679ce2d44cb6682b9530f636f1bf4e09e85ba567fe8f4b5aa70`이다. 매번 빈 저장 폴더를 사용했고 SDL 비디오·오디오 드라이버를 사용할 수 없게 지정했다. 각 플레이어가 생성한 `slot-001.save`를 게스트에서 비교한 뒤 Linux는 SSH, Windows는 VM 종료 후 가상 USB를 통해 Mac으로 회수해 원본과 다시 비교했다. [입력과 Mac 플레이 기록](../input-replay/README.md) · [게스트 실행기](../../InventoryLab.Replay/README.md)

최종 저장의 SHA-256은 네 경우 모두 `eed41defcbd7987a527deb677c60da8151ad0de1616c717c646e92b26e70f795`다. 약초 3개, 수정 1개, 약초상 의뢰의 수집량 3과 보상 수령 완료, 대화 노드 `paid`가 저장됐다. [Mac 기준 저장](../../fixtures/InventoryLab/Herbalist.save) · [Mac에서 비교한 기록](comparison.txt) · [Linux 게스트 로그](linux-x64/build.log) · [Windows 게스트 로그](win-x64/build.log)

Windows의 첫 오프라인 복원에서 `NU1101`이 발생해 SDK와 같은 버전의 WindowsDesktop 런타임 팩을 추가했다. 첫 AOT 링크는 `LNK1104: OLDNAMES.lib`에서 실패했다. 같은 MSVC 버전의 누락된 x64 CRT 라이브러리를 보완한 후 게스트 CLI에서 AOT를 새로 빌드했다. 최종 실행에서 Windows의 `fc /b`도 차이 0건을 보고했다. Windows 로그에는 실패와 성공 두 시도가 함께 들어 있다. 두 VM은 정상 종료했고 게스트 디스크·빌드 도구·배포본을 보존했다. [Windows 재실행 환경](../../../../docs/troubleshooting/windows.md#오프라인-게스트의-native-aot-빌드) · [Debian 재실행 환경](../../../../docs/troubleshooting/linux.md#inventorylab-헤드리스-빌드회귀)
