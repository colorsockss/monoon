# AccessLab — macOS UI·접근성 검증

2026-11-14, macOS 27.0.1 arm64. [AccessLab](../../../../samples/AccessLab/)을 에디터 Scene Play, .NET Release 앱, Native AOT 앱으로 각각 실행했다. 에디터는 프로젝트 복사본을 열고, 세 프로세스는 서로 다른 빈 사용자 데이터 폴더에서 시작했다.

| 조작 및 실측 | Scene Play | Release | Native AOT |
| --- | --- | --- | --- |
| macOS 두벌식으로 이름 입력, 조합을 거친 뒤 저장 | 한글 | 한글 | 한글 |
| Tab·Enter로 언어와 글자 크기 변경 | 일본어 · 150% | 일본어 · 150% | 일본어 · 150% |
| 가상 패드 D-pad ↓ / 아래쪽 버튼, 왼쪽 스틱 ↓ / 아래쪽 버튼 | 고대비 → 저장 | 고대비 → 저장 | 고대비 → 저장 |
| 저장 시 포커스 / 스크롤 오프셋(게임 좌표) | 저장 / 55 | 저장 / 55 | 저장 / 55 |
| 리사이즈 후 화면에 그려진 저장 버튼 중앙 클릭 | 1100×720, 재저장 | 920×660, 재저장 | 920×660, 재저장 |

이름은 완성된 문자열을 주입하지 않고 실제 두벌식 키 순서 **ㅎ ㅏ ㄴ ㄱ ㅡ ㄹ**을 입력했다. 마지막에 공백으로 확정하고 Backspace로 공백을 지웠다. [Scene Play 조합](editor-composition.png) · [Release 조합](release-composition.png) · [Native AOT 조합](aot-composition.png). 조합 중 텍스트가 한 번 이상 관찰됐는지 샘플도 기록한다. 입력 소스를 임시로 선택한 경우 원래 선택으로 되돌린다.

게임패드는 각 프로세스의 SDL에 연결한 [테스트 전용 가상 컨트롤러](../../Accessibility.Mac/virtual-pad.c)를 썼다. 포커스나 클릭 함수를 직접 호출하지 않았다. 실물 컨트롤러나 OS 스크린 리더의 검증은 포함하지 않는다. 리사이즈 후에는 캡처의 노란 포커스 테두리를 찾아 그 중앙에 마우스 클릭을 보냈고, 파일의 수정 시각이 바뀌면서 값이 유지되는지 확인했다.

[Scene Play 저장](editor.save), [Release 저장](release.save), [Native AOT 저장](aot.save) 파일의 SHA-256은 모두 `3332d3954a0c8730b73807e3807b5869852c2316bc3b88cc9dbe15663d057e86`다. 각 창의 장식·배율이 다르므로 전체 스크린샷을 픽셀 단위로 동일하다고 보지는 않는다.

- 리사이즈 결과: [Scene Play](editor-resized.png) · [Release](release-resized.png) · [Native AOT](aot-resized.png)
- 에디터 정렬: [1080 폭 Inspector](layout-inspector.png) · [285 폭 프로젝트 설정](ui-defaults.png)
- [GUI 실측 기록](gui.txt) · [엔진 회귀 검사](checks.txt) · [GUI 재실행](../../Accessibility.Mac/README.md). 자동 검사에는 가로·세로 스크롤, 클리핑과 클릭, 숨김 행 재배치, 지역 언어 폴백, 다국어 줄바꿈, 결합 문자·이모지 편집을 포함한다.
