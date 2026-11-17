# KeyRoom 가방의 언어 전환 프레임

2026-11-17, macOS 27.0.1 arm64와 기존 QEMU Windows 11 Enterprise LTSC x64 및 Debian 12 x64에서 해당 OS의 KeyRoom Distribution 배포본을 실행했다. Mac과 Windows는 데스크톱 창, Debian은 Xvfb의 X11 창을 사용했다. 각 실행은 빈 프로필에서 I로 가방을 열고 L로 한국어, 다시 L로 영어를 선택했다.

| 환경 | 배포본 | 영어 → 한국어 프레임 | 한국어 → 영어 프레임 | 실행 기록 |
| --- | --- | ---: | ---: | --- |
| macOS arm64 | Release | [77](osx-arm64/release/frames/77.png) | [138](osx-arm64/release/frames/138.png) | [입력](osx-arm64/release/bag.inputreplay) · [판독·해시](osx-arm64/release/frames.json) · [빌드](osx-arm64/release/build.receipt) |
| macOS arm64 | Native AOT | [83](osx-arm64/native-aot/frames/83.png) | [144](osx-arm64/native-aot/frames/144.png) | [입력](osx-arm64/native-aot/bag.inputreplay) · [판독·해시](osx-arm64/native-aot/frames.json) · [빌드](osx-arm64/native-aot/build.receipt) |
| Windows 11 x64 | Release | [32](win-x64/release/frames/32.png) | [37](win-x64/release/frames/37.png) | [입력](win-x64/release/bag.inputreplay) · [판독·해시](win-x64/release/frames.json) · [빌드](win-x64/release/build.receipt) |
| Windows 11 x64 | Native AOT | [24](win-x64/native-aot/frames/24.png) | [29](win-x64/native-aot/frames/29.png) | [입력](win-x64/native-aot/bag.inputreplay) · [판독·해시](win-x64/native-aot/frames.json) · [빌드](win-x64/native-aot/build.receipt) |
| Debian 12 x64 | Release | [19](linux-x64/release/frames/19.png) | [32](linux-x64/release/frames/32.png) | [입력](linux-x64/release/bag.inputreplay) · [판독·해시](linux-x64/release/frames.json) · [빌드](linux-x64/release/build.receipt) |
| Debian 12 x64 | Native AOT | [31](linux-x64/native-aot/frames/31.png) | [46](linux-x64/native-aot/frames/46.png) | [입력](linux-x64/native-aot/bag.inputreplay) · [판독·해시](linux-x64/native-aot/frames.json) · [빌드](linux-x64/native-aot/build.receipt) |

Windows AOT는 첫 시도에서 검사기가 기록 중인 PNG를 열어 파일 공유 오류가 발생했다. 파일 쓰기 완료를 기다리도록 검사기를 수정한 뒤 같은 빌드와 빈 프로필로 전체 입력을 다시 기록했다. 표에는 재실행 결과를 실었다.

프레임 번호는 각 실행에서 0부터 센다. 입력 기록에서 L을 처음 누른 이벤트의 번호를 찾아, 같은 게임 루프에서 UI 갱신과 SDL 그리기를 마친 직후 화면을 읽었다. 프레임 숫자 링크는 SDL에서 저장한 전체 화면 원본이다. 같은 폴더의 `en-to-ko.png`와 `ko-to-en.png`에서는 가방만 원래 배율로 볼 수 있다.

한국어 첫 프레임에서 ‘가방’, ‘휴대용 나침반 / 보관 중’, ‘의뢰 / 황동 열쇠 찾기’를 읽었다. 영어 첫 프레임에서도 ‘Inventory’, ‘Pocket compass / In bag’, ‘Quest / Find Brass key’를 확인했다. 여섯 실행 모두 영어로 되돌린 첫 프레임의 제목·아이템·의뢰 영역 RGB가 I로 가방을 처음 연 영어 화면과 바이트 단위로 일치했다. 판독 파일에는 전체 SDL 프레임의 SHA-256, 화면 크기, OCR 문구와 세 영역의 RGB 해시가 들어 있다.

로컬 엔진 회귀 검사는 L 입력 하나당 게임 루프를 한 번 진행한 직후 첫 그리기의 세 문자열을 검증한다. `./monoon test`가 통과했다. [엔진 회귀 검사](../../../tests/Engine.Checks/LocalizationChecks.cs) · [배포본에서 입력 프레임 기록하기](../../../../docs/manual/build.md) · [수정 전 Windows에서 포착된 전환 프레임](../sdl-player/x64/win-x64/transitional-bag.png)

이전 macOS Release에서는 같은 게임 루프에서 L을 처리한 첫 그리기에 이전 언어의 아이템과 의뢰가 양방향 모두 남았다. [수정 전 세 문자열](pre-fix-console.txt)
