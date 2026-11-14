# MoNoon Engine 문서

실행 중 문제가 생겼으면 **어떤 창에서 실패했는지**부터 고른다. Scene 패널에만 생기는 문제와 배포 게임이 시작조차 못 하는 문제는 조사할 코드와 재현 환경이 다르다.

| 필요한 문서 | 다루는 내용 |
| --- | --- |
| [범용 2D 엔진 로드맵](roadmap.md) | 공통 런타임부터 장르 도구·배포까지의 개발 순서 |
| [MoNoon Engine 사용자 매뉴얼](manual/README.md) | 프로젝트 열기부터 컴포넌트, UI, 애니메이션, 네 가지 타일 Layout과 게임 배포까지 실제 조작 순서 |
| [공통 진단과 KeyRoom 재검증](troubleshooting/README.md) | 템플릿, YAML 에셋, 빌드 종류, 기존 VM 재사용 |
| [macOS](troubleshooting/macos.md) · [Windows](troubleshooting/windows.md) · [Linux](troubleshooting/linux.md) | OS별 에디터/게임 실행 명령, 증상, 조치와 검증 기록 |
| [스프라이트 애니메이션 에디터 설계](design/sprite-animation-editor.md) | 기존 클립을 타임라인에서 편집하고 실제 게임과 같은 프레임을 미리 본다. |
| [인게임 UI: Canvas와 버튼](design/in-game-ui.md) | 게임 화면에 요소를 배치하고, 클릭·키보드 입력을 프로젝트 스크립트로 받는다. KeyRoom에 적용했다. |
| [타일맵 에디터 설계](design/tilemap-editor.md) | 같은 PNG 영역 그리기를 재사용해 Scene에서 타일 레이어를 칠하고 충돌을 설정한다. |

[KeyRoomTiles 샘플](../samples/KeyRoomTiles/)에서 타일셋·Scene 페인팅과 정적 타일 충돌을 직접 확인할 수 있다. [플랫폼별 검증 기록](../engine/tests/results/tilemap/README.md)
