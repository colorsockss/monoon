# MoNoon Engine 사용자 매뉴얼

MoNoon에서 게임을 만드는 순서는 프로젝트 열기 → 씬 편집 → Play → 저장 → 게임 빌드다. 아래 안내는 배포된 에디터의 영문 메뉴 이름을 사용한다.

| 하고 싶은 일 | 읽을 장 |
| --- | --- |
| 프로젝트 열기, 오브젝트·부모·자식·프리팹 편집, 단축키 찾기 | [에디터와 프로젝트](editor.md) |
| 에셋 검색·임포트, 의존성과 누락 참조 확인, 데이터·테이블 편집 | [Project 에셋 관리](project-assets.md) |
| 플레이어 이동, 충돌·트리거, 카메라 전환, 효과음, C# 컴포넌트 작성 | [런타임과 컴포넌트](runtime.md) |
| 배경음악 스트리밍, 페이드, 버스 볼륨, 공간 음향 | [오디오와 믹서](audio.md) |
| 카메라로 플레이어 추적, 맵 경계·분할 화면·미니맵 만들기 | [Camera 2D](cameras.md) |
| 충돌 도형·레이어·마스크 편집, 겹침·레이캐스트·스윕 질의 | [충돌 도형과 공간 질의](collision.md) |
| 한 칸씩 이동하기, 시드와 턴 기록 저장, 자동·한 턴씩 재생 | [턴과 리플레이](turn-replay.md) |
| 시드로 미로 만들기, 맵 재생성, 실행 중 타일·충돌 교체 | [절차 생성과 시드](generation.md) |
| 카메라 컷, 대사 작성·넘기기, 건너뛰기와 조작 복귀 | [컷신과 대화](cutscenes.md) |
| 왕복 발판, 뼈대 키프레임, 경사에 맞춘 발 IK | [트윈·스켈레탈 애니메이션·IK](rig-animation.md) |
| 착지 먼지, 절차적 스프라이트 셰이더, 횃불 조명 | [파티클·셰이더·조명](effects.md) |
| 경사 보행과 적의 순찰·추적, 탑다운 그리드의 지형 비용과 길찾기 | [내비게이션과 AI](navigation.md) |
| 강체와 거리 조인트 만들기, 경사 걷기·점프·이동 발판 | [플랫포머 물리](platformer-physics.md) |
| 약초 의뢰 코드를 따라 주요 엔진·패키지 API 사용하기 | [InventoryLab 스크립트 API](api.md) |
| 기존 게임 프로젝트 변환, 구버전 저장 복원과 두 형식의 빌드 검증 | [프로젝트 업그레이드](upgrade.md) |
| 컴포넌트 생명주기, 타입 참조, 공용 C# 파일 만들기 | [프로젝트 C# 스크립팅](scripting.md) |
| 방 전환, 실행 중 구역·UI 씬 추가와 제거, 공유 에셋 해제 | [씬 전환과 리소스 수명](scene-streaming.md) |
| 데이터 에셋 만들기, 슬롯별 진행 저장·복원, 사용자 설정과 버전 이전 | [데이터 에셋과 플레이어 저장](player-data.md) |
| Play Inspector 값 조정·선택 적용, 물리 질의·충돌선, 실시간 콘솔·프레임 프로파일러 확인 | [실행 진단](play-diagnostics.md) |
| 씬 복구·병합, 에셋 Undo, 패키지·템플릿·확장 명령 | [작업 복구와 프로젝트 확장](workspace.md) |
| Play 일시정지, 한 틱 진행, 배속과 타이머 | [시간 제어](time.md) |
| 프로젝트 액션 추가, 키·마우스·패드 연결, 기기에 맞는 조작 안내 | [입력 액션](input-actions.md) |
| 게임 창·테두리 없는 전체 화면 전환, VSync와 창 크기 복원 | [DisplayLab 화면 설정](game-ui.md#데스크톱-화면-설정) |
| HUD·스크롤 목록 만들기, 한글 입력·번역·글자 크기와 키보드·패드 탐색 | [게임 화면 UI](game-ui.md) |
| PNG 배치, 클립·컴포넌트 키프레임·프레임 이벤트 편집, 파라미터로 상태 전환 | [스프라이트와 애니메이션](sprites.md) |
| 직교·등각·육각 타일, 경사·단방향 발판, 오토타일·청크 스트리밍 | [타일맵과 타일셋](tilemaps.md) |
| 시작 씬 선택, Release·Native AOT 내보내기, CLI·CI와 회귀 검사, 플랫폼 오류 해결 | [빌드와 배포](build.md) |

## 첫 실행

1. Apple Silicon Mac에서 [MoNoon.app](../../editor/MoNoon.app)을 연다. **File → Open Project…**로 [KeyRoom](../../samples/KeyRoom/) 폴더를 선택한다. 새 게임은 **File → New Project…**에서 빈 폴더를 선택한다.
2. **Hierarchy**에서 **Player**를 선택한다. **Inspector**에서 **PlayerMovement**의 **MoveSpeed**를 바꾸고 **Play**를 누른다. 방향키나 WASD로 이동 속도를 확인한다. **Stop** 또는 **Esc**로 편집 화면에 돌아온다.
3. **File → Save Scene**으로 저장한다. **Run → Save & Run**을 선택하면 현재 씬이 독립 게임 창에서 열린다. 여러 씬을 묶어 배포하려면 **Build → Build Game…**을 사용한다.

## 샘플에서 찾아보기

| 프로젝트에서 열 파일 | 직접 해 볼 작업 |
| --- | --- |
| [InventoryLab](inventory.md) | 세 칸에 아이템을 쌓고 [약초 수집 의뢰](quests.md)를 완료한다. [약초상의 대사와 선택지](dialogue.md)를 편집하고 저장한 대화에서 이어간다. |
| [CardLab](decks.md) | 시드로 섞은 덱에서 카드를 뽑아 사용하고, 저장한 손패에서 이어서 플레이한다. |
| [BarrageLab](sprite-pools.md) | 탄막을 발사·중지하고, 같은 탄막에서 묶음·개별 제출과 슬롯 재사용을 비교한다. 충돌체와 스크립트가 있는 프리팹 탄환을 풀링과 생성·제거로 쏘아 명중 수를 비교한다. |
| [RhythmLab](rhythm.md) | 음악에 맞춰 Space를 누르고 박 번호와 빠름·정확·늦음 판정을 비교한다. |
| [KeyRoom.scene](../../samples/KeyRoom/Scenes/KeyRoom.scene) | 열쇠를 먹기 전과 후의 문 충돌, 카메라 전환, Controls 버튼, 다음 방 이동을 확인한다. |
| [Transform 2D: 상자와 뚜껑](transform-2d.md) | 회전·스케일·피벗, 부모 변경과 월드 좌표 보존을 확인한다. |
| [런타임 프리팹: KeyRoom의 상자와 코인](runtime-prefabs.md) | E로 상자를 열어 코인을 생성하고, 태그·그룹 조회와 트리거 획득을 확인한다. |
| [Door.clip](../../samples/KeyRoom/Assets/Door.clip) | Project에서 더블클릭하고 타임라인의 두 프레임을 바꾼 뒤 문이 열리는 순서를 확인한다. |
| [Room.tileset](../../samples/KeyRoomTiles/Assets/Room.tileset) | **KeyRoomTiles** 프로젝트에서 바닥과 벽의 Collision 설정을 비교한다. |
| [GridLayouts.scene](../../samples/KeyRoomTiles/Scenes/GridLayouts.scene) | 같은 시트를 쓰는 마름모·육각형 두 방향을 한 씬에서 편집하고 충돌 경계를 비교한다. |

이 매뉴얼의 기능별 절차와 별도로, 기존 프로젝트 변환과 실행 장애는 [플랫폼별 문제 해결](../troubleshooting/README.md)에서 찾을 수 있다.
