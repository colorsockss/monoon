# Windows·Linux x64에서 KeyRoom 번역과 게임 폰트

2026-11-16에 이 Mac에 보존한 QEMU Windows 11 Enterprise LTSC x64(빌드 26100.1742)와 Debian 12.15 amd64에서 현재 KeyRoom을 각각 Release와 Native AOT로 빌드했다. 네 배포본의 프로필은 모두 `distribution`이다. Windows에서는 게스트 데스크톱, Debian에서는 Xvfb의 X11 디스플레이에 800×450 SDL 게임 창을 열었다. 각 실행은 개별 저장 폴더에서 시작했다. [Windows 빌드 로그](win-x64/first-launch.log) · [Windows 재실행 로그](win-x64/build.log) · [Debian 빌드·실행 로그](linux-x64/build.log)

| 실행 환경 | 빌드 | 열쇠 획득 전 | 획득 후 | 영어로 복귀 | 한국어 장착 확인 | 빌드 기록 |
| --- | --- | --- | --- | --- | --- | --- |
| Windows x64 | Release | [찾기](win-x64/release/ko.png) | [완료](win-x64/release/ko-complete.png) | [영어](win-x64/release/en-complete.png) | [확인창](win-x64/release/ko-confirm.png) | [`d01294c1`](win-x64/release/MoNoonBuild) |
| Windows x64 | Native AOT | [찾기](win-x64/native-aot/ko.png) | [완료](win-x64/native-aot/ko-complete.png) | [영어](win-x64/native-aot/en-complete.png) | [확인창](win-x64/native-aot/ko-confirm.png) | [`a5fa3e9b`](win-x64/native-aot/MoNoonBuild) |
| Debian x64 | Release | [찾기](linux-x64/release/ko.png) | [완료](linux-x64/release/ko-complete.png) | [영어](linux-x64/release/en-complete.png) | [확인창](linux-x64/release/ko-confirm.png) | [`10c97a0c`](linux-x64/release/MoNoonBuild) |
| Debian x64 | Native AOT | [찾기](linux-x64/native-aot/ko.png) | [완료](linux-x64/native-aot/ko-complete.png) | [영어](linux-x64/native-aot/en-complete.png) | [확인창](linux-x64/native-aot/ko-confirm.png) | [`e6023e41`](linux-x64/native-aot/MoNoonBuild) |

## 재현과 화면 대조

1. **I**로 가방을 열어 `Pocket compass / In bag`과 `Quest / Find Brass key`를 확인했다. 가방 오른쪽의 **한국어 [L]** 버튼을 마우스로 클릭했다.
2. **가방**, **휴대용 나침반 / 보관 중**, **의뢰 / 황동 열쇠 찾기**가 한 화면에 나타났다. **I**로 닫고 왼쪽 방향키를 눌러 화면 왼쪽 위에 열쇠 아이콘이 나타날 때까지 이동했다.
3. **I**로 다시 열었을 때 **의뢰 완료 / 황동 열쇠**가 표시됐다. **L**로 영어로 바꿔도 **Quest complete / Brass key**로 완료 상태가 유지됐다.
4. **L**로 한국어로 돌아와 **Enter**를 누르자 **확인**, **나침반을 장착할까요?**, **적용**, **취소**가 표시됐다.

네 실행에서 캡처한 다섯 상태를 한국어·영어 OCR로 읽고 Mac 기준 화면과 가방 제목·아이템·의뢰의 글자 모양도 대조했다. OCR 원문, 실행 파일 SHA-256, 빌드 ID와 비교 수치를 함께 보관했다. [Mac 기준 캡처](../README.md) · [비교 데이터](comparison.json)

같은 OS에서 Release와 Native AOT의 320×150 가방·확인 패널을 픽셀 단위로 비교했다. 동일한 상태는 Windows 5/5개, Debian 5/5개였다.

Windows 첫 일괄 실행은 게임 창이 키보드 포커스를 얻지 못해 입력 전에 중단됐다. 같은 빌드 ID의 실행 파일을 다시 열고, 게임 창의 가려지지 않은 씬 영역을 실제로 클릭해 포커스를 확인한 뒤 위 입력을 반복했다. [첫 실행과 포커스 오류](win-x64/first-launch.log)

## 기본·대체 글꼴

네 실행 모두 `MONOON_FONT_TRACE=1`로 기록했다. 화면에 등장한 서로 다른 한글 음절 40개가 Mac과 동일한 글꼴에서 나왔고 `missing` 기록은 0건이었다. **가·방**은 기본 **고운돋움**, **휴·황·쇠**는 대체 **나눔고딕**에서 선택됐다. 네 배포본의 번역 테이블, TTF 두 파일과 각 라이선스의 SHA-256도 Mac Release 및 Native AOT와 일치했다. [음절별 글꼴 선택](fonts.tsv)

게스트 빌드는 Windows의 `C:\MNLocale67\exports`와 Debian의 `~/MNLocale67/exports`에 남겼다. 각 폴더 안에서 Release는 `KeyRoom-<RID>`, Native AOT는 `NativeAOT/KeyRoom-<RID>`에 있다. 결과 회수 뒤 두 VM을 정상 종료하고 디스크와 빌드 도구를 보존했다.
