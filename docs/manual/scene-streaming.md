# 씬 전환과 리소스 수명

KeyRoom에서 **M**으로 방 안내를 열고 **Close**를 누른다. 방 안내는 별개의 `RoomGuide.scene`이다. 원래 방의 카메라와 물리 계산은 계속되고, 안내를 닫으면 그 씬의 UI와 버튼 클릭 영역이 함께 사라진다. 왼쪽에서 열쇠를 얻고 오른쪽 문을 통과하면 진행 막대를 표시한 뒤 두 번째 방이 열린다. [방 안내 씬](../../samples/KeyRoom/Scenes/RoomGuide.scene) · [실제 화면](../../engine/tests/results/scene-streaming/README.md)

## 씬을 연결하는 방법

1. Project에서 목적지 씬을 만들고 **Build → Build Game… → Included scenes**에 포함한다. 시작 씬과 함께 실행할 구역은 별도 카메라 없이 만들 수 있다. UI 전용 씬은 단독으로도 실행할 수 있다.
2. 스크립트에 `public SceneReference Destination;` 필드를 선언하고 Inspector에서 목적지 씬을 고른다. KeyRoom의 **Exit → RoomExit → Destination**은 두 번째 방을 가리킨다.
3. 다음 조작 중 게임에 맞는 것을 호출한다. 씬 전환이나 추가가 필요 없는 동안은 현재 씬을 계속 사용한다.

| 호출 | 게임에서 일어나는 일 |
| --- | --- |
| `state.LoadSceneAsync(Destination.Id, "Entering…", 0.45)` | 현재 게임의 입력·시뮬레이션을 멈추고 전환 화면을 그린다. 준비가 끝나고 UI 시간으로 최소 0.45초가 지나면 현재 월드와 추가 씬들을 목적지로 교체한다. 기본 최소 시간은 0초다. 문구에 `null`을 주면 준비 중 기존 화면을 표시한다. |
| `state.AddSceneAsync(Guide.Id)` | 기존 게임을 계속 실행하면서 씬을 읽고 준비한다. 준비가 끝난 뒤 같은 월드에 오브젝트를 등록한다. 그때부터 충돌, 카메라, Canvas 정렬 순서, UI 입력, Play Hierarchy와 Inspector를 공유한다. |
| `state.UnloadScene(Guide.Id)` | 다음 업데이트 경계에서 추가 씬과 그 소유 오브젝트를 제거한다. 주 씬을 바꾸려면 교체를 사용한다. |
| `state.IsSceneLoaded(id)` | 주 씬 또는 추가 완료된 씬인지 조회한다. `state.SceneId`는 현재 주 씬의 ID다. |

세 메서드는 `SceneOperation`을 반환한다. `Status`로 대기·읽기·준비·완료·실패·취소를 구분하고, `Error`에서 실패 원인을 읽는다. `Progress`는 읽기와 에셋 준비 단계의 진행도다. 게임 스레드에서 `Cancel()`을 호출하면 준비 중인 씬을 폐기한다. 한 번에 한 작업을 처리하며, 진행 중 들어온 다른 비동기 요청은 실패 상태를 돌려준다. 이미 추가된 씬을 다시 추가하거나 이미 제거한 씬을 다시 제거하면 그대로 완료한다. [실제 열기·닫기 스크립트](../../samples/KeyRoom/Scripts/RoomGuide.cs)

기존 `LoadScene(id, onEntered)`는 같은 업데이트에서 교체한다. `LoadSceneAsync`의 `onEntered`도 새 월드가 준비된 뒤 첫 `OnCreate` 전에 실행하므로 복원할 플래그를 설정할 수 있다. 두 방식 모두 세션 플래그와 플레이어 저장소를 유지한다.

## 씬과 에셋이 해제되는 시점

