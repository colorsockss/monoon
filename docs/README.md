# MoNoon Engine 문서

실행 중 문제가 생겼으면 **어떤 창에서 실패했는지**부터 고른다. Scene 패널에만 생기는 문제와 배포 게임이 시작조차 못 하는 문제는 조사할 코드와 재현 환경이 다르다.

| 필요한 문서 | 다루는 내용 |
| --- | --- |
| [범용 2D 엔진 로드맵](roadmap.md) | 남은 작업과 보류 항목, 다음 장(게임 개발자용 AI 에이전트 도구) |
| [구현 기록](history/README.md) | 구현을 마친 기능이 어떻게 만들어져 있고 왜 그렇게 정했는지(런타임, 렌더링, UI·오디오, 에디터, 빌드·배포) |
| [MoNoon Engine 사용자 매뉴얼](manual/README.md) | 프로젝트 열기부터 컴포넌트, UI, 애니메이션, 네 가지 타일 Layout과 게임 배포까지 실제 조작 순서 |
| [BRUTAL 프레임워크 기능 정리](brutal.md) | 엔진이 기반으로 쓰는 BRUTAL 패키지별 기능과 MoNoon에서 사용할 때의 경계·라이선스 |
| [타일맵 샘플 게임 3종 계획](plans/samples/README.md) | 직교 플랫포머, 아이소메트릭 용병단 RPG, 헥사고날 문명식 전략 샘플의 설계·엔진 보강·단계 |
| [다음 작업 계획 (2026-11-23 조사)](plans/next-work/README.md) | Inspector·엔진 기능, 배포 용량·시작 시간, 코드·에셋 trim, 파티클 에디터, AI 에이전트 개발 방식, 게임 개발자용 AI 도구 조사와 결정 사항 |
| [개발 문서](development.md) · [에이전트 작업 흐름](manual/agent-workflow.md) | 엔진 저장소 구조와 개발 규칙, 에이전트 워크트리·GUI 잠금·하네스·결과 기록 |
| [InventoryLab 게임 스크립트 API](manual/api.md) | 컴포넌트 참조, 입력·UI, 가방·의뢰·대화, 저장 슬롯 호출 예제 |
| [기존 프로젝트 업그레이드](manual/upgrade.md) | 소스·GUID와 구버전 저장을 보존하며 버전 35에서 검증한 순서로 Release·Native AOT로 내보내기 |
| [공통 진단과 KeyRoom 재검증](troubleshooting/README.md) | 템플릿, YAML 에셋, 빌드 종류, 기존 VM 재사용 |
| [macOS](troubleshooting/macos.md) · [Windows](troubleshooting/windows.md) · [Linux](troubleshooting/linux.md) | OS별 에디터/게임 실행 명령, 증상, 조치와 검증 기록 |
| [스프라이트 애니메이션 에디터 설계](design/sprite-animation-editor.md) | 기존 클립을 타임라인에서 편집하고 실제 게임과 같은 프레임을 미리 본다. |
| [인게임 UI: Canvas와 버튼](design/in-game-ui.md) | 게임 화면에 요소를 배치하고, 클릭·키보드 입력을 프로젝트 스크립트로 받는다. KeyRoom에 적용했다. |
| [타일맵 에디터 설계](design/tilemap-editor.md) | 같은 PNG 영역 그리기를 재사용해 Scene에서 타일 레이어를 칠하고 충돌을 설정한다. |

[KeyRoomTiles 샘플](../samples/KeyRoomTiles/)에서 타일셋·Scene 페인팅과 정적 타일 충돌을 직접 확인할 수 있다. [플랫폼별 검증 기록](../engine/tests/results/tilemap/README.md)
