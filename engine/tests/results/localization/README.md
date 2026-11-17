# KeyRoom 공용 번역과 한글 폰트 검증

2026-11-16, macOS 27.0.1 arm64에서 MoNoon 편집기의 Scene Play와 KeyRoom의 두 배포 앱에 키보드·마우스 입력을 보냈다. 각 실행은 별도 플레이어 데이터 폴더에서 시작했다.

| 실행 | 열쇠를 줍기 전 | 주운 뒤 | 한국어 → 영어로 복귀 |
| --- | --- | --- | --- |
| Scene Play | [황동 열쇠 찾기](editor-ko.png) | [의뢰 완료](editor-ko-complete.png) | Quest complete / Brass key |
| Release | [황동 열쇠 찾기](release-ko.png) | [의뢰 완료](release-ko-complete.png) | Quest complete / Brass key |
| Native AOT | [황동 열쇠 찾기](aot-ko.png) | [의뢰 완료](aot-ko-complete.png) | Quest complete / Brass key |

## 재현 입력

1. 첫 방의 시작점에서 **I**를 눌러 가방을 연다. **Inventory**, **Pocket compass / In bag**과 진행 중인 의뢰가 보인다.
2. 가방 오른쪽의 **한국어 [L]** 버튼을 클릭한다. 같은 패널에서 **가방**, **휴대용 나침반 / 보관 중**, **의뢰 / 황동 열쇠 찾기**가 표시된다.
3. **I**로 가방을 닫고 왼쪽 방향키를 약 0.4초 눌러 열쇠를 줍는다. 다시 **I**를 누르면 **의뢰 완료 / 황동 열쇠**가 보인다.
4. **L**로 영어로 전환하면 완료 상태가 유지된다. **L**로 한국어로 돌아온 뒤 **Enter**로 나침반 관리에 들어가면 **확인**, **나침반을 장착할까요?**, **적용**, **취소**가 표시된다.

세 실행의 가방과 확인창에서 위 문구를 화면 캡처와 한국어 OCR로 확인했다. Release와 Native AOT에서 가방을 같은 크기(760×360픽셀)로 잘라 비교했다. 열쇠 획득 전후 각각 두 앱의 픽셀 값이 모두 일치했다.

## 글꼴과 빌드

`MONOON_FONT_TRACE=1`로 실행하여 렌더러가 실제 선택한 글꼴을 기록했다. 세 실행 모두 ‘가방’은 **GowunDodum-Regular.ttf**, ‘휴’, ‘황’, ‘쇠’는 **NanumGothic-Regular.ttf**에서 그렸다. 이 흐름에서 사용된 서로 다른 한글 음절은 40개였고 누락 글리프는 없었다. 회귀 검사에서는 번역 테이블 전체에 등장하는 한글 음절 93개의 글리프 존재 여부를 검사했다. 기본·대체 글꼴을 함께 지정한 ‘가’와 ‘휴’는 해당 글꼴만 지정해서 그린 기준 비트맵과 각각 일치했다. [실행별 글꼴 기록](font-selection.tsv) · [회귀 검사](../../Engine.Checks/LocalizationChecks.cs)

두 앱의 프로필은 `distribution`이다. Release 빌드 ID는 `92ab8e0a54ea407580e4aebc78adc0bc`, Native AOT 빌드 ID는 `46f69dd2376f44ff81ca1b082a376c43`다. `Contents/MacOS/Game` 안에 들어간 번역 테이블, TTF 두 개, 각 글꼴의 라이선스 파일을 SHA-256으로 대조했고 모두 프로젝트 원본과 일치했다.

에디터의 **Project → UI Accessibility…** 설정 창은 440pt와 최소 폭 340pt에서 확인했다. 번역 테이블·언어·기본 폰트·대체 폰트 선택 행의 왼쪽 기준선이 일치했고, 긴 경로를 선택해도 삭제·저장 버튼이 창 안에 있었다. 전체 엔진 회귀 검사도 통과했다. [설정 방법](../../../../docs/manual/game-ui.md#공용-번역-테이블과-게임-폰트)

같은 키 입력과 언어 전환을 Windows·Linux x64 VM의 Release·Native AOT에서도 확인했다. [네 배포본의 화면과 폰트 비교](x64/README.md)