- YAML 씬 읽기, 파일 참조 확인, PNG·오디오 디코딩은 백그라운드에서 한다. 컴포넌트 생성과 SDL 텍스처·오디오 업로드는 게임 스레드에서 업데이트당 예산 안에서 한다. [큰 방을 나눠 읽기](#큰-방을-여러-프레임에-나눠-읽기)
- 같은 PNG 또는 WAV를 여러 씬이 쓰면 한 번 준비한다. 마지막 참조가 사라질 때 SDL 텍스처 또는 디코딩한 WAV를 해제한다. 재생 중인 효과음은 스트림에 이미 복사된 데이터를 끝까지 재생한다. 스크립트가 읽은 YAML 데이터 에셋은 읽은 씬들이 해제될 때 캐시에서 빠진다.
- 추가 씬의 스크립트가 부모 없이 만든 프리팹은 그 씬에 속한다. 부모를 지정하면 부모의 씬에 속한다. 씬을 제거하면 활성 컴포넌트의 `OnDisable`과 생성된 컴포넌트의 `OnDestroy`가 호출된다.
- 파일 오류·업로드 실패·취소 시 이미 실행 중인 방은 유지한다. Play를 중지하면 준비 중인 작업과 해당 세션이 보유한 리소스도 정리한다.

## 큰 방을 여러 프레임에 나눠 읽기

`LoadSceneAsync`와 `AddSceneAsync`는 로딩 작업을 여러 업데이트에 나눈다. CollisionLab [하네스 복사본](../../engine/tests/Shared/collision_lab.py)에서 **3**은 충돌체 1만 개 방으로 교체하고, **4**는 현재 방을 계속 실행하면서 그 방을 추가한다. 추가 중 **Backspace**를 누르면 취소한다. [실제 스크립트](../../samples/CollisionLab/Scripts/FieldSelector.cs)

1. 워커 스레드가 YAML 씬을 읽는다.
2. 게임 스레드가 업데이트마다 `state.SceneLoadBudgetMilliseconds`(기본 4 ms) 동안 오브젝트와 컴포넌트를 만든다. 예산이 끝나면 남은 오브젝트는 다음 업데이트에서 만든다. 오브젝트 하나는 중간에 멈추지 않는다.
3. 오브젝트를 모두 만들면 충돌·그리기 색인을 한 번에 만든다.
4. 아직 올라가지 않은 PNG·WAV·Ogg는 워커 스레드가 디코딩한다. 게임 스레드는 디코딩이 끝난 파일부터 같은 예산 안에서 SDL 텍스처·오디오로 올린다. 디코딩을 기다리는 업데이트에서는 게임만 진행한다.
5. 남은 업데이트에서 현재 월드에 연결하거나 교체한다.

추가하는 동안 기존 방의 물리·스크립트·UI 입력은 계속 실행된다. 교체하는 동안에는 전환 화면과 진행 막대가 매 프레임 갱신된다.

| 설정 | 동작 |
| --- | --- |
| `state.SceneLoadBudgetMilliseconds = 8;` | 다음 비동기 작업부터 업데이트당 최대 8 ms를 쓴다. 값이 작으면 프레임은 짧아지고 로딩 프레임 수는 늘어난다. |
| `state.SceneLoadBudgetMilliseconds = 0;` | 이전 방식이다. 준비를 시작한 업데이트에서 씬 전체를 만들고, 업데이트마다 파일 하나를 게임 스레드에서 디코딩·업로드한다. |

개발용 빌드와 에디터 Scene Play는 환경 변수 `MONOON_SCENE_LOAD_BUDGET_MS`(밀리초)로 스크립트를 고치지 않고 예산을 바꾼다. 배포용 빌드는 이 변수를 무시한다.

- 진행 중 `Cancel()`을 호출하면 만든 오브젝트, 올린 텍스처·오디오, 디코딩이 끝난 버퍼를 해제한다. 디코딩 중인 파일은 워커가 끝나는 대로 버린다. 게임 스레드는 워커를 기다리지 않는다. 교체 중에는 기존 방의 스크립트가 실행되지 않으므로, 스크립트에서 취소하려면 추가를 사용한다.
- 오브젝트 하나의 생성자, 텍스처 한 장의 업로드, 색인 생성, 월드 연결은 나누지 않는다. CollisionLab 1만 개 방에서는 색인 생성과 연결이 남은 가장 긴 프레임이다. [Mac 측정 결과](../../engine/tests/results/split-loading/README.md)
- 개발용 빌드의 프레임 프로파일러는 Scene.Loading 아래에 Scene.Instantiate(오브젝트 생성), Scene.Upload(업로드), Scene.Commit(연결·교체)을 기록한다. [느린 프레임 분석](play-diagnostics.md#느린-프레임의-작업-분석)
- 같은 프로젝트를 동기 `LoadScene`, 예산 0, 예산 4 ms로 읽으면 오브젝트·위치·`OnCreate` 호출·상주 파일이 같다.
